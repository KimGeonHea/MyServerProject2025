using Server;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  //단일 쓰레드 방식//
  //메인 쓰레드로만 동작
  //현재 ec2 제일 저렴한 버전을 써야해서//..
  public class RoomManager : Singleton<RoomManager>
  {
    readonly object _roomsLock = new();

    //public static RoomManager Instance { get; } = new RoomManager();

    public LobbyRoom LobbyRoom = LobbyRoom.Instance;

    readonly Dictionary<int, GameRoom> _rooms = new();
    readonly GameRoomPool _roomPool = new();

    RoomWorker lobbyWorker;
    readonly List<RoomWorker> gameWorker = new();

    int _roomId = 1;
    public const int RoomsPerWorker = 200; // 스레드당 게임룸 200개 목표

    // 스케줄러 기동
    public void StartSchedulers(int gameWorkerCount, int lobbyHz = 30, int gameHz = 50)
    {
      // 로비 전용 워커 1개
      lobbyWorker = new RoomWorker(lobbyHz, "LobbyWorker");
      lobbyWorker.Add(LobbyRoom);

      // 게임룸 워커 N개
      gameWorker.Clear();
      for (int i = 0; i < gameWorkerCount; i++)
        gameWorker.Add(new RoomWorker(gameHz, $"GameWorker_{i}"));
    }

    RoomWorker PickGameWorker()
    {
      // 1) 200개 미만인 워커 중 가장 적은 곳
      var ok = gameWorker
        .OrderBy(w => w.RoomCount)
        .FirstOrDefault(w => w.RoomCount < RoomsPerWorker);

      if (ok != null) return ok;

      // 2) 전부 가득이면 일단 가장 적은 곳(임시) — 필요하면 동적 증설 로직 추가
      return gameWorker.OrderBy(w => w.RoomCount).First();
    }

    // 싱글스레드 루프 기반 Update는 더이상 사용 안 함 (워커가 돌림)
    // public void Update(float dt) { ... } // 제거 또는 비워두기

    public void Create1vs1Room(Player p1, Player p2)
    {
      if (p1?.Room != null || p2?.Room != null) return;

      GameRoom room = _roomPool.Rent();

      lock (_roomsLock)
      {
        room.GameRoomId = _roomId++;
        _rooms.Add(room.GameRoomId, room);
      }

      var worker = PickGameWorker();
      worker.Add(room);                       //방을 워커에 연결

      room.Push(room.Init, 1);                //이후부터는 해당 워커에서 실행
      room.Push(room.EnterGame, p1);
      room.Push(room.EnterGame, p2);
    }

    public bool Remove(int roomId)
    {
      GameRoom room;
      lock (_roomsLock)
      {
        if (!_rooms.TryGetValue(roomId, out room))
          return false;
      }

      // 방 정리는 방 워커에서
      room.Push(() =>
      {
        // 남은 유저 정리
        foreach (var p in room.players.Values.ToList())
        {
          room.LeaveGame(p);            // 통지(S_LeaveGame)
          room.Remove(p.ObjectID);      // 링크 해제 + players.Remove
        }

        // 워커에서 떼기
        room.Worker?.Remove(room);
        room.Worker = null;

        // 매니저 딕셔너리 제거
        lock (_roomsLock) _rooms.Remove(roomId);

        // 풀 반납
        _roomPool.Return(room);
      });

      return true; // “삭제 예약” 성공
    }
    public GameRoom Find(int roomId)
    {
      lock (_roomsLock)
        return _rooms.TryGetValue(roomId, out var r) ? r : null;
    }

    public List<GameRoom> GetRooms()
    {
      lock (_roomsLock)
        return _rooms.Values.ToList();
    }


    //public static RoomManager Instance { get; } = new RoomManager();
    //
    //public LobbyRoom LobbyRoom = LobbyRoom.Instance;
    //
    //Dictionary<int, GameRoom> _rooms = new Dictionary<int, GameRoom>();
    //GameRoomPool _roomPool = new GameRoomPool();
    //
    //
    //int _roomId = 1;
    //
    //public void Update(float deltaTime)
    //{
    //  Instance.Flush();
    //  LobbyRoom.Update(deltaTime);
    //
    //  foreach (GameRoom room in _rooms.Values)
    //  {
    //    room.Update(deltaTime);  // GameRoom도 deltaTime 받음
    //  }
    //  foreach (GameRoom room in _rooms.Values)
    //  {
    //    room.FixedUpdate(deltaTime);  // GameRoom도 deltaTime 받음
    //  }
    //
    //}
    //
    //public void Create1vs1Room(Player p1, Player p2)
    //{
    //
    //  if (p1.Room != null || p2.Room != null)
    //    return;
    //
    //  GameRoom room = _roomPool.Rent();
    //  room.GameRoomId = _roomId++;
    //  _rooms.Add(room.GameRoomId, room);
    //
    //  room.Push(room.Init, 1); // 원하는 맵 ID
    //
    //  room.Push(room.EnterGame, p1);
    //  room.Push(room.EnterGame, p2);
    //
    //  p1.Room = room;
    //  p2.Room = room;
    //}
    //
    //public GameRoom Add(int mapTemplateId)
    //{
    //  GameRoom gameRoom = new GameRoom();
    //  gameRoom.Push(gameRoom.Init, mapTemplateId);
    //
    //  gameRoom.GameRoomId = _roomId;
    //  _rooms.Add(_roomId, gameRoom);
    //  _roomId++;
    //
    //  return gameRoom;
    //}
    //
    //public bool Remove(int roomId)
    //{
    //  return _rooms.Remove(roomId);
    //}
    //
    //public GameRoom Find(int roomId)
    //{
    //  GameRoom room = null;
    //  if (_rooms.TryGetValue(roomId, out room))
    //    return room;
    //
    //  return null;
    //}
    //
    //public List<GameRoom> GetRooms()
    //{
    //  return _rooms.Values.ToList();
    //}

  }
}
