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
    var s = (ClientSession)session;
    var p = s.player;
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
          // lb.Push(lb.Remove, p.ObjectID);
          // p.Session.ServerState = PlayerServerState.ServerStateLogin;
          break;
      }
    });
    //C_LeaveGame pkt = packet as C_LeaveGame;
    //ClientSession clientSession = session as ClientSession;
    //
    //Player player = clientSession.player;
    //if (player == null)
    //  return;
    //
    //GameRoom room = player.Room as GameRoom;
    //if (room == null)
    //  return;
    //
    //if (room?.Worker == null) return; // 내려간 방이면 무시
    //
    //room.Push(() =>
    //{
    //  room.LeaveGame(player);           // 1) 알림/연출/정리
    //  room.Remove(player.ObjectID);     // 2) 실제 제거
    //
    //  // 3) 게임룸이면 로비로 보내기
    //  if (room is GameRoom gr)
    //  {
    //    var lobby = RoomManager.Instance.LobbyRoom;
    //    if (lobby?.Worker != null)
    //      lobby.Push(lobby.EnterGame, player); // 또는 EnterLobby 래퍼
    //
    //    // (Remove 오버라이드에서 빈방 삭제 예약을 이미 함)
    //  }
    //  else if (room is LobbyRoom)
    //  {
    //    // 로비에서 '나가기' 정책:
    //    // - 매칭 대기열 취소만 하고 로비에는 남겨두기, 또는
    //    // - 진짜 로비 자체도 떠나게 할 거면 여기서 상태 전환 등
    //    // p.Session.ServerState = PlayerServerState.ServerStateLogin;
    //  }
    //});

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

    //LobbyRoom room = player.Room as LobbyRoom;
    //
    //if (room == null) return;
    //
    //LobbyJobManager.Push(player.PlayerDbId, () =>
    //{
    //  room.HandleUnEquipItem(player, pkt.ItemDbId);
    //});


    LobbyRoom room = player.Room as LobbyRoom;
    if (room == null)
      return;
    
    room.Push(room.HandleUnEquipItem, player, pkt.ItemDbId);
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

    room.Push(room.CheckDailyReward, player);
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
