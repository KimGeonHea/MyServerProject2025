using GameServer.Game.Room;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class BearBullet : HeroBullet
  {

    float halfAngleDeg = 25f;
    float tickRate = 10f;        // 10Hz (0.1s) -> 0.2s면 5로
    float perTickScale = 0.3f;

    float tickInterval;
    float _accum;

    //  마지막 유효 방향 (MoveDir이 0일 때 유지용)
    Vector3 _lastForward = Vector3.UnitZ;

    public override void OnSpawned()
    {
      base.OnSpawned();
      _accum = 0f;
      tickInterval = 1.0f / MathF.Max(1f, tickRate);
      _lastForward = Vector3.UnitZ;
    }

    public override void OnDespawned()
    {
      base.OnDespawned();
      _accum = 0f;
    }

    public override void Init(Hero owner, Vector3 direction, Vector3 startPos)
    {
      base.Init(owner, direction, startPos);


    }

    public override void FixedUpdate(float deltaTime)
    {
      if (!IsAlive || Owner == null || Owner.Room == null)
        return;

      //  토글 OFF 기능을 쓰려면 HeroBullet에 GameplayEnabled를 추가해서 여기서 체크
      // if (!GameplayEnabled) return;

      // forward는 Owner.MoveDir이 아니라 "이 불꽃의 방향"인 MoveDir을 써야 함
      Vector3 forward = Direction;
      forward.Y = 0f;

      if (forward.LengthSquared() < 1e-6f)
        forward = _lastForward;
      else
        forward = Vector3.Normalize(forward);

      _lastForward = forward;

      _accum += deltaTime;
      while (_accum >= tickInterval)
      {
        _accum -= tickInterval;
        DoOneTick(forward);
      }
    }

    void DoOneTick(Vector3 forward)
    {
      if (Owner?.Room is not GameRoom room)
        return;

      float raw = Owner.AttackDamage * perTickScale;
      int damage = Math.Max(1, (int)MathF.Round(raw));
      if (damage <= 0) return;

      float rangeSq = Range * Range;
      float halfRad = halfAngleDeg * (float)Math.PI / 180f;
      float cosHalf = MathF.Cos(halfRad);

      foreach (var kv in room.creatures)
      {
        Creature c = kv.Value;
        if (c == null) 
          continue;
        if (c.ObjectID == Owner.ObjectID) 
          continue; // 자기 자신 제외

        // TODO: 팀 판정 있으면 여기서 아군 제외

        Vector3 to = c.Position - Position; //  Position은 핸들러가 총구로 갱신해줘야 함
        to.Y = 0f;

        float distSq = to.LengthSquared();
        if (distSq <= 1e-6f || distSq > rangeSq) continue;

        Vector3 dir = Vector3.Normalize(to);
        float cos = Vector3.Dot(forward, dir);
        if (cos < cosHalf) continue;

        c.OnDamageBasic(damage, Owner);
      }
    }
  }
}

