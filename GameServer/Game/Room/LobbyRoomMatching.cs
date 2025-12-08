using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  public partial class LobbyRoom : Room
  {

    private float matchTickAcc = 0f;
    private const float MatchTryInterval = 1.0f; // 1초마다 재시도

    // ─────────────────────────────────────────────────────────────
    // 튜닝 파라미터
    // ─────────────────────────────────────────────────────────────
    private const int RatingBucketSize = 25; // 레이팅 버킷 간격
    private const int PlayersPerMatch = 2;   // 확장 대비(현재 1vs1)
    private const int BaseDiff = 50;         // 기본 허용 레이팅 차
    private const int DiffGrowPerSec = 10;   // 초당 허용 증가
    private const int MaxDiffCap = 500;      // 허용 상한

    // ─────────────────────────────────────────────────────────────
    // 자료구조
    // ─────────────────────────────────────────────────────────────

    /// <summary>큐에 들어가는 최소 정보</summary>
    private struct MatchEntry
    {
      public Player Player;         // 매칭 성사 시 넘겨줄 Player
      public int Rating;            // 큐 진입 시점의 레이팅 스냅샷
      public DateTime EnqueueUtc;   // 큐 진입 시각
    }

    /// <summary>역인덱스: 빠른 제거를 위해 플레이어 위치 저장</summary>
    private struct NodeRef
    {
      public int BucketKey;
      public LinkedListNode<MatchEntry> Node;
    }

    // 버킷 키(= rating / RatingBucketSize) → FIFO 큐
    private readonly Dictionary<int, LinkedList<MatchEntry>> ratingBuckets =
      new Dictionary<int, LinkedList<MatchEntry>>();

    // player.ObjectID → (버킷키, 노드참조)
    private readonly Dictionary<int, NodeRef> ratingIndex =
      new Dictionary<int, NodeRef>();

    // 레이팅 → 버킷 키(음수 방지)
    private int ToBucketKey(int rating)
    {
      if (rating < 0) rating = 0;
      return rating / RatingBucketSize;
    }


    /// <summary>플레이어를 매칭 큐에 등록</summary>
    public void EnqueuePlayer(Player player)
    {
      if (player == null) return;

      // 중복 등록 방지
      if (ratingIndex.ContainsKey(player.ObjectID))
        return;

      int playerRating = player.playerStatInfo.Rating;
      int bucketKey = ToBucketKey(playerRating);

      LinkedList<MatchEntry> list;
      if (!ratingBuckets.TryGetValue(bucketKey, out list))
      {
        list = new LinkedList<MatchEntry>();
        ratingBuckets[bucketKey] = list;
      }

      MatchEntry entry = new MatchEntry
      {
        Player = player,
        Rating = playerRating,
        EnqueueUtc = DateTime.UtcNow
      };

      LinkedListNode<MatchEntry> node = list.AddLast(entry);
      ratingIndex[player.ObjectID] = new NodeRef { BucketKey = bucketKey, Node = node };

      // 바로 매칭 시도 (대기시간 최소화)
      CompleteMatchByRating();
    }

    /// <summary>대기 취소</summary>
    public void CancelMatch(Player player)
    {
      if (player == null) return;

      NodeRef info;
      if (!ratingIndex.TryGetValue(player.ObjectID, out info))
        return; // 큐에 없음

      LinkedList<MatchEntry> list;
      if (ratingBuckets.TryGetValue(info.BucketKey, out list))
      {
        list.Remove(info.Node);
        if (list.Count == 0)
          ratingBuckets.Remove(info.BucketKey);
      }

      ratingIndex.Remove(player.ObjectID);
    }

    /// <summary>로비에서 제거될 때 큐에서도 정리</summary>
    public override void PlayerRomve(int objectId)
    {
      NodeRef info;
      if (ratingIndex.TryGetValue(objectId, out info))
      {
        LinkedList<MatchEntry> list;
        if (ratingBuckets.TryGetValue(info.BucketKey, out list))
        {
          list.Remove(info.Node);
          if (list.Count == 0)
            ratingBuckets.Remove(info.BucketKey);
        }
        ratingIndex.Remove(objectId);
      }

      // 부모(Room)의 players 딕셔너리 및 링크 해제
      base.PlayerRomve(objectId);
    }


    // ─────────────────────────────────────────────────────────────
    // 매칭 코어
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 가능한 한 많은 1vs1 매칭을 즉시 성사.
    /// - 버킷 키를 정렬 순서로 순회 (결정성 확보)
    /// - 각 버킷의 헤드(앵커)를 기준으로 주변 버킷에서 최적 파트너 탐색
    /// </summary>
    public void CompleteMatchByRating()
    {
      if (ratingBuckets.Count == 0)
        return;

      bool matchedInThisPass = true;

      while (matchedInThisPass)
      {
        matchedInThisPass = false;

        // 매 라운드마다 키 스냅샷 + 정렬 (매칭 중 버킷이 비워질 수 있음)
        List<int> bucketKeys = new List<int>(ratingBuckets.Keys);
        bucketKeys.Sort();

        // bucketKeys: 현재 라운드에서 스냅샷한 버킷 키 목록(오름차순 정렬됨)
        for (int i = 0; i < bucketKeys.Count; i++)
        {
          int centerBucketKey = bucketKeys[i];
          // 예) centerBucketKey = 49 (앵커가 들어있는 중심 버킷)

          LinkedList<MatchEntry> centerList;
          if (!ratingBuckets.TryGetValue(centerBucketKey, out centerList))
            continue;
          // 예) 방금 전 매칭/취소로 버킷이 사라졌다면 스킵하고 다음 버킷으로

          // 이 버킷의 "맨 앞(헤드)" = 앵커를 기준으로 반복 시도(FIFO 보장)
          while (centerList.Count > 0)
          {
            LinkedListNode<MatchEntry> anchorNode = centerList.First;
            MatchEntry anchor = anchorNode.Value;
            // 예) anchor.Player.ObjectID == 101, anchor.Rating == 1230, anchor가 버킷49의 맨 앞

            LinkedListNode<MatchEntry> partnerNode; // 후보가 발견되면 여기에 담김
            int partnerBucketKey;                   // 후보가 속한 버킷 키 (같은 버킷일 수도, 이웃 버킷일 수도)
            bool found = TryFindPartner(anchor, centerBucketKey, out partnerNode, out partnerBucketKey);
            // TryFindPartner 동작(요약):
            //   - 반경 0(49) -> 좌1(48) -> 우1(50) -> 좌2(47) -> 우2(51) ... 순서로 탐색
            //   - 허용치(기다린 시간 기반) 안에서 diff(레이팅 차)가 가장 작은 후보를 고름
            //   - diff == 0(완벽 매치)이면 즉시 true 반환
            // 예) partnerNode.Value.Player.ObjectID == 202, partnerBucketKey == 48 로 찾았다고 가정

            if (found)
            {
              // ─────────────────────────────────────────────────────────
              // [매칭 성사] 앵커(헤드)와 파트너 둘 다 큐/인덱스에서 제거
              // ─────────────────────────────────────────────────────────

              // 1) 중심 버킷(49)에서 앵커 노드 제거 + 역인덱스에서 제거
              RemoveQueuedNode(centerBucketKey, anchorNode);
              // 예) 버킷49의 맨 앞 A(101)를 제거 → 버킷49 리스트에서 A 빠짐, ratingIndex에서도 101 제거

              // 2) 파트너가 있던 버킷(48 or 49 or 50 ...)에서 파트너 노드 제거 + 역인덱스에서 제거
              RemoveQueuedNode(partnerBucketKey, partnerNode);
              // 예) 버킷48의 D(202)를 제거 → 버킷48 리스트에서 D 빠짐, ratingIndex에서도 202 제거

              // 3) 로비 players 딕셔너리에서도 링크 해제 (이제 로비 대기자가 아니라 "매칭된 사람")
              base.PlayerRomve(anchor.Player.ObjectID);
              base.PlayerRomve(partnerNode.Value.Player.ObjectID);
              // 예) players 딕셔너리에서 101, 202 삭제. 
              //     이렇게 해야 로비에서 중복 처리/중복 입장 같은 문제가 안 생김.

              // 4) 실제 게임 방 생성 (1vs1) — 프로젝트의 RoomManager가 담당
              RoomManager.Instance.Create1vs1Room(anchor.Player, partnerNode.Value.Player);
              // 예) A(101) vs D(202) 방 생성 → 이 시점부터 두 플레이어는 게임룸 로직으로 넘어감

              // 5) “이번 라운드에서 매칭이 최소 1번은 일어났다” 표시
              matchedInThisPass = true;

              // 6) 방금 앵커를 빼면서 center 버킷(49)이 비었을 수 있으니, 최신 핸들 다시 확보
              if (!ratingBuckets.TryGetValue(centerBucketKey, out centerList))
                break;
              // 예) 버킷49가 이제 비었다면 더 볼 게 없으니 while 탈출 → 다음 버킷(i+1)로
              //     (비지 않았다면 while이 계속 돌면서 이 버킷의 "새 헤드"로 다시 시도)
            }
            else
            {
              // [매칭 실패] 지금 이 헤드(anchor)는 아직 안 붙음.
              // FIFO(헤드 우선) 공정성을 위해 "헤드를 건너뛰지 않음".
              // → 이 버킷 처리를 여기서 끝내고 다음 버킷으로 이동.
              break;
              // 예) A(101)가 아직은 못 붙음 → 시간이 흐르며 허용치가 커지면 다음 라운드에 다시 시도
            }
          }
        }


      }
    }


    /// <summary>대기 시간에 따라 허용 레이팅 차이를 계산</summary>
    private int AllowedDiffAt(DateTime nowUtc, DateTime enqueuedUtc)
    {
      double waited = (nowUtc - enqueuedUtc).TotalSeconds;
      if (waited < 0) 
        waited = 0;

      double allow = BaseDiff + waited * DiffGrowPerSec;
      if (allow > MaxDiffCap) 
        allow = MaxDiffCap;

      return (int)Math.Floor(allow);
    }

    /// <summary>
    /// 앵커가 속한 버킷부터 좌/우로 반경을 넓혀가며
    /// 허용 범위 내에서 "레이팅 차가 가장 작은" 파트너를 찾는다.
    /// </summary>
    private bool TryFindPartner(
      MatchEntry anchor,
      int anchorBucketKey,
      out LinkedListNode<MatchEntry> bestNode,
      out int bestBucketKey)
    {
      bestNode = null;
      bestBucketKey = -1;

      if (ratingBuckets.Count == 0)
        return false;

      DateTime nowUtc = DateTime.UtcNow;
      int anchorAllowed = AllowedDiffAt(nowUtc, anchor.EnqueueUtc);

      // 허용치/버킷크기 기반으로 대략 몇 칸까지 보면 될지 계산
      int maxRadius = anchorAllowed / RatingBucketSize + 1;
      if (maxRadius < 1) 
        maxRadius = 1;

      int bestDiff = int.MaxValue;

      // 중심 → 좌/우 1칸 → 좌/우 2칸 …
      for (int radius = 0; radius <= maxRadius; radius++)
      {
        if (radius == 0)
        {
          if (CheckBucketForPartner(anchorBucketKey, anchor, anchorAllowed, nowUtc,
                                    ref bestDiff, ref bestNode, ref bestBucketKey))
            return true; 
        }
        else
        {
          if (CheckBucketForPartner(anchorBucketKey - radius, anchor, anchorAllowed, nowUtc,
                                    ref bestDiff, ref bestNode, ref bestBucketKey))
            return true;

          if (CheckBucketForPartner(anchorBucketKey + radius, anchor, anchorAllowed, nowUtc,
                                    ref bestDiff, ref bestNode, ref bestBucketKey))
            return true;

        }
      }

      return bestNode != null;
    }

    /// <summary>
    /// 단일 버킷을 스캔하여 최적 후보를 갱신한다.
    /// 완벽 매치(diff==0)를 찾으면 true 반환.
    /// </summary>
    private bool CheckBucketForPartner(
      int bucketKey,
      MatchEntry anchor,
      int anchorAllowed,
      DateTime nowUtc,
      ref int bestDiff,
      ref LinkedListNode<MatchEntry> bestNode,
      ref int bestBucketKey)
    {
      if (bucketKey < 0)
        return false;

      LinkedList<MatchEntry> list;
      if (!ratingBuckets.TryGetValue(bucketKey, out list) || list == null || list.Count == 0)
        return false;

      LinkedListNode<MatchEntry> node = list.First;
      while (node != null)
      {
        MatchEntry cand = node.Value;

        // 자기 자신 제외
        if (cand.Player.ObjectID != anchor.Player.ObjectID)
        {
          int diff = Math.Abs(anchor.Rating - cand.Rating);
          int candAllowed = AllowedDiffAt(nowUtc, cand.EnqueueUtc);
          int allowed = Math.Max(anchorAllowed, candAllowed);

          if (diff <= allowed && diff < bestDiff)
          {
            bestDiff = diff;
            bestNode = node;
            bestBucketKey = bucketKey;

            if (bestDiff == 0)
              return true; // 완벽 매치 즉시 종료
          }
        }

        node = node.Next;
      }

      return false;
    }

    /// <summary>버킷/역인덱스에서 노드 제거 (O(1))</summary>
    private void RemoveQueuedNode(int bucketKey, LinkedListNode<MatchEntry> node)
    {
      LinkedList<MatchEntry> list;
      if (ratingBuckets.TryGetValue(bucketKey, out list))
      {
        list.Remove(node);
        if (list.Count == 0)
          ratingBuckets.Remove(bucketKey);
      }

      int objectId = node.Value.Player.ObjectID;
      ratingIndex.Remove(objectId);
    }
  }
}


