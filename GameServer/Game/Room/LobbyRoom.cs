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
      // 큐가 비었으면 굳이 돌리지 않아도 됨
      if (ratingBuckets.Count == 0)
        return;

      matchTickAcc += deltaTime;

      if (matchTickAcc >= MatchTryInterval)
      {
        matchTickAcc = 0f;
        CompleteMatchByRating(); // 대기 시간이 늘어난 효과를 반영하여 재평가
      }
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


    public void C_EnterSingleStage(Player player, C_EnterSingleStage pkt)
    {
      if (player == null)
        return;

      string targetStageId = pkt.StageId;
      var stageDict = DataManager.StageDataDict;

      // 1) 스테이지 존재 여부
      if (!stageDict.TryGetValue(targetStageId, out StageData targetStage))
      {
        // 없는 스테이지
        S_EnterSingleStage fail = new S_EnterSingleStage
        {
          StageId = targetStageId,
          EStageResultType = EStageResultType.SingleInvalidStage,
          Energy = player.Energy
        };
        player.Session.Send(fail);
        return;
      }

      // 2) 진행도 체크
      int clearedOrder = 0;

      // player.Stagename = 마지막으로 클리어한 스테이지 ID (예: "1-2")
      if (!string.IsNullOrEmpty(player.Stagename) &&
          stageDict.TryGetValue(player.Stagename, out StageData lastCleared))
      {
        clearedOrder = lastCleared.OrderIndex;
      }

      // 규칙:
      // - target.Order <= clearedOrder      : 재도전 허용
      // - target.Order == clearedOrder + 1  : 바로 다음 스테이지 입장 허용
      // - target.Order >= clearedOrder + 2  : 잠금
      if (targetStage.OrderIndex > clearedOrder + 1)
      {
        S_EnterSingleStage fail = new S_EnterSingleStage
        {
          StageId = targetStageId,
          EStageResultType = EStageResultType.SingleStageLocked,
          Energy = player.Energy
        };
        player.Session.Send(fail);
        return;
      }

      // 3) 에너지 체크
      if (player.Energy < targetStage.ConsumeEnergy)
      {
        S_EnterSingleStage fail = new S_EnterSingleStage
        {
          StageId = targetStageId,
          EStageResultType = EStageResultType.SingleNotEnoughEnergy,
          Energy = player.Energy
        };
        player.Session.Send(fail);
        return;
      }

      if (player.Room is LobbyRoom lobby)
      {
        // 로비 워커에서 처리
        lobby.Push(() =>
        {
          // (1) 로비에서 제거
          lobby.Remove(player.ObjectID);

          // (2) 싱글 룸 워커에게 일을 던진다
          RoomManager.Instance.SingleRoom.Push(() =>
          {     // 4) 실제 입장

            RoomManager.Instance.SingleRoom.EnterSingleStage(player, targetStageId);
          });
        });
      }
      else
      {
        // 만약 이미 다른 룸(게임중?)이면 규칙에 맞게 처리 (그냥 무시하거나 에러)
      }
    }
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



    public void HandleEquipItem(Player player, C_EquipItem packet)
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
      if (player == null) 
        return;

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

    public void HandleSelectHero(Player player, int heroDbID)
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

      if (player.playerStatInfo.Gold >= consumeGold)
      {
        player.inventory.AddInventoryCapacity(player, consumeGold);
      }
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
