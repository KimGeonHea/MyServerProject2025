using GameServer.Utils;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.Data;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  public partial class LobbyRoom : Room
  {
    public static LobbyRoom Instance { get; } = new LobbyRoom();

    
    public ChatManager chatManager = new ChatManager();

    List<Player> waitingPlayers = new List<Player>();

    object _lock = new object();

    
    public override void Update(float deltaTime)
    {
      base.Update(deltaTime);
    }

    public override void EnterGame(Player player)
    {
      base.EnterGame(player);
      if (player == null)
        return;

      player.Session.ServerState = PlayerServerState.ServerStateLobby;


      S_EnterGame s_EnterGame = new S_EnterGame()
      {
        PlayerObjectId = player.ObjectID,
      };

      player.Session?.Send(s_EnterGame);
    }

    public override void LeaveGame(Player player)
    {
      base.LeaveGame(player);
    }

    //public override  void Remove(int objectId)
    //{
    //  Player player = null;
    //  if (players.TryGetValue(objectId, out player))
    //
    //    if (player == null)
    //    {
    //      Console.WriteLine("player null dont remove player");
    //      return;
    //    }
    //
    //  if (player != null)
    //  {
    //    player.Room = null;
    //    waitingPlayers.Remove(player);
    //    players.Remove(objectId);
    //  }
    //}


    public Player Find(int objectId)
    {
      Player player = null;
      if (players.TryGetValue(objectId, out player))
        return player;
      return null;
    }

    public void HanldeItemClick(Player player, C_ItemClick packet )
    {
      if (player == null)
        return;
      Item item = player.inventory.GetItemByDbId(packet.ItemDbId);

      if (item == null)
        return;

      item.ApplyMemoryItemSeen(player);
      //DBManager.ItemSeenNoti(player, item);
    }



    public void HanldeEquipItem(Player player, C_EquipItem packet)
    {
      if (player == null)
        return;
      Item item = player.inventory.GetItemByDbId(packet.ItemDbId);

      if (item == null)
        return;

      //db연동

      player.inventory.EquipItem(item.ItemDbId);
    }

    public void HandleUnEquipItem(Player player, long itemDbId)
    {
      if (player == null) return;

      player.inventory.UnEquipItem(itemDbId);
    }

    public void HandleChat(Player player, C_Chat c_Chat)
    {
      if (player == null) return;

      ChatMessage chat = chatManager.AddorGetChatMessage(player.PlayerDbId, player.playerStatInfo.PlayerName, c_Chat.Message);
      S_Chat chatPacket = new S_Chat()
      {
        ChatMessage = chat
      };
      Broadcast(chatPacket);
    }

    public void HandleChatList(Player player)
    {
      if (player == null) return;

      S_ChatList chatList = new S_ChatList();
      var list = chatManager.GetAllChatMessages();

      foreach (var chat in list)
      {
        ChatMessage chatMessage = new ChatMessage
        {
          PlayerDbId = chat.PlayerDbId,
          PlayerName = chat.PlayerName,
          TextMessage = chat.TextMessage,
          ChatTime = chat.ChatTime
        };

        chatList.Chatlist.Add(chatMessage);
      }

      // 클라이언트에게 전송
      player.Session?.Send(chatList);
    }

    public void HandleSelctHero(Player player, int heroDbID)
    {
      if (player == null) return;

      player.invenHero.EquipSelectHero(heroDbID);
    }

    public void HandleHeroList(Player player)
    {
      if (player == null) return;

      S_HeroList heroList = new S_HeroList();

      var list = player.invenHero.GetAllHeroInfos();

      foreach (var herostat in list)
      {
        HeroStatInfo info = new HeroStatInfo
        {
          HeroDbId = herostat.HeroDbId,
          ItemSlotType = herostat.ItemSlotType,
          TemplateId = herostat.TemplateId,
        };
        heroList.Heros.Add(info);
      }

      player.Session?.Send(heroList);
    }
    public void HandleItemList(Player player)
    {
      if (player == null) return;

      S_ItemList s_ItemList = new S_ItemList();

      var list = player.inventory.GetItems();

      foreach (var info in list)
      {
        ItemInfo iteminfo = new ItemInfo
        {
          ItemDbId = info.ItemDbId,
          ItemSlotType = info.ItemSlotType,
          TemplateId = info.TemplateId,
          Count = info.Count,
          EnchantCount = info.EnchantCount,
        };
        s_ItemList.Items.Add(iteminfo);
      }

      player.Session?.Send(s_ItemList);
    }

    public void HandleAddInventoryCapacity(Player player)
    {
      if (player == null) 
        return;
      int consumeGold = Define.INVENTORY_CAPACITY_CONSUMGOLD;

      if(player.playerStatInfo.Gold >= consumeGold)
      {
        player.playerStatInfo.Gold -= consumeGold;
        player.inventory.AddInventoryCapacity(consumeGold);
      }

      S_InvenCapaticy s_InventoryCapacity = new S_InvenCapaticy()
      {
        InvenCapacity = player.playerStatInfo.InventoryCapacity,
        Cost = new CurrencyAmount 
        {
          Type = ECurrencyType.Gold,
          Amount = consumeGold 
        },
      };
      player.Session?.Send(s_InventoryCapacity);
    }

    public override void Broadcast(IMessage packet)
    {
      base.Broadcast(packet);
      //foreach (Player p in players.Values)
      //{
      //  p.Session?.Send(packet);
      //}
    }
  }
}
