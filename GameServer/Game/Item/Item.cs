using GameServer;
using GameServer.Game;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Server.Game
{
  public class Item
  {
    public ItemInfo Info { get; } = new ItemInfo();
    public ItemData itemData = new ItemData();
    public long ItemDbId
    {
      get { return Info.ItemDbId; }
      set { Info.ItemDbId = value; }
    }
    
    public int TemplateId
    {
      get { return Info.TemplateId; }
      set { Info.TemplateId = value; }
    }
    
    public EItemSlotType ItemSlotType
    {
      get { return Info.ItemSlotType; }
      set { Info.ItemSlotType = value; }
    }
    
    public int Count
    {
      get { return Info.Count; }
      set { Info.Count = value; }
    }
    
    public int EnchantCount
    {
      get { return Info.EnchantCount; }
      set { Info.EnchantCount = value; }
    }
    public EItemSubType SubType 
    {
      get { return itemData.SubType; } 
    }
    public EConsumableType ConsumableType 
    { 
      get { return itemData.ConsumableType; } 
    }

    public int maxCount { get; set; }
    public int OwnerDbId { get; set; }

    public bool Stackable { get; set; }

    public bool IsNew 
    {
      get
      {
        return Info.IsNew;
      }
      set
      {
        Info.IsNew = value;
      }
    }

    public DateTime LastAcquiredAtUtc;
    public DateTime SeenAcquiredUtc;
    public Item Init(ItemDb itemdb)
    {
      int templateId = itemdb.TemplateId;
      if (DataManager.itemDict.TryGetValue(templateId, out ItemData itemData) == false)
        return null;

      this.itemData = itemData;
      ItemDbId = itemdb.ItemDbId;
      OwnerDbId = itemdb.PlayerDbId.Value;
      ItemSlotType = itemdb.EquipSlot;
      Count = itemdb.Count;
      EnchantCount = itemdb.EnchantCount;
      TemplateId = templateId;
      LastAcquiredAtUtc = itemdb.LastAcquiredAtUtc;
      SeenAcquiredUtc = itemdb.SeenAcquiredUtc;
      IsNew = LastAcquiredAtUtc > SeenAcquiredUtc;

      //itemData setting//
      Stackable = itemData.Stacable;
      maxCount = itemData.MaxCount;

      return this;
    }
    public static EItemStatus GetItemStatus(EItemSlotType itemSlotType)
    {
      if (EItemSlotType.None < itemSlotType && itemSlotType < EItemSlotType.EquipmentMax)
        return EItemStatus.Equipped;

      if (itemSlotType == EItemSlotType.Inventory)
        return EItemStatus.Inventory;

      return EItemStatus.None;
    }

    public EItemStatus GetItemStatus() { return GetItemStatus(ItemSlotType); }
    public bool IsEquipped() { return GetItemStatus() == EItemStatus.Equipped; }
    public bool IsInInventory() { return GetItemStatus() == EItemStatus.Inventory; }

    public int GetAvailableStackCount() { return Math.Max(0, maxCount - Count); }

    //public static Dictionary<EItemSubType, EItemSlotType> SubTypeToEquipTypeMap = new Dictionary<EItemSubType, EItemSlotType>()
    //{
    //  { EItemSubType.Mainweapon,  EItemSlotType.Mainweapon },
    //  { EItemSubType.Subweapon,   EItemSlotType.Subweapon} ,
    //  { EItemSubType.Helmet,      EItemSlotType.Helmet },
    //  { EItemSubType.Chest,       EItemSlotType.Chest },
    //  { EItemSubType.Leg,         EItemSlotType.Leg },
    //  { EItemSubType.Shoes,       EItemSlotType.Shoes },
    //  { EItemSubType.Gloves,      EItemSlotType.Gloves },
    //  { EItemSubType.Shoulder,    EItemSlotType.Shoulder },
    //  { EItemSubType.Ring,        EItemSlotType.Ring },
    //  { EItemSubType.Amulet,      EItemSlotType.Amulet },
    //};
    //
    //
    //public EItemSlotType GetEquipSlotType()
    //{
    //  return GetEquipSlotType(SubType);
    //}
    //public static EItemSlotType GetEquipSlotType(EItemSubType subType)
    //{
    //  if (SubTypeToEquipTypeMap.TryGetValue(subType, out EItemSlotType value))
    //    return value;
    //
    //  return EItemSlotType.None;
    //}


    public void AddCount(Player owner, int addCount, bool sendToClient = false, bool dbNoti = true)
    {
      if (owner == null)
        return;

      if (addCount == 0)
        return;

      Count = Math.Clamp(Count + addCount, 0, maxCount);

      if (sendToClient)
        SendAddPacket(owner);

      if (Count == 0)
      {
        DBManager.DeleteItemNoti(owner, this, dbNoti);
      }
    }

    public void UseItem(Player owner)
    {
      if (owner == null)
        return;
      if (Count > 0)
        Count -= 1;

      if (Count == 0)
      {
        DBManager.DeleteItemNoti(owner, this, true);
        return;
      }
    }



    public void Equip(Inventory inventory)
    {
      if (IsInInventory() == false)
        return;

      Player owner = inventory.Owner;
      if (owner == null)
        return;

      EItemSlotType equipSlotType = GetEquipSlotType();

      // 0. 같은 부위에 이미 장착 아이템이 있다면 일단 벗는다.
      if (inventory.EquippedItems.TryGetValue(equipSlotType, out Item prev))
      {
        if (prev == this)
          return;

        prev.UnEquip(inventory);
      }

      // 1. 인벤토리에서 제거.
      inventory.InventoryItems.Remove(ItemDbId);

      // 2. 장착 아이템에 추가.
      inventory.EquippedItems[equipSlotType] = this;

      // 3. 슬롯 갱신.
      ItemSlotType = equipSlotType;

      // 4. DB에 Noti. 
      DBManager.EquipItemNoti(owner, this);

      // 5. 장착한 아이템 이펙트 적용.
      //if (EffectData != null)
      //  owner.EffectComp.ApplyEffect(EffectData, owner);

      // 6. 패킷전송.
      SendChangeItemSlotPacket(owner);
    }
    public void UnEquip(Inventory inventory)
    {
      if (IsEquipped() == false)
        return;

      Player owner = inventory.Owner;
      if (owner == null)
        return;

      EItemSlotType equipSlotType = GetEquipSlotType();
      if (equipSlotType != ItemSlotType)
        return;

      // 1. 장착 아이템에서 제거.
      inventory.EquippedItems.Remove(equipSlotType);

      // 2. 인벤토리에 추가.
      inventory.InventoryItems.Add(ItemDbId, this);

      // 3. 슬롯 갱신.
      ItemSlotType = EItemSlotType.Inventory;

      // 4. DB에 Noti.
      DBManager.EquipItemNoti(owner, this);

      // 5. 기존 아이템 이펙트 제거.
      //if (EffectData != null)
      //  owner.EffectComp.RemoveItemEffect(EffectData.TemplateId);

       //6. 패킷전송.
      SendChangeItemSlotPacket(owner);
    }

    public void ApplyMemoryItemSeen(Player player)
    {
      if (player == null)
        return;

      // 지금 (획득시각 > 본 시각)
      bool isNewNow = LastAcquiredAtUtc > SeenAcquiredUtc;
      if (!isNewNow)
        return; // 이미 본 상태면 아무 것도 안 함

      // 메모리 선적용
      SeenAcquiredUtc = LastAcquiredAtUtc;
      IsNew = false; // 로컬 UI 즉시 OFF

      SendApplyNewSeen(player);
      DBManager.ItemSeenNoti(player, this);
    }

    public EItemSlotType GetEquipSlotType()
    {
      return Utils.GetEquipSlotType(SubType);
    }


    public static ItemInfo MakeItemInfo(ItemDb itemdb)
    {
      int templateId = itemdb.TemplateId;
      if (DataManager.itemDict.TryGetValue(templateId, out ItemData itemData) == false)
        return null;

      ItemInfo itemInfo = new ItemInfo()
      {
        ItemDbId = itemdb.ItemDbId,
        TemplateId = itemdb.TemplateId,
        ItemSlotType = itemdb.EquipSlot,
        EnchantCount = itemdb.EnchantCount,
        Count = itemdb.Count,
        IsNew =  itemdb.LastAcquiredAtUtc > itemdb.SeenAcquiredUtc,
      };

      return itemInfo;
    }

    public static Item MakeItem(ItemDb itemDb)
    {
      int templateId = itemDb.TemplateId;
      if (DataManager.itemDict.TryGetValue(templateId, out ItemData itemData) == false)
        return null;

      Item item = new Item();

      item.Init(itemDb);

      return item;
    }
    public void SendApplyNewSeen(Player owner)
    {
      S_ItemClick packet = new S_ItemClick();
      packet.ItemDbId = this.ItemDbId;
      owner.Session?.Send(packet);
    }

    public void SendUpdatePacket(Player owner)
    {
      S_AddItem packet = new S_AddItem();
      {
        ItemInfo itemInfo = new ItemInfo();
        itemInfo.MergeFrom(Info);
        packet.Item = itemInfo;
      }

      owner.Session?.Send(packet);
    }


    public void SendAddPacket(Player owner)
    {
      S_AddItem packet = new S_AddItem();
      {
        ItemInfo itemInfo = new ItemInfo();
        itemInfo.MergeFrom(Info);
        packet.Item = itemInfo;
      }
      
      owner.Session?.Send(packet);
    }

    public void SendDeletePacket(Player owner)
    {
      S_DeleteItem packet = new S_DeleteItem();
      packet.ItemDbId = ItemDbId;
      
      owner.Session?.Send(packet);
    }

    public void SendChangeItemSlotPacket(Player owner)
    {
      S_ChangeItemSlot packet = new S_ChangeItemSlot();
      packet.ItemDbId = ItemDbId;
      packet.ItemSlotType = ItemSlotType;
      
      owner.Session?.Send(packet);
    }
  }


}
