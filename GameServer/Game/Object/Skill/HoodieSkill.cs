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

    public override void Init(Hero owner, Vector3 direction, Vector3 targetPosition)
    {
      base.Init(owner, direction, targetPosition);

      ObjectType = EGameObjectType.Projecttile;
      TempleteID = TempleteId;
      damage = owner.HeroData.AttackDamage;

      startPosition = new Vector3(owner.Position.X, 0.5f, owner.Position.Z);
      this.targetPosition = new Vector3(targetPosition.X, 0.5f, targetPosition.Z); // Y 동일

      Position = startPosition;

      float maxDistance = 8f;
      float distance = (this.targetPosition - startPosition).Length();
      float t = Math.Clamp(distance / maxDistance, 0f, 1f); // 보간 계수 (0~1)

      float minTime = 0.3f;
      float maxTime = 1.2f;

      float flightTime = minTime + (maxTime - minTime) * t; // 직접 보간
      CalculateVelocity(startPosition, this.targetPosition, 1.3f, flightTime);
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
      GameRoom room = Owner.Room as GameRoom;
      float radiusSq = heroSkillData.Radius * 0.3f;

      foreach (var obj in room.heros.Values)
      {
        if (obj == null || obj.ObjectID == Owner.ObjectID)
          continue;

        float distSq = (obj.Position - Position).LengthSquared();
        if (distSq <= radiusSq)
        {
          obj.OnDamaged(damage, Owner);
          Explode();
          break;
        }
      }
    }

    private void Explode()
    {

      GameRoom room = Owner.Room as GameRoom;
      float radiusSq = heroSkillData.Radius * heroSkillData.Radius;

      foreach (var obj in room.heros.Values)
      {
        if (obj == null || obj.ObjectID == Owner.ObjectID)
          continue;

        float distSq = (obj.Position - Position).LengthSquared();
        if (distSq < radiusSq)
        {
          obj.OnDamaged(damage, Owner);
        }
      }

      Owner?.Room.Despawn(this);
    }
  }
}
