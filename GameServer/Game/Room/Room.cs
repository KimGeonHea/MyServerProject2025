using GameServer.Game.Object.Creature;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GameServer.Game.Room
{
  public interface IDamageRoom
  {
    void BroadcastDamage(BaseObject target, DamageContext ctx, int finalDamage);

    void OnCreatureDead(BaseObject obj,DamageContext ctx ,int curHp);
  }

  public class Room : JobSerializer
  {
    public int GameRoomId { get; set; }
    public int MapTemplateId { get; set; }
    public RoomWorker Worker { get; set; } 

    public Dictionary<int, Player> players = new Dictionary<int, Player>();
    public Dictionary<int, BaseObject> baseObjects = new Dictionary<int, BaseObject>();

    protected int playerCount = 0; // 플레이어 ObjectID 발급용
    protected int objectCount = 0; // 일반 오브젝트 ObjectID 발급용

    // 추가: 방 활성 여부(내려가는 중 Push 방지)
    public bool IsActive { get; private set; } = true;
    
    // 편의: 스케줄러에 매달려 있고 활성 상태인가?

    public bool IsAlive => (Worker != null) && IsActive;

    public virtual void Update(float deltaTime)
    {
      // 스케줄러 루프에서 주기 호출  Push된 작업 실행
      Flush();
    }

    public void Init(int mapTemplateId)
    {
      MapTemplateId = mapTemplateId;
      IsActive = true; // 풀에서 돌아왔을 수도 있으니 명시적으로 on
    }

    public virtual void EnterGame(Player player)
    {
      if (player == null || !IsActive)
        return;

      player.ObjectID = playerCount;
      if (players.ContainsKey(player.ObjectID))
        return;

      playerCount++;
      player.Room = this;
      players.Add(player.ObjectID, player);
    }

    public virtual void EnterGame(BaseObject baseObject)
    {
      if (baseObject == null || !IsActive)
        return;

      baseObject.ObjectID = objectCount;
      if (baseObjects.ContainsKey(baseObject.ObjectID))
        return;

      objectCount++;
      baseObject.Room = this;
      baseObjects.Add(baseObject.ObjectID, baseObject);
    }


    // 실제 오브젝트 제거(+링크 해제) — 공통 마무리
    public virtual void Despawn(BaseObject obj)
    {
      if (obj == null)
        return;

      // 공통 컨테이너에서 제거
      if (baseObjects.Remove(obj.ObjectID))
        obj.Room = null;

    }

    // 최소 프로토콜 헬퍼 2개 (본인 통지 / 모두 제거)
    protected void NotifyLeave(Player me, ELeaveReason reason, bool goLobby = true)
    {
      if (me?.Session == null) return;

      var pkt = new S_LeaveGame
      {
        RoomID = GameRoomId,
        LeaveReason = reason,
        GoLobby = goLobby
      };
      me.Session.Send(pkt);
    }


    public virtual void LeaveGame(Player player)
    {
      if (player == null) 
        return;
      // 1) 나간다고 통보
      NotifyLeave(player, ELeaveReason.Voluntary, goLobby: false);

      // 2) 이 Room에서 제거
      Remove(player.ObjectID);
    }



    // 실제 플레이어 제거(+링크 해제) — TryGetValue로 안전 처리
    public virtual void Remove(int objectId)
    {
      if (!players.TryGetValue(objectId, out Player player))
        return;

      if (player == null)
      {
        Console.WriteLine("player null, don't remove player");
        return;
      }

      player.Room = null;
      players.Remove(objectId);
    }


    public virtual void Broadcast(IMessage packet)
    {
      if (packet == null) return;
      foreach (Player p in players.Values)
        p.Session?.Send(packet);
    }

    public virtual void BroadcastExcept(int exceptObjectId, IMessage packet)
    {
      if (packet == null) return;
      foreach (var kv in players)
      {
        if (kv.Key == exceptObjectId) continue;
        kv.Value.Session?.Send(packet);
      }
    }

    public virtual void Unicast(int objectId, IMessage packet)
    {
      if (packet == null) return;
      if (players.TryGetValue(objectId, out var p))
        p.Session?.Send(packet);
    }

    // 빈방/서버 내려갈 때 호출(남은 유저 통지 + 비움)
    public virtual void Close()
    {
      if (!IsActive) return;
      IsActive = false;

      foreach (var p in players.Values)
      {
        p.Session?.Send(new S_LeaveGame());
        p.Room = null;
      }
      players.Clear();

      foreach (var obj in baseObjects.Values)
        obj.Room = null;
      baseObjects.Clear();
    }

    // 풀 반납용 완전 초기화
    public virtual void ResetForPool()
    {
      GameRoomId = 0;
      MapTemplateId = 0;
      Worker = null;
      IsActive = false;

      players.Clear();
      baseObjects.Clear();

      playerCount = 0;
      objectCount = 0;
    }

    // 외부 스레드에서 쓸 때 편한 가드(살아있을 때만 Push)
    public void PushIfAlive(Action job)
    {
      if (IsAlive && job != null)
        Push(job);
    }
  }
}

