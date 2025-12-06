using GameServer;
using GameServer.Game;
using GameServer.Game.Room;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Server.Game
{

  public partial class DBManager : JobSerializer
	{
    public static void SaveItemDbChanges(Player player, ItemDb newItemDb, ItemDb stackItemDb, ItemDb existingItemDb = null)
    {
      Push(player.PlayerDbId, () =>
      {
        using (GameDbContext db = new GameDbContext())
        {
          if (newItemDb != null)
            db.Items.Add(newItemDb);

          if (stackItemDb != null)
          {
            db.Entry(stackItemDb).State = EntityState.Unchanged;
            db.Entry(stackItemDb).Property(nameof(ItemDb.Count)).IsModified = true;
            db.Entry(stackItemDb).Property(nameof(ItemDb.EquipSlot)).IsModified = true;
          }


          if (existingItemDb != null)
          {
            db.Entry(existingItemDb).State = EntityState.Unchanged;
            db.Entry(existingItemDb).Property(nameof(ItemDb.EquipSlot)).IsModified = true;
            db.Entry(existingItemDb).Property(nameof(ItemDb.Count)).IsModified = true;
            db.Entry(existingItemDb).Property(nameof(ItemDb.DbState)).IsModified = true;
          }

          bool success = db.SaveChangesEx();
          if (success)
          {
          }
        }
      });
    }

    public static void MakeItemAsync(Player player, int templateId)
    {
      Push(player.PlayerDbId, () =>
      {
        using (GameDbContext db = new GameDbContext())
        {
          ItemDb itemDb = new ItemDb()
          {
            ItemDbId = GenerateItemDbId(),
            PlayerDbId = player.PlayerDbId,
            TemplateId = templateId,
            Count = 1,
            EnchantCount = 0,
            EquipSlot = EItemSlotType.Inventory,
          };

          db.Items.Add(itemDb);

          bool success = db.SaveChangesEx();
          if (!success)
          {
            // 실패 시 로그
            Console.WriteLine($"[MakeItemAsync] SaveChanges 실패 - PlayerId:{player.PlayerDbId}");
            return;
          }
        }
      });
    }
    /// <summary>
    /// PlayerId, TemplateId 기준 가챠 피티 업서트
    /// Unchanged로 붙이고 PityCount/UpdatedAtUtc만 수정
    /// </summary>
    public static void SavePityAsync(Player player, int templateId, int pity)
    {
      Push(player.PlayerDbId, () =>
      {
        using (var db = new GameDbContext())
        {
          // PlayerId 타입 주의: GachaDb.PlayerId가 int? 이면 캐스팅 필요
          int playerId = checked((int)player.PlayerDbId);

          // PK가 GachaDbId라서 Find(playerId, templateId) 못 씀  조회 후 분기
          var exist = db.gachaDbs
                        .AsNoTracking()
                        .Where(x => x.PlayerId == playerId && x.TemplateId == templateId)
                        .Select(x => new { x.GachaDbId })
                        .FirstOrDefault();

          if (exist == null)
          {
            // 신규 추가
            var row = new GachaDb
            {
              GachaDbId = GenerateGachaDbId(),
              PlayerId = playerId,
              TemplateId = templateId,
              PityCount = pity,
              UpdatedAtUtc = DateTime.UtcNow
            };

            db.gachaDbs.Add(row);
          }
          else
          {
            // 부분 업데이트(분리 엔티티를 붙여 특정 컬럼만 수정)
            var row = new GachaDb
            {
              GachaDbId = exist.GachaDbId,
              PlayerId = playerId,   // 안전을 위해 세팅
              TemplateId = templateId, // 안전을 위해 세팅
              PityCount = pity,
              UpdatedAtUtc = DateTime.UtcNow
            };

            db.Entry(row).State = EntityState.Unchanged;
            db.Entry(row).Property(nameof(GachaDb.PityCount)).IsModified = true;
            db.Entry(row).Property(nameof(GachaDb.UpdatedAtUtc)).IsModified = true;
          }

          bool success = db.SaveChangesEx();
          if (!success)
          {
            Console.WriteLine($"[SavePityAsync] SaveChanges 실패 - PlayerId:{player.PlayerDbId}, TemplateId:{templateId}");
            return;
          }
        }
      });
    }

    public static void SaveDiamon(Player player)
    {
      Push(player.PlayerDbId, () =>
      {
        using (var db = new GameDbContext())
        {
          var playerDb = new PlayerDb
          {
            PlayerDbId = player.PlayerDbId,
            Diamond = player.Diamond
          };
          db.playerDbs.Attach(playerDb);
          db.Entry(playerDb).Property(p => p.Diamond).IsModified = true;
          db.SaveChangesEx();
        }
      });
    }


    public static void EquipItemNoti(Player player, Item item)
    {
      if (player == null || item == null)
        return;

      ItemDb itemDb = new ItemDb()
      {
        ItemDbId = item.ItemDbId,
        EquipSlot = item.ItemSlotType
      };

      Push(player.PlayerDbId, () =>
      {
        using (GameDbContext db = new GameDbContext())
        {
          db.Entry(itemDb).State = EntityState.Unchanged;
          db.Entry(itemDb).Property(nameof(ItemDb.EquipSlot)).IsModified = true;

          bool success = db.SaveChangesEx();
          if (!success)
          {

          }
        }
      });
    }

    public static void ItemSeenNoti(Player player, Item item)
    {
      if (player == null || item == null)
        return;

      // 여기서는 "이미 봤냐" 검사 안 함. 
      // 호출자가 이미 새 아이템인지 확인했다고 가정.
      var itemdb = new ItemDb
      {
        ItemDbId = item.ItemDbId,
        SeenAcquiredUtc = item.SeenAcquiredUtc, // 이미 메모리에서 Last로 맞춰놓은 값
                                                // LastAcquiredAtUtc 건드리지 말기!!
      };

      Push(player.PlayerDbId, () =>
      {
        using (var db = new GameDbContext())
        {
          db.Entry(itemdb).State = EntityState.Unchanged;
          db.Entry(itemdb).Property(nameof(ItemDb.SeenAcquiredUtc)).IsModified = true;

          db.SaveChangesEx();
        }
      });
    }
    public static void UpdatePlayerInventoryCapacity(Player player , int gold , int inventoryCapacity)
    {
      if(inventoryCapacity <= 0)
        return; 

      if (player == null)
        return;

      Push(player.PlayerDbId, () =>
      {
        using (GameDbContext db = new GameDbContext())
        {
          var playerDb = new PlayerDb
          {
            PlayerDbId = player.PlayerDbId,
            Gold = gold,
            InventoryCapacity = inventoryCapacity
          };

          db.playerDbs.Attach(playerDb);
          db.Entry(playerDb).Property(p => p.Gold).IsModified = true;
          db.Entry(playerDb).Property(p => p.InventoryCapacity).IsModified = true;
          db.SaveChangesEx();
        }
      });
    }

    public static void EquipHeroNoti(Player player, Hero hero1, Hero hero2)
    {
      if (player == null)
        return;

      Push(player.PlayerDbId, () =>
      {
        using (var db = new GameDbContext())
        {
          if (hero1 != null)
          {
            var dbHero1 = new HeroDb { HeroDbId = hero1.HeroDbId, Slot = hero1.Slot };
            db.Heroes.Attach(dbHero1);
            db.Entry(dbHero1).Property(e => e.Slot).IsModified = true;
          }

          if (hero2 != null)
          {
            var dbHero2 = new HeroDb { HeroDbId = hero2.HeroDbId, Slot = hero2.Slot };
            db.Heroes.Attach(dbHero2);
            db.Entry(dbHero2).Property(e => e.Slot).IsModified = true;
          }

          db.SaveChangesEx();
        }
      });
    }

    public static void DeleteItemNoti(Player player, Item item, bool dbNoti = true)
    {
      if (player == null || item == null)
        return;

      if (player.inventory.GetItemByDbId(item.Info.ItemDbId) == null)
        return;

      // 선적용.
      player.inventory.Remove(item, sendToClient: true);

      if (dbNoti == false)
        return;

      ItemDb itemDb = new ItemDb
      {
        ItemDbId = item.Info.ItemDbId,
        DbState = EItemDBState.Deleted // SoftDelete
      };

      // DBThread
      Push(player.PlayerDbId, () =>
      {
        using (GameDbContext db = new GameDbContext())
        {
          db.Entry(itemDb).Property(nameof(ItemDb.DbState)).IsModified = true;
          db.Entry(itemDb).State = EntityState.Deleted;
          bool success = db.SaveChangesEx();
          if (success == false)
          {
            // 실패했으면 Kick
          }
        }
      });
    }

    public static void DeleteItem(Player player, Item item)
    {
      if (player == null || item == null)
        return;

      if (player.inventory.GetItemByDbId(item.Info.ItemDbId) == null)
        return;

      ItemDb itemDb = new ItemDb
      {
        ItemDbId = item.Info.ItemDbId,
      };

      Instance.Push(DeleteItem_Step2, player, itemDb);
    }

    // DBThread
    public static void DeleteItem_Step2(Player player, ItemDb itemDb)
    {
      using (GameDbContext db = new GameDbContext())
      {
        db.Entry(itemDb).State = EntityState.Deleted;
    
        bool success = db.SaveChangesEx();
        if (success)
        {
          player.Room.Push(DeleteItem_Step3, player, itemDb);
        }
      }
    }


    // GameThread
    public static void DeleteItem_Step3(Player player, ItemDb itemDb)
    {
      Item item = player.inventory.GetItemByDbId(itemDb.ItemDbId);
      if (item == null)
        return;

      player.inventory.Remove(item, sendToClient: true);
    }


    public static bool MakeAddItemDb(Player player, int itemTemplateId, int count,
  out ItemDb newItemDb, out ItemDb stackItemDb, out int addStackCount,
  EItemSlotType slotType = EItemSlotType.Inventory)
    {
      newItemDb = null;
      stackItemDb = null;
      addStackCount = 0;

      if (player == null || player.Room == null || player.inventory == null)
        return false;

      if (DataManager.ItemDataDict.TryGetValue(itemTemplateId, out ItemData itemData) == false)
        return false;

      int remainingAddCount = 1;

      // 1. 기존 아이템과 병합 시도
      if (itemData.Stacable)
      {
        remainingAddCount = count;

        Item stackItem = null;
        if (slotType == EItemSlotType.Inventory)
        {
          stackItem = player.inventory.GetAnyInventoryItemByCondition(
            stackItem => stackItem.TemplateId == itemTemplateId && stackItem.GetAvailableStackCount() > 0);
        }
        // 창고 등 필요하면 여기 조건 추가

        if (stackItem != null)
        {
          addStackCount = Math.Min(remainingAddCount, stackItem.GetAvailableStackCount());

          // 1-1. 아이템 수량 증가
          stackItemDb = new ItemDb
          {
            ItemDbId = stackItem.ItemDbId,
            EquipSlot = slotType,
            Count = stackItem.Count + addStackCount,
          };

          // 1-2. 카운트 소모
          remainingAddCount -= addStackCount;
        }
      }

      // 2. 새로 생성
      if (remainingAddCount > 0)
      {
        if (player.inventory.IsInventoryFull())
          return false;

        newItemDb = new ItemDb
        {
          ItemDbId = GenerateItemDbId(),
          TemplateId = itemTemplateId,
          EquipSlot = slotType,
          Count = remainingAddCount,
          PlayerDbId = player.PlayerDbId,
        };
      }

      return true;
    }

    public static void ApplyAddItemDbToMemory(Player player, ItemDb newItemDb, ItemDb stackItemDb, int addStackCount)
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

    public static void ApplyWalletDelta(Player player, int addGold = 0, int addDia = 0, int addEnergy = 0, int addExp = 0)
    {
      if (player == null)
        return;

      // 1) 메모리(Player) 쪽 적용 + 클라에 Send
      player.Wallet(addGold, addDia, addEnergy, addExp, sendToClient: true);


      Push(player.PlayerDbId, () =>
      {
        using (var db = new GameDbContext())
        {
          var pdb = new PlayerDb
          {
            PlayerDbId = player.PlayerDbId,
            Gold = player.Gold,
            Diamond = player.Diamond,
            Exp = player.Exp,
            Energy = player.Energy,

          };

          db.playerDbs.Attach(pdb);
          db.Entry(pdb).Property(p => p.Gold).IsModified = true;
          db.Entry(pdb).Property(p => p.Diamond).IsModified = true;
          db.Entry(pdb).Property(p => p.Exp).IsModified = true;
          db.Entry(pdb).Property(p => p.Energy).IsModified = true;

          db.SaveChangesEx();
        }
      });
    }
    

    public static void GrantStageRewardItems(Player player, int rewardId, int exp)
    {
      if (player == null)
        return;

      // 0) EXP 먼저 메모리 적용
      if (exp > 0)
      {
        player.Exp += exp; // 또는 player.playerStatInfo.Exp 사용 중이면 거기에 맞춰서
      }

      var re = rewardId.ToString();
      // 1) 리워드 그룹 조회
      if (!DataManager.StageRewardDict.TryGetValue(re, out StageRewardDataGroup group))
        return;

      if (group.Rewards == null || group.Rewards.Count == 0)
        return;

      // 2) 골드 / 다이아 / 아이템 메모리 적용 + 아이템은 SaveItemDbChanges까지
      foreach (var r in group.Rewards)
      {
        switch (r.ERewardType)
        {
          case ERewardType.ErwardTypeGold:
            {
              player.Gold += r.Count;
              break;
            }

          case ERewardType.ErwardTypeDiamod:
            {
              player.Diamond += r.Count;
              break;
            }

          case ERewardType.ErwardTypeObject:
            {
              // 아이템 템플릿 보상
              if (r.ItemId <= 0 || r.Count <= 0)
                break;

              // 1) ItemDb 준비
              if (MakeAddItemDb(player, r.ItemId, r.Count,
                    out ItemDb newItemDb, out ItemDb stackItemDb, out int addStackCount) == false)
                break;

              // 2) 메모리(인벤토리) 반영 + 클라에 패킷 전송
              ApplyAddItemDbToMemory(player, newItemDb, stackItemDb, addStackCount);

              // 3) DB 반영 (Push 내부에서 처리)
              SaveItemDbChanges(player, newItemDb, stackItemDb);
              break;
            }

          case ERewardType.ErwardTypeObjects:
            {
              //현재 필요없음

              break;
            }

          default:
            break;
        }
      }

      // 3) 골드/다이아/EXP를 PlayerDb에 한 번에 반영
      Push(player.PlayerDbId, () =>
      {
        using (var db = new GameDbContext())
        {
          var pdb = new PlayerDb
          {
            PlayerDbId = player.PlayerDbId,
            Gold = player.Gold,
            Diamond = player.Diamond,
            Exp = player.Exp,
          };

          db.playerDbs.Attach(pdb);
          db.Entry(pdb).Property(p => p.Gold).IsModified = true;
          db.Entry(pdb).Property(p => p.Diamond).IsModified = true;
          db.Entry(pdb).Property(p => p.Exp).IsModified = true;

          db.SaveChangesEx();
        }
      });
    }


  }
}
