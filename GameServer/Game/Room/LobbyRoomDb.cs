using Google.Protobuf.Protocol;
using Server;
using Server.Data;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  public partial class LobbyRoom : Room
  {

    public void RewardOpenToClinetRewardListSend(Player player)
    {
      if (player == null)
        return;

      char[] result = new char[7];
      for (int i = 0; i < 7; i++)
        result[i] = (player.WeeklyRewardFlags & (1 << i)) != 0 ? '1' : '0';

      S_DailyRewardOpen ptk = new S_DailyRewardOpen();
      ptk.DailyRewardOpen = new string(result); // 문자열 변환은 이렇게!

      player.Session?.Send(ptk); // 주석 처리된 부분 복구
    }

    public void RewardTest(Player player)
    {
      AddReward(player, ERewardType.ErwardTypeObject, itemTemplateId: 10031, count: 1);
    }

    int CountSetBits(int n)
    {
      int count = 0;
      while (n != 0)
      {
        n &= (n - 1);
        count++;
      }
      return count;
    }

    static DateTime EffectiveLocalDate(DateTime localNow, int resetHourLocal)
    {
      var cut = new DateTime(localNow.Year, localNow.Month, localNow.Day, resetHourLocal, 0, 0, localNow.Kind);
      return (localNow < cut) ? localNow.Date.AddDays(-1) : localNow.Date;
    }

    // Monday=0 … Sunday=6 로 매핑(weekStartsMonday=true 기준)
    static (DateTime weekStart, int dayIndex) GetWeekStartAndIndex(DateTime localDate, bool weekStartsMonday)
    {
      int dow = (int)localDate.DayOfWeek; // Sunday=0 … Saturday=6
      if (weekStartsMonday)
      {
        int monBased = (dow + 6) % 7;          // Monday=0 … Sunday=6

        return (localDate.AddDays(-monBased), monBased);
      }
      else
      {
        return (localDate.AddDays(-dow), dow); // Sunday=0 … Saturday=6
      }
    }

    public void CheckDailyReward(
        Player player,
        int resetHourLocal = 9,
        bool weekStartsMonday = true,
        string timeZoneId = "Asia/Seoul" // 계정별로 저장해두면 더 좋음
    )
    {
      // 1) 타임존 로딩
      TimeZoneInfo tz;
      try { tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
      catch { tz = TimeZoneInfo.Local; }

      // 2) 현재/마지막 수령시각을 UTC 기준으로 정규화
      DateTime nowUtc = DateTime.UtcNow;

      DateTime lastUtc = player.LastDailyRewardTime;
      if (lastUtc != default)
      {
        if (lastUtc.Kind == DateTimeKind.Local) lastUtc = lastUtc.ToUniversalTime();
        else if (lastUtc.Kind == DateTimeKind.Unspecified) lastUtc = DateTime.SpecifyKind(lastUtc, DateTimeKind.Utc);
      }

      // 3) 로컬 변환 + 리셋 규칙 적용(리셋 전은 어제)
      DateTime nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
      DateTime todayUI = EffectiveLocalDate(nowLocal, resetHourLocal);

      DateTime lastLocal = (lastUtc == default) ? DateTime.MinValue
                                                : TimeZoneInfo.ConvertTimeFromUtc(lastUtc, tz);
      DateTime lastUI = (lastLocal == DateTime.MinValue) ? DateTime.MinValue
                                                            : EffectiveLocalDate(lastLocal, resetHourLocal);

      // 4) 오늘 이미 수령했으면 종료
      if (todayUI == lastUI)
      {
        Console.WriteLine("오늘 이미 보상 받음");
        return;
      }

      // 5) 주 경계 체크(지난 주였다면 플래그 초기화)
      var (weekStart, todayIndex) = GetWeekStartAndIndex(todayUI, weekStartsMonday);
      if (lastUI < weekStart)
        player.WeeklyRewardFlags = 0;

      // 6) 오늘 칸 비트 계산(요일 기반)
      int todayBit = 1 << todayIndex;

      // 동시성/중복 방지: 이미 세팅되어 있으면 종료
      if ((player.WeeklyRewardFlags & todayBit) != 0)
        return;

      // 7) 오늘 비트 세팅 + 보상
      player.WeeklyRewardFlags |= todayBit;
      player.LastDailyRewardTime = nowUtc;

      // 보상 단계: 요일 고정형이면 todayIndex+1, 연속 출석형이면 CountSetBits(...) + 1 사용
      int rewardDay = todayIndex + 1; // 월=1, … 일=7 (weekStartsMonday=true 기준)
      RewardDaily(player, rewardDay);

      // 8) DB 반영 (부분 업데이트)
      DBManager.Push(player.PlayerDbId, () =>
      {
        using (var db = new GameDbContext())
        {
          var playerDb = new PlayerDb
          {
            PlayerDbId = player.PlayerDbId,
            LastDailyRewardTime = player.LastDailyRewardTime, // UTC 저장
            WeeklyRewardFlags = player.WeeklyRewardFlags
          };

          db.playerDbs.Attach(playerDb);
          db.Entry(playerDb).Property(p => p.LastDailyRewardTime).IsModified = true;
          db.Entry(playerDb).Property(p => p.WeeklyRewardFlags).IsModified = true;
          db.SaveChangesEx();
        }
      });
    }

    /// <summary>
    /// TODO UTC로 사용할껀지 한국 시간 및 다른 시간 적용 시킬껀지
    /// </summary>
    /// <param name="player"></param>
    public void CheckDailyReward(Player player)
    {
      DateTime now = DateTime.UtcNow.Date;
      DateTime last = player.LastDailyRewardTime.Date;
      // 오늘 이미 보상 받았으면 리턴
      if (last == now)
      {
        Console.WriteLine("오늘 이미 보상 받음");
        return;
      }
      // 이번 주 시작일 (월요일 기준)
      DateTime startOfWeek = now.AddDays(-((int)now.DayOfWeek + 6) % 7);

      // 월요일이고, 마지막 접속이 지난 주라면 플래그 초기화
      bool isMonday = ((int)now.DayOfWeek + 6) % 7 == 0;
      if (isMonday && last < startOfWeek)
      {
        player.WeeklyRewardFlags = 0;
      }

      // 이번 주 받은 일수 계산
      int rewardCountThisWeek = CountSetBits(player.WeeklyRewardFlags);

      if (rewardCountThisWeek >= 7)
      {
        Console.WriteLine("이번 주 보상 완료");
        return;
      }
      player.WeeklyRewardFlags |= (1 << rewardCountThisWeek);
      // 보상 시간 업데이트
      player.LastDailyRewardTime = now;
      // 보상 지급 (1일차 = index 0이므로 +1)
      RewardDaily(player, rewardCountThisWeek + 1);

      // 해당 일수 비트 세팅
    

      // DB 반영 데일리 반영
      DBManager.Push(player.PlayerDbId, () =>
      {
        using (GameDbContext db = new GameDbContext())
        {
          var playerDb = new PlayerDb()
          {
            PlayerDbId = player.PlayerDbId,
            LastDailyRewardTime = now,
            WeeklyRewardFlags = player.WeeklyRewardFlags
          };

          db.playerDbs.Attach(playerDb);
          db.Entry(playerDb).Property(p => p.LastDailyRewardTime).IsModified = true;
          db.Entry(playerDb).Property(p => p.WeeklyRewardFlags).IsModified = true;

          db.SaveChangesEx();
        }
      });
    }

    private void RewardDaily(Player player, int day)
    {
      switch (day)
      {
        case 1:
          AddReward(player, ERewardType.ErwardTypeGold, count: 100); // 100 골드
          break;

        case 2:
          AddReward(player, ERewardType.ErwardTypeDiamod, count: 50); // 50 다이아
          break;

        case 3:
          AddReward(player, ERewardType.ErwardTypeObject, itemTemplateId: 10001, count: 1); // 특정 아이템 1개
          break;

        case 4:
          AddReward(player, ERewardType.ErwardTypeGold, count: 1000); // 1000 골드
          break;

        case 5:
          AddReward(player, ERewardType.ErwardTypeDiamod, count: 100); // 100 다이아
          break;

        case 6:
          AddReward(player, ERewardType.ErwardTypeObject, itemTemplateId: 10020, count: 1); // 특정 아이템 2개
          break;

        case 7:
          AddReward(player, ERewardType.ErwardTypeObjects, objectIds: new List<int> { 1003, 1004, 1005 }); // 다양한 아이템 1개씩
          break;
      }
    }


    public void AddReward(Player player, ERewardType type ,int itemTemplateId = 0, int count = 0,List<int> objectIds = null)
    {
      switch (type)
      {
        case ERewardType.ErwardTypeGold:
          player.playerStatInfo.Gold += count;
          player.ApplyAddOrDeleteGoldDiaEnergy(type, count);
          SaveStatToDb(player);
          break;

        case ERewardType.ErwardTypeDiamod:
          player.playerStatInfo.Daimond += count;
          player.ApplyAddOrDeleteGoldDiaEnergy(type, count);
          SaveStatToDb(player);
          break;

        case ERewardType.ErwardTypeObject:
          if (itemTemplateId != 0)
          {
            RewardItem(player, itemTemplateId, count);
          }
          break;

        case ERewardType.ErwardTypeObjects:
          if (objectIds != null)
          {
            foreach (int id in objectIds)
            {
              RewardItem(player, itemTemplateId, count);
            }
          }
          break;
      }
    }
    public void SaveStatToDb(Player player)
    {
      DBManager.Push(player.PlayerDbId, () =>
      {
        using (GameDbContext db = new GameDbContext())
        {
          var playerDb = new PlayerDb
          {
            PlayerDbId = player.PlayerDbId,
            Gold = player.playerStatInfo.Gold,
            Diamond = player.playerStatInfo.Daimond
          };

          db.playerDbs.Attach(playerDb);
          db.Entry(playerDb).Property(p => p.Gold).IsModified = true;
          db.Entry(playerDb).Property(p => p.Diamond).IsModified = true;

          db.SaveChangesEx();
        }
      });
    }

    public void RewardItem(Player player, int itemTemplateId, int count)
    {
      if (MakeAddItemDb(player, itemTemplateId, count, out ItemDb newItemDb, out ItemDb stackItemDb, out int addStackCount) == false)
        return;

      // 메모리 선적용
      ApplyAddItemDbToMemory(player, newItemDb, stackItemDb, addStackCount);

      // DBThread
      DBManager.Push(player.PlayerDbId, () => DBManager.SaveItemDbChanges(player, newItemDb, stackItemDb));
    }

    public bool MakeAddItemDb(Player player, int itemTemplateId, int count, out ItemDb newItemDb, out ItemDb stackItemDb, out int addStackCount, EItemSlotType slotType = EItemSlotType.Inventory)
    {
      newItemDb = null;
      stackItemDb = null;
      addStackCount = 0;

      if (player == null || player.Room == null || player.inventory == null)
        return false;
      if (DataManager.itemDict.TryGetValue(itemTemplateId, out ItemData itemData) == false)
        return false;

      int remainingAddCount = 1;

      // 1. 기존 아이템과 병합 시도.
      if (itemData.Stacable)
      {
        remainingAddCount = count;

        Item stackItem = null;
        if (slotType == EItemSlotType.Inventory)
        {
          stackItem = player.inventory.GetAnyInventoryItemByCondition(stackItem => stackItem.TemplateId == itemTemplateId && stackItem.GetAvailableStackCount() > 0);
        }
        //else if (slotType == EItemSlotType.Warehouse)
        //{
        //  stackItem = player.inventory.GetAnyWarehouseItemByCondition(stackItem => stackItem.TemplateId == itemTemplateId && stackItem.GetAvailableStackCount() > 0);
        //}

        if (stackItem != null)
        {
          addStackCount = Math.Min(remainingAddCount, stackItem.GetAvailableStackCount());

          // 1-1. 아이템 수량 증가.
          stackItemDb = new ItemDb
          {
            ItemDbId = stackItem.ItemDbId,
            EquipSlot = slotType,
            Count = stackItem.Count + addStackCount,
          };

          // 1-2. 카운트 소모.
          remainingAddCount -= addStackCount;
        }
      }

      // 2. 새로 생성.
      if (remainingAddCount > 0)
      {
        if (player.inventory.IsInventoryFull())
          return false;

        newItemDb = new ItemDb
        {
          ItemDbId = DBManager.GenerateItemDbId(),
          TemplateId = itemTemplateId,
          EquipSlot = slotType,
          Count = remainingAddCount,
          PlayerDbId = player.PlayerDbId,
        };
      }

      return true;
    }

    public void ApplyAddItemDbToMemory(Player player, ItemDb newItemDb, ItemDb stackItemDb, int addStackCount)
    {
      if (player == null)
        return;

      if (newItemDb != null)
      {
        Item newItem = Item.MakeItem(newItemDb);
        player.inventory.Add(newItem, sendToClient: true);
      }

      if (stackItemDb != null)
      {
        Item stackItem = player.inventory.GetItemByDbId(stackItemDb.ItemDbId);
        if (stackItem != null)
          player.inventory.AddCount(stackItem.ItemDbId, addStackCount, sendToClient: true);
      }
    }
    


  }
}
