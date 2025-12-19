using GameServer.Game.Room;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class HoodieSkill : HeroSkill
  {

    private Vector3 velocity;
    private Vector3 startPosition;
    private Vector3 targetPosition;

    private int bounceCount = 0;
    private int maxBounces = 2;
    private float bounceFactor = 0.6f;

    private float elapsed = 0f;
    private float lifeTime = 3f;
    public override void OnSpawned()
    {
      base.OnSpawned();
      velocity = Vector3.Zero;
      Direction = Vector3.Zero;
      elapsed = 0f;
      bounceCount = 0;
    }
    public override void OnDespawned()
    {
      base.OnDespawned();

    }

    public override void Init(Hero owner, Vector3 direction, Vector3 targetPos)
    {
      base.Init(owner, direction, targetPos);

      ObjectType = EGameObjectType.Skill;
      TempleteID = owner.TempleteID + 1;
      damage = owner.SkillDamage;

      // 시작 / 타겟 위치 (클라랑 비슷하게)
      startPosition = new Vector3(owner.Position.X, 0.5f, owner.Position.Z);
      targetPosition = new Vector3(targetPos.X, 0.5f, targetPos.Z);

      Position = startPosition;

      //  클라랑 동일하게 고정 값 사용
      float flightTime = 0.8f;   // 클라 CalculateVelocity(..., 0.8f)
      float yMultiplier = 1.2f;   // 클라 CalculateVelocity(..., 1.2f, ...)

      CalculateVelocity(startPosition, targetPosition, yMultiplier, flightTime);

      lifeTime = 5f;
    }



    public void CalculateVelocity(Vector3 start, Vector3 end, float yMultiplier , float flightTime)
    {
      Vector3 to = new Vector3(end.X - start.X, 0f, end.Z - start.Z);
      float dist = to.Length();

      // 제자리 발사 방지
      if (dist < 0.001f)
      {
        velocity = Vector3.Zero;
        Direction = Vector3.UnitZ;
        lifeTime = 0.5f;
        return;
      }

      Vector3 dir = Vector3.Normalize(to);

      // 2) 비행 시간 T = 거리 / 수평속도 (수평은 등속도라서)
      float T = dist / heroSkillData.Speed;
      if (T < 0.1f) 
        T = 0.1f; // 너무 짧은 T 방지용

      // 3) 등가속도 공식으로 vY 역산
      // dy = endY - startY
      float dy = end.Y - start.Y;
      float g = gravity; // 여기서는 음수(-9.81f 같은 값) 사용

      // dy = vY * T + 0.5 * g * T^2    vY = (dy - 0.5*g*T^2) / T
      float vY = (dy - 0.5f * g * T * T) / T;

      // 4) 최종 속도 벡터
      velocity = new Vector3(dir.X * heroSkillData.Speed, vY, dir.Z * heroSkillData.Speed);
      Direction = dir;

      // 5) lifeTime은 비행시간 + 약간의 여유
      lifeTime = T + 0.5f;
    }

    public override void FixedUpdate(float deltaTime)
    {
      base.FixedUpdate(deltaTime);

      elapsed += deltaTime;
      if (elapsed > lifeTime)
      {
        Explode();
        return;
      }

      velocity.Y += gravity * deltaTime;
      Vector3 delta = velocity * deltaTime;
      Vector3 nextPos = Position + delta;

      // 장애물 충돌 (XZ 기준)
      if (DataManager.ObstacleGrid.IsBlocked(new Vector3(nextPos.X, nextPos.Y, nextPos.Z)))
      {
        Explode();
        return;
      }

      // 바닥 충돌
      if (nextPos.Y <= 0.0f)
      {
        if (bounceCount < maxBounces)
        {
          bounceCount++;
          velocity.Y *= -bounceFactor;
          Position = new Vector3(nextPos.X, 0.01f, nextPos.Z); // 바닥 위로 살짝
        }
        else
        {
          Explode();
          return;
        }
      }
      else
      {
        Position = nextPos;
      }

      CheckCollision();
    }
    private void CheckCollision()
    {
      GameRoom room = Room as GameRoom;

      float radiusSq = heroSkillData.Radius * 2f;

      foreach (var obj in room.heroes.Values)
      {
        if (obj == null || obj.ObjectID == Owner.ObjectID)
          continue;

        float distSq = (obj.Position - Position).LengthSquared();
        if (distSq <= radiusSq)
        {
          //obj.OnDamaged(damage, Owner);
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

      // 풀에 돌아간 이후거나, 이미 방에서 제거된 경우 방어
      if (Owner == null || Room == null)
        return;

      GameRoom room = Room as GameRoom;
      if (room == null)
        return;

      // heroSkillData 가 예상과 다르게 null 이어도 죽지 않게 기본값 사용
      float range = 3.0f;
      if (heroSkillData != null && heroSkillData.Range > 0)
        range = heroSkillData.Range;


      foreach (var obj in room.heroes.Values)
      {
        if (obj == null || obj.ObjectID == Owner.ObjectID)
          continue;

        float distSq = (obj.Position - Position).LengthSquared();
        if (distSq < range)
          obj.OnDamageBasic(damage, Owner);
      }

      room.Despawn(this);
    }
  }
}
