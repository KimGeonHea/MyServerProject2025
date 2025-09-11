using GameServer;
using GameServer.Game;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
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

      if (item.SeenAcquiredUtc >= item.LastAcquiredAtUtc) 
        return; // 이미 본 상태면 스킵

      // 이번 획득분까지 본 것으로 마킹
      var itemdb = new ItemDb
      {
        ItemDbId = item.ItemDbId,
        SeenAcquiredUtc = item.LastAcquiredAtUtc,
        LastAcquiredAtUtc = DateTime.UtcNow
      };

      Push(player.PlayerDbId, () =>
      {
        using (var db = new GameDbContext())
        {
          db.Entry(itemdb).State = EntityState.Unchanged;
          db.Entry(itemdb).Property(nameof(ItemDb.SeenAcquiredUtc)).IsModified = true;
          db.Entry(itemdb).Property(nameof(ItemDb.LastAcquiredAtUtc)).IsModified = true;

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


  
  }
}
