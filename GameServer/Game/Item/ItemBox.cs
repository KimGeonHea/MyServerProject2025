using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public static class ItemBox
  {
    // --------------------------------------------------------
    // [등급 확률] 만분율(BP, Basis Point) — 10000 = 100%
    // Ancient = 0.1% → 10 BP
    // --------------------------------------------------------
    private const int COMMON_BP = 6700; // 67.00%
    private const int STRONG_BP = 2200; // 22.00%
    private const int RARE_BP = 1000; // 10.00%
    private const int LEGENDARY_BP = 90; //  0.90%
    private const int ANCIENT_BP = 10; //  0.10%

    // 등급별 가중치 테이블 (읽기 쉬움)
    private static readonly Dictionary<EItemGrade, int> _gradeBp = new()
  {
    { EItemGrade.Common,    COMMON_BP },
    { EItemGrade.Strong,    STRONG_BP },
    { EItemGrade.Rare,      RARE_BP },
    { EItemGrade.Legendary, LEGENDARY_BP },
    { EItemGrade.Ancient,   ANCIENT_BP },
  };

    // RNG (멀티스레드면 lock 사용)
    private static readonly Random random = new();
    private static readonly object _rock = new();

    // 등급별 풀을 한 곳에서 관리
    private static readonly Dictionary<EItemGrade, List<ItemData>> _pool = new()
  {
    { EItemGrade.Common,    new List<ItemData>() },
    { EItemGrade.Strong,    new List<ItemData>() },
    { EItemGrade.Rare,      new List<ItemData>() },
    { EItemGrade.Legendary, new List<ItemData>() },
    { EItemGrade.Ancient,   new List<ItemData>() },
  };

    // ========================================================
    // 1) 풀 만들기
    //    - 스택형(Stacable = true) 제외
    //    - 장비(ItemType == EItemType.Equipment)만 포함
    //    - 등급별로 분류해서 _pool에 저장
    // ========================================================
    public static void BuildPools(IEnumerable<ItemData> allItems)
    {
      foreach (var kv in _pool) kv.Value.Clear();

      foreach (var it in allItems ?? Enumerable.Empty<ItemData>())
      {
        if (it == null) continue;

        // 1) 스택형(상자/재화 등) 제외
        if (it.Stacable) continue;

        // 2) 장비만 포함 — 문자열 비교 X, enum 비교 O
        if (it.ItemType != EItemType.Equipment) continue;

        // 3) 등급은 이미 enum이므로 그대로 사용
        _pool[it.Grade].Add(it);
      }
    }

    // ========================================================
    // 2) 상자 1회 오픈 (천장 X)
    //    - 등급을 가중치로 뽑고 → 그 등급 안에서 "균등" 1개 선택
    //    - 비어있는 등급이 나올 수 있으니 몇 번 재시도
    // ========================================================
    public static bool OpenOnce(out ItemData picked)
    {
      picked = null;

      for (int retry = 0; retry < 20; retry++)
      {
        var grade = PickGradeByWeightIgnoringEmpty(); // 비어있는 등급은 후보 제외
        var list = _pool[grade];
        if (list.Count == 0) continue;

        int idx;
        lock (_rock)
        {
          idx = random.Next(list.Count);
        }
        picked = list[idx];
        return true;
      }

      // 모든 등급이 비어있거나 데이터가 잘못된 경우
      return false;
    }

    // ========================================================
    // (옵션) 3) 상자 1회 오픈 (천장 포함)
    //    - pityCount: "해당 박스"의 현재 피티(연속 실패) 값 (DB에서 불러와 ref로 전달)
    //    - pityThreshold: 예) 100 → 100번째는 보장
    //    - Ancient 풀에 아이템이 있어야 보장 가능
    // ========================================================
    public static bool OpenOnceWithPity(ref int pityCount, int pityThreshold, out ItemData picked)
    {
      picked = null;
      if (pityThreshold <= 0) pityThreshold = 100; // 안전장치

      // 보장 발동 조건: (임계-1) 이상 + Ancient 풀 존재
      if (pityCount >= pityThreshold - 1 && HasAnyAncient())
      {
        picked = PickOneFromAncient();
        pityCount = 0;            // 보장으로 Ancient 지급 → 리셋
        return picked != null;
      }

      // 일반 뽑기
      if (!OpenOnce(out picked)) return false;

      // 피티 갱신: Ancient면 리셋, 아니면 +1
      pityCount = IsAncient(picked) ? 0 : pityCount + 1;
      return true;
    }

    // --------------------------------------------------------
    // 내부: 비어있는 등급을 제외하고 "가중치 뽑기"
    // --------------------------------------------------------
    private static EItemGrade PickGradeByWeightIgnoringEmpty()
    {
      // 1) 후보 모으기(아이템이 1개 이상 있는 등급만)
      var candidates = new List<(EItemGrade grade, int weight)>(5);
      int total = 0;

      foreach (var kv in _gradeBp)
      {
        var grade = kv.Key;
        int weight = kv.Value;
        if (weight <= 0) continue;

        var list = _pool[grade];
        if (list.Count == 0) continue; // 비어있으면 후보 제외

        candidates.Add((grade, weight));
        total += weight;
      }

      // 모든 등급이 비어있으면 안전장치 (실제로는 OpenOnce가 false 반환)
      if (total <= 0) 
        return EItemGrade.Common;

      // 2) 0 <= r < total
      int r;
      lock (_rock)
      {
        r = random.Next(total);
      }

      // 3) 누적합으로 구간 찾기
      int acc = 0;
      foreach (var c in candidates)
      {
        acc += c.weight;
        if (r < acc) 
          return c.grade;
      }
      return candidates[^1].grade; // 이론상 도달 X, 안전장치
    }

    // --------------------------------------------------------
    // 유틸
    // --------------------------------------------------------
    public static bool HasAnyAncient() => _pool[EItemGrade.Ancient].Count > 0;

    public static ItemData PickOneFromAncient()
    {
      var list = _pool[EItemGrade.Ancient];
      if (list.Count == 0) return null;

      int idx;
      lock (_rock)
      {
        idx = random.Next(list.Count);
      }
      return list[idx];
    }

    public static bool IsAncient(ItemData item) =>
      item != null && item.Grade == EItemGrade.Ancient;
  }
}
