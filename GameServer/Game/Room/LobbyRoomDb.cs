using GameServer.Utils;
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
      AddReward(player, ERewardType.ErwardTypeObject, itemTemplateId: 20000, count: 1);
    }

    /// <summary>
    /// 여기부터 주간 리워드
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
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

    public void HandleCheckDailyReward(Player player)
    {
      if (player == null) 
        return;

      // 유저 설정(없으면 기본)
      string tzId = (!string.IsNullOrWhiteSpace(player.playerStatInfo?.TimeZoneId) ? player.playerStatInfo.TimeZoneId : "Asia/Seoul");
      byte resetHour = (byte)Math.Clamp((int)player.ResetHourLocal, 0, 23); // 전역 9시면 9 고정
      DayOfWeek weekStart =
          (player.WeekStartDay >= DayOfWeek.Sunday && player.WeekStartDay <= DayOfWeek.Saturday)
          ? player.WeekStartDay : DayOfWeek.Monday;

      // 지금/마지막 수령 UTC
      DateTime nowUtc = TzUtil.AsUtc(DateTime.UtcNow);
      DateTime lastUtc = TzUtil.AsUtc(player.LastDailyRewardTime);

      // 오늘 리셋 창(현지 resetHour ~ 다음 resetHour)
      //예시 : 2025-09-02 09:00, 2025-09-03 09:00 로 변환
      //UTC 2025-09-02 00:00:00Z ,  2025-09-03 00:00:00Z
      (DateTime startUtc, DateTime endUtc) = TzUtil.GetDailyWindowUtc(tzId, nowUtc, resetHour);

      //예시 : 2025-09-02 09:00, 2025-09-03 09:00 의 사이 값 경계가 아니면 리턴
      if (lastUtc >= startUtc && lastUtc < endUtc)
        return;

      // 주간 경계: 지난 주였다면 진행도 초기화
      DateTime weekStartNowUtc = TzUtil.GetWeekStartUtc(tzId, weekStart, startUtc, resetHour);
      if (lastUtc != DateTime.MinValue)
      {
        var (lastStartUtc, _) = TzUtil.GetDailyWindowUtc(tzId, lastUtc, resetHour);
        DateTime weekStartLastUtc = TzUtil.GetWeekStartUtc(tzId, weekStart, lastStartUtc, resetHour);
        if (weekStartLastUtc < weekStartNowUtc)
          player.WeeklyRewardFlags = 0;
      }
      else
      {
        player.WeeklyRewardFlags = 0;
      }

      // 이번 주 진행도 = 세워진 비트 개수 (0~7)
      int countThisWeek = CountSetBits(player.WeeklyRewardFlags);
      if (countThisWeek >= 7)
        return; // 이번 주 이미 7회 완료

      // 순차 방식: 다음 비트를 세운다 (요일과 무관)
      int nextBit = 1 << countThisWeek;
      player.WeeklyRewardFlags |= nextBit;


      // 타임스탬프/보상
      player.LastDailyRewardTime = nowUtc;   // 항상 UTC 저장
      int rewardDay = countThisWeek + 1;     // 1~7 보상 단계
      RewardDaily(player, rewardDay);

      // DB 부분 업데이트
      DBManager.Push(player.PlayerDbId, () =>
      {

        using var db = new GameDbContext();
        var row = new PlayerDb
        {
          PlayerDbId = player.PlayerDbId,
          LastDailyRewardTime = player.LastDailyRewardTime,
          WeeklyRewardFlags = player.WeeklyRewardFlags
        };
        db.playerDbs.Attach(row);
        db.Entry(row).Property(p => p.LastDailyRewardTime).IsModified = true;
        db.Entry(row).Property(p => p.WeeklyRewardFlags).IsModified = true;
        db.SaveChangesEx();
      });
    }


    private void RewardDaily(Player player, int day)
    {
      switch (day)
      {
        case 1:
          AddReward(player, ERewardType.ErwardTypeGold, count: 500); // 100 골드
          break;

        case 2:
          AddReward(player, ERewardType.ErwardTypeDiamod, count: 50); // 50 다이아
          break;

        case 3:
          AddReward(player, ERewardType.ErwardTypeObject, itemTemplateId: 20000, count: 1); // 특정 아이템 1개
          break;

        case 4:
          AddReward(player, ERewardType.ErwardTypeGold, count: 1000); // 1000 골드
          break;

        case 5:
          AddReward(player, ERewardType.ErwardTypeDiamod, count: 100); // 100 다이아
          break;

        case 6:
          AddReward(player, ERewardType.ErwardTypeObject, itemTemplateId: 20000, count: 1); // 특정 아이템 2개
          break;

        case 7:
          AddReward(player, ERewardType.ErwardTypeObjects, itemTemplateId: 20001 , count:1); // 다양한 아이템 1개씩
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
          DBupdateGoldAndDaimond(player);
          break;

        case ERewardType.ErwardTypeDiamod:
          player.playerStatInfo.Daimond += count;
          player.ApplyAddOrDeleteGoldDiaEnergy(type, count);
          DBupdateGoldAndDaimond(player);
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
    public void DBupdateGoldAndDaimond(Player player)
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
      if (DataManager.ItemDataDict.TryGetValue(itemTemplateId, out ItemData itemData) == false)
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
