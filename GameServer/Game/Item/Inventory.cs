using Azure.Identity;
using GameServer;
using GameServer.Game;
using GameServer.Utils;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Server.Game
{
  public class ToatalEquipData
  {
    public int totalAttack;
    public int totalDeffence;
    public int totalHealth;
    public int totalSkillDamage;
    public float totalCriDamage;
    public float totalSpregen;
  }

  public class Inventory
  {
    //슬롯 수//
    //public int inventoryCapacity;

    public Inventory(Player owner )
    {
      Owner = owner;
    }

    private int _inventoryCapacity;

    public int InventoryCapacity
    {
      get => _inventoryCapacity;
      set
      {
        if (_inventoryCapacity == value) 
          return;   // 변경 없으면 스킵
        _inventoryCapacity = value;                // 1) 내 값 저장

        if (Owner != null)
        {
          // 2) 외부 객체에 전달(필요 시)
          Owner.playerStatInfo.InventoryCapacity = value;
        }
      }
    }

    public Player Owner { get; set; }

    Dictionary<long, Item> AllItems = new Dictionary<long, Item>();

    public Dictionary<EItemSlotType, Item> EquippedItems { get; } = new Dictionary<EItemSlotType, Item>();
    public Dictionary<long, Item> InventoryItems { get; } = new Dictionary<long, Item>();

    public ToatalEquipData toatalEquipData = new ToatalEquipData();
    public void Init(PlayerDb playerDb)
    {
      InventoryCapacity = playerDb.InventoryCapacity;

      var list = playerDb.Items.ToList();

      for (int i = 0; i < list.Count; i++)
      {
        Item item = new Item();
        item.Init(list[i]);
        Add(item);
      }
    }

    public void Add(Item item, bool sendToClient = false)
    {
      AllItems.Add(item.ItemDbId, item);
    
      EItemStatus status = item.GetItemStatus();
      switch (status)
      {
        case EItemStatus.Equipped:
          // DB 요청이 꼬여서 중복 장착 되었다면 인벤토리로 보낸다.
          if (!EquippedItems.TryAdd(item.ItemSlotType, item))
          {
            item.ItemSlotType = EItemSlotType.Inventory;
            InventoryItems.Add(item.ItemDbId, item);
          }
          break;
    
        case EItemStatus.Inventory:
          InventoryItems.Add(item.ItemDbId, item);
          break;
      }
    
      if (sendToClient)
        item.SendAddPacket(Owner);
    }

  

    public void Remove(Item item, bool sendToClient = false)
    {
      AllItems.Remove(item.ItemDbId);

      EItemStatus status = item.GetItemStatus();
      switch (status)
      {
        case EItemStatus.Equipped:
          EquippedItems.Remove(item.ItemSlotType);
          break;
        case EItemStatus.Inventory:
          InventoryItems.Remove(item.ItemDbId);
          break;
      }

      if (sendToClient)
        item.SendDeletePacket(Owner);
    }
    public Item GetItemByDbId(long ItemDbId)
    {
      Item item = null;
      AllItems.TryGetValue(ItemDbId, out item);
      return item;
    }


    public Item GetInventoryItemByDbId(long itemDbId)
    {
      InventoryItems.TryGetValue(itemDbId, out Item item);
      return item;
    }
    public ToatalEquipData GetTotalDataEquipItems()
    {
      toatalEquipData.totalAttack = EquippedItems.Values.Sum(e => e.itemData.Attack);
      toatalEquipData.totalDeffence = EquippedItems.Values.Sum(e => e.itemData.Def);
      toatalEquipData.totalCriDamage = EquippedItems.Values.Sum(e => (int)e.itemData.CriDamage);
      toatalEquipData.totalHealth =  EquippedItems.Values.Sum(e => e.itemData.Health);
      toatalEquipData.totalCriDamage = EquippedItems.Values.Sum(e => e.itemData.CriRate);
      toatalEquipData.totalSkillDamage = EquippedItems.Values.Sum(e => e.itemData.Attack);

      return toatalEquipData;
    }

    public List<Item> GetItems() 
    {
      return AllItems.Values.ToList(); 
    }

    public Item Find(Func<Item, bool> condition)
    {
      foreach (Item item in AllItems.Values)
      {
        if (condition.Invoke(item))
          return item;
      }
      return null;
    }

    public bool IsInventoryFull()
    {
      return InventoryItems.Count >= InventoryCapacity;
    }
    public void AddCount(long itemDbId, int count, bool sendToClient = false, bool dbNoti = true)
    {
      Item item = GetItemByDbId(itemDbId);
      if (item == null)
        return;

      item.AddCount(Owner, count, sendToClient, dbNoti);

      //if (item.ItemSlotType == EItemSlotType.Inventory)
      //    Owner.BroadcastPlyerInternalEvent(EHeroInternalEventType.CollectItem, 0, item.TemplateId, count);
    }

    /// <summary>
    /// 특정 조건을 만족시키는 아이템 반환
    /// </summary>
    /// <param name="condition"> 특정 조건</param>
    /// <returns></returns>
    public Item GetAnyInventoryItemByCondition(Func<Item, bool> condition)
    {
      return InventoryItems.Values.Where(condition).FirstOrDefault();
    }

    public bool EquipItem(long itemDbId)
    {
      Item item = GetItemByDbId(itemDbId);
      if (item == null)
        return false;

      EItemSlotType itemSlotType = Utils.GetEquipSlotType(item.SubType);

      // 1. 같은 부위에 장착중인 템이 있는 경우, 그 아이템 장착 해제.
      if (EquippedItems.TryGetValue(itemSlotType, out Item prev))
      {
        if (prev == item)
          return false;

        prev.UnEquip(this);
      }

      // 2. 아이템 장착.
      item.Equip(this);
      GetTotalDataEquipItems();
      return true;
    }

    public bool UnEquipItem(long itemDbId)
    {
      Item item = GetItemByDbId(itemDbId);
      if (item == null)
        return false;

      // 1. 아이템 장착 해제.
      item.UnEquip(this);

      return true;
    }

    public void HandleDeleteItem(long itemDbId)
    {
      Item item = GetInventoryItemByDbId(itemDbId);
      if (item == null)
        return;

      DBManager.DeleteItem(Owner, item);
    }

    public void AddInventoryCapacity(Player player,int consumeGold)
    {
      if (player == null)
        return;
     
      InventoryCapacity += Define.INVENTORY_UP_CAPACITY;
      //TODO메세지 요청 보내기 맥스 상황// 
      if (InventoryCapacity > Define.INVENTORY_MAXCAPACITY)
      {
        InventoryCapacity = Define.INVENTORY_MAXCAPACITY;
        return;
      }
      //메모리 처리//
      player.Gold -= consumeGold;

      S_InvenCapaticy s_InventoryCapacity = new S_InvenCapaticy()
      {
        InvenCapacity = player.playerStatInfo.InventoryCapacity,
        Cost = new CurrencyAmount
        {
          //확정 골드 보내줌//
          Type = ECurrencyType.Gold,
          Amount = player.playerStatInfo.Gold
        },
      };
      player.Session?.Send(s_InventoryCapacity);
      //Db 처리//
      DBManager.UpdatePlayerInventoryCapacity(player, consumeGold, InventoryCapacity);  
    }
  }
}


