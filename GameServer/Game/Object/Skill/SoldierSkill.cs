using GameServer.Game.Room;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class SoldierSkill : HeroSkill
  {
    private Vector3 velocity;
    private Vector3 startPosition;
    private Vector3 targetPosition;

    private float elapsed = 0f;
    private float lifeTime = 3.0f;


    private float minFlightTime = 0.5f;
    private float maxFlightTime = 2.0f;
    private float arcFactor = 1.6f;

    public override void OnSpawned()
    {
      base.OnSpawned();
      velocity = Vector3.Zero;
      Direction = Vector3.Zero;
      elapsed = 0f;
      lifeTime = 0;
    }

    public override void Init(Hero owner, Vector3 start, Vector3 targetPosition)
    {
      base.Init(owner, start, targetPosition);

      ObjectType = EGameObjectType.Skill;
      TempleteID = Owner.TempleteID + 1;
      damage = Owner.SkillDamage;

      // 그냥 받은 start/target 그대로 사용
      startPosition = new Vector3(Owner.Position.X, start.Y, Owner.Position.Z);
      this.targetPosition = targetPosition;

      Vector3 to = targetPosition - startPosition;
      Vector3 toXZ = new Vector3(to.X, 0f, to.Z);
      float horizontalDistance = toXZ.Length();

      if (horizontalDistance < 0.01f)
        horizontalDistance = 0.01f;

      // 기본 T
      float baseT = horizontalDistance / Speed;

      // 포물선 과장 (멀어도 더 오래 날게)
      float T = baseT * arcFactor;

      // 너무 짧거나 길면 클램프
      if (T < minFlightTime) T = minFlightTime;
      if (T > maxFlightTime) T = maxFlightTime;

      lifeTime = T;
      CalculateVelocityPoint(startPosition, this.targetPosition, lifeTime);
      Position = startPosition;
    }
    /// <summary>
    /// 등가속도 운동 공식
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="time"></param>
    private void CalculateVelocityPoint(Vector3 start, Vector3 end, float time)
    {
      if (time <= 0.0001f)
      {
        velocity = Vector3.Zero;
        return;
      }
      // 중력은 예: -9.81f
      float g = gravity;
      Vector3 to = end - start;
      // 수평(XZ)
      Vector3 toXZ = new Vector3(to.X, 0f, to.Z);
      Vector3 vXZ = toXZ / time;
      /* 수직(Y)
      // endY = startY + vY * T + 0.5 * g * T^2
      // ednY -startY = vY * T + 0.5 * g * T^2  //( ednY -startY  = toY))
      // (toY) = ( (vY * time) + (0.5f * g * time * time) )
      // toY - (0.5f * g * time * time) ) = (vY * time)
      // vY = toY - (0.5f * g * time * time) / time
      */
      float vY = (to.Y - 0.5f * g * time * time) / time;

      velocity = new Vector3(vXZ.X, vY, vXZ.Z);
    }

    private void CalculateVelocity_FromHeight(Vector3 start, Vector3 end, float yMultiplier, float flightTime)
    {
      float g = Math.Abs(gravity);

      float baseVy = g * (flightTime / 2f);
      float adjustedVy = baseVy * yMultiplier;

      float tUp = adjustedVy / g;
      float hMax = start.Y + (adjustedVy * adjustedVy) / (2f * g);
      float hDown = hMax - end.Y;
      float tDown = MathF.Sqrt(2f * hDown / g);
      float totalTime = tUp + tDown;

      Vector3 planar = new Vector3(end.X - start.X, 0f, end.Z - start.Z);
      float horizontalDistance = planar.Length();
      Vector3 horizontalDir = Vector3.Normalize(planar);
      float horizontalSpeed = horizontalDistance / totalTime;

      velocity = new Vector3(horizontalDir.X * horizontalSpeed, adjustedVy, horizontalDir.Z * horizontalSpeed);

    }

    public override void FixedUpdate(float deltaTime)
    {
      base.FixedUpdate(deltaTime);

      elapsed += deltaTime;

      if (elapsed >= lifeTime)
      {
        // 남은 오차 없이 딱 목표 지점에 스냅
        Position = targetPosition;
        Explode();
        return;
      }

      // 중력 적용
      velocity.Y += gravity * deltaTime;
      Vector3 delta = velocity * deltaTime;
      Vector3 nextPos = Position + delta;

      if (velocity.LengthSquared() > 0.0001f)
        Direction = Vector3.Normalize(velocity);

      // 장애물 충돌만 체크 (Y<=0 때문에 목표 전에 터져버리면 끄고 보면서 조정)
      if (DataManager.ObstacleGrid.IsBlocked(nextPos))
      {
        Position = nextPos;
        Explode();
        return;
      }

      Position = nextPos;
      //CheckCollision();
    }

    private void CheckCollision()
    {
      GameRoom room = Room as GameRoom;

      float radiusSq = 3 * 0.3f;

      foreach (var obj in room.creatures.Values)
      {
        if (obj == null || obj.ObjectID == Owner.ObjectID)
          continue;

        float distSq = (obj.Position - Position).LengthSquared();
        if (distSq <= radiusSq)
        {
          //Vector3 dir = Vector3.Normalize(obj.Position - Position);
          //
          ////obj.OnDamageKnockback(damage, Owner, 2.5f);
          //obj.OnDamageKnockback(damage, dir , 2.5f , Owner);
          ////obj.OnDamageKnockback(damage, Owner);
          Explode();
          break;
        }
      }
    }

    private void Explode()
    {
      if (exploded)
        return;
      exploded = true;

      // 보통 여기서 IsAlive = false; 가 맞을 듯
      IsAlive = false;

      if (Owner == null || Room == null)
        return;

      GameRoom room = Room as GameRoom;
      if (room == null)
        return;

      float range = 5.0f;
      //if (heroSkillData != null && heroSkillData.Range > 0)
      //  range = heroSkillData.Range;

      float rangeSq = range * range;

      foreach (var obj in room.creatures.Values)
      {
        if (obj == null || obj.ObjectID == Owner.ObjectID)
          continue;

        float distSq = (obj.Position - Position).LengthSquared();
        if (distSq <= rangeSq)
        {
          Vector3 dir = Vector3.Normalize(obj.Position - Position);
          obj.OnDamageKnockback(damage, dir, 3.5f, Owner);
        }
      }

      room.Despawn(this);
    }


  }
}
