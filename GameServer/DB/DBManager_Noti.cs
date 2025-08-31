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

    //public static void EquipHeroNoti(Player player, Hero hero)
    //{
    //  if (player == null || hero == null)
    //    return;
    //
    //  HeroDb heroDb = new HeroDb()
    //  {
    //    HeroDbId = hero.HeroDbId,
    //    Slot = hero.Slot
    //  };
    //
    //  Push(player.PlayerDbId, () =>
    //  {
    //    using (GameDbContext db = new GameDbContext())
    //    {
    //      db.Entry(heroDb).State = EntityState.Unchanged;
    //      db.Entry(heroDb).Property(nameof(HeroDb.Slot)).IsModified = true;
    //
    //      bool success = db.SaveChangesEx();
    //      if (!success)
    //      {
    //
    //      }
    //    }
    //  });
    //}
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
