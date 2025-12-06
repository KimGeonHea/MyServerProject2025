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
using static System.Net.Mime.MediaTypeNames;

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
      MoveDir = Vector3.Zero;
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

      // 원하면 여기서 한 번 디버그 찍어봐도 좋음
      // Console.WriteLine($"[HoodieSkill.Init] start={startPosition}, target={targetPosition}, time={flightTime}");

      CalculateVelocity(startPosition, targetPosition, yMultiplier, flightTime);

      // lifeTime은 적당히 여유 있게 (클라는 5f)
      lifeTime = 5f;
    }



    public void CalculateVelocity(Vector3 start, Vector3 end, float yMultiplier , float flightTime)
    {
      float g = Math.Abs(gravity);

      // 1. 수직 거리 계산
      float baseVy = g * (flightTime / 2f);
      float adjustedVy = baseVy * yMultiplier;

      // 2. 오름 시간과 최고점 높이
      float tUp = adjustedVy / g;
      float hMax = start.Y + (adjustedVy * adjustedVy) / (2f * g);

      // 3. 내려가는 높이
      float hDown = hMax - end.Y;

      // 4. 낙하 시간
      float tDown = MathF.Sqrt(2f * hDown / g);
      float totalTime = tUp + tDown;

      // 5. 수평 이동 속도
      Vector3 planar = new Vector3(end.X - start.X, 0f, end.Z - start.Z);
      float horizontalDistance = planar.Length();
      Vector3 horizontalDir = Vector3.Normalize(planar);
      float horizontalSpeed = horizontalDistance / totalTime;

      // 6. 최종 velocity
      velocity = new Vector3(horizontalDir.X * horizontalSpeed, adjustedVy, horizontalDir.Z * horizontalSpeed);
      MoveDir = horizontalDir;
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

      if (_exploded)
        return;
      _exploded = true;

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
