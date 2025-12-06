using Google.Protobuf;
using Server;
using Server.Game;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server.Data;
using Google.Protobuf.Protocol;
using GameServer;
using GameServer.Game.Room;
using GameServer.Game;

class PacketHandler
{
    ///////////////////////////////////// Client - Game Server /////////////////////////////////////
    public static void C_TestHandler(PacketSession session, IMessage packet)
    {
        C_Test pkt = packet as C_Test;
        System.Console.WriteLine(pkt.Temp);
    }
  

  public static void C_LoginResHandler(PacketSession session, IMessage packet)  
  {
    C_LoginRes pkt = packet as C_LoginRes;
    ClientSession clientSession = session as ClientSession;
    clientSession.HandleLogin(pkt);
  }

  public static void C_PingHandler(PacketSession session, IMessage packet)
  {
    C_Ping pkt = packet as C_Ping;
    ClientSession clientSession = session as ClientSession;
    //clientSession.HandleLogin(pkt);
  }
  
  public static void C_EnterGameHandler(PacketSession session, IMessage packet)
  {
    C_EnterGame pkt = packet as C_EnterGame;
    ClientSession clientSession = session as ClientSession;
    clientSession.HandleEnter(pkt);
  }

  public static void C_EnterQeueHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_EnterQeue;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    room.Push(room.EnqueuePlayer, player);

  }
  public static void C_LeaveGameHandler(PacketSession session, IMessage packet)
  {
    var clientSession = session as ClientSession;
    var p = clientSession.player;
    var r = p?.Room;
    if (r?.Worker == null) return;            // 내려간 방이면 무시

    r.Push(() =>
    {
      if (p.Room != r) return;                 // 도중에 방 이동했으면 무시

      switch (r)
      {
        case GameRoom gr:
          // 1) (연결 살아있을 때만 의미) 클라에 알림
          p.Session?.Send(new S_LeaveGame { /* Reason/NextState 등 필요시 */ });

          // 2) 서버 권위로 제거
          gr.Remove(p.ObjectID);

          // 3) 로비로 보내기 방에 Push (워커 X)
          var lobby = RoomManager.Instance.LobbyRoom;
          lobby?.Push(lobby.EnterGame, p);

          // 4) 방 비었으면 정리
          if (gr.players.Count == 0)
            RoomManager.Instance.Remove(gr.GameRoomId);
          break;

        case LobbyRoom lb:
          // 로비 대기 취소도  방에 Push
          lb.Push(lb.CancelMatch, p);

          // 정책상 로비 자체도 떠나게 하려면:
          lb.Push(lb.Remove, p.ObjectID);
          break;
        case SingleGameRoom single:
          //  싱글 LeaveGame 호출
          single.LeaveGame(p);
          break;
      }
    });
  }
  public static void C_ItemClickHandler(PacketSession session, IMessage packet)
  {
    C_ItemClick pkt = packet as C_ItemClick;
    ClientSession clientSession = session as ClientSession;

    Player player = clientSession.player;
    if (player == null)
      return;


    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null) return;

    room.Push(room.HanldeItemClick, player, pkt);
  }


  
  public static void C_EquipItemHandler(PacketSession session, IMessage packet)
  {
    C_EquipItem pkt = packet as C_EquipItem;
    ClientSession clientSession = session as ClientSession;

    Player player = clientSession.player;
    if (player == null)
      return;

    //LobbyRoom room = player.Room as LobbyRoom;
    //
    //if (room == null) return;
    //
    //LobbyJobManager.Push(player.PlayerDbId, () =>
    //{
    //  room.HanldeEquipItem(player, pkt);
    //});

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null) return;

    room.Push(room.HandleEquipItem, player,pkt);
  }

  public static void C_UnEquipItemHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_UnEquipItem;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;
    
    room.Push(room.HandleUnEquipItem, player, pkt.ItemDbId);
  }

  public static void C_UseItemHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_UseItem;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    room.Push(room.HandleUseItem, player, pkt);
  }

  public static void C_EnterSingleStageHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_EnterSingleStage;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    room.Push(room.C_EnterSingleStage, player, pkt);
  }

  public static void C_ReportStageHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_ReportStage;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    SingleGameRoom room = player.Room as SingleGameRoom;
    if (room == null)
      return;

    room.Push(room.ReportStageEnd, player, pkt.StageId,pkt.IsClear);
  }

  public static void C_StagePauseHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_StagePause;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    SingleGameRoom room = player.Room as SingleGameRoom;
    if (room == null)
      return;

    room.Push(room.ToggleStagePause, player);
  }

  public static void C_StageReviveHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_StageRevive;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    SingleGameRoom room = player.Room as SingleGameRoom;
    if (room == null)
      return;

    room.Push(room.RevivePlayer, player);
  }
  

  public static void C_ChatHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_Chat;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;
    
    room.Push(room.HandleChat, player, pkt);
  }
  
  public static void C_ChatListHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_ChatList;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;
    
    room.Push(room.HandleChatList, player);
  }
  

  public static void C_InvenCapaticyHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_InvenCapaticy;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    room.Push(room.HandleAddInventoryCapacity, player);
  }
  public static void C_ItemListHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_ItemList;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;
    
    room.Push(room.HandleItemList, player);
  }
  public static void C_DeleteItemHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_DeleteItem;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    //player.inventory.HandleDeleteItem(pkt.ItemDbId);
  }

  public static void C_HeroListHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_HeroList;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;


    //LobbyRoom room = player.Room as LobbyRoom;
    //
    //if (room == null) return;
    //
    //LobbyJobManager.Push(player.PlayerDbId, () =>
    //{
    //  room.HandleHeroList(player);
    //});


    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;
    
    room.Push(room.HandleHeroList, player);
  }

  public static void C_SelectHeroHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_SelectHero;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    //LobbyRoom room = player.Room as LobbyRoom;
    //
    //if (room == null) return;
    //
    //LobbyJobManager.Push(player.PlayerDbId, () =>
    //{
    //  room.HandleSelctHero(player, pkt.HeroDbId);
    //});

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;
    
    room.Push(room.HandleSelectHero, player , pkt.HeroDbId);
  }

  public static void C_DailyRewardHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_DailyReward;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    //LobbyRoom room = player.Room as LobbyRoom;
    //
    //if (room == null) return;
    //
    //LobbyJobManager.Push(player.PlayerDbId, () =>
    //{
    //  room.CheckDailyReward(player);
    //});

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    room.Push(room.HandleCheckDailyReward, player);
  }
  public static void C_DailyRewardOpenHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_DailyReward;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    room.Push(room.RewardOpenToClinetRewardListSend, player);
  }

  public static void C_RewardItemHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_RewardItem;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    room.Push(room.RewardTest, player);
    //LobbyJobManager.Push(player.PlayerDbId, () =>
    //{
    //  room.RewardTest(player);
    //});
  }
  public static void C_EnterMultyGameHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_EnterMultyGame;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    room.Push(room.EnqueuePlayer, player);

  }
  public static void C_MatchCancelHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_MatchCancel;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;

    room.Push(room.CancelMatch, player);
  }

  

  public static void C_HeroMoveHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_HeroMove;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    GameRoom room = player.Room as GameRoom;
    if (room == null)
      return;

    room.Push(room.MoveHero, player , pkt);
  }

  public static void C_HeroShotHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_HeroShot;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    GameRoom room = player.Room as GameRoom;
    if (room == null)
      return;

    room.Push(room.ShotHero, player, pkt);
  }


  public static void C_HeroSkillHandler(PacketSession session, IMessage packet)
  {
    var pkt = packet as C_HeroSkill;
    if (pkt == null)
      return;

    var clientSession = session as ClientSession;
    if (clientSession == null)
      return;

    Player player = clientSession.player;
    if (player == null)
      return;

    GameRoom room = player.Room as GameRoom;
    if (room == null)
      return;

    room.Push(room.SkillHero, player, pkt);
  }

}
