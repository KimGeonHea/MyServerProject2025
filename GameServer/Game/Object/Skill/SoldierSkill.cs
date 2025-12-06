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
    private float lifeTime = 1.2f;
    private float yMultiplier = 1.3f;
    private float explosionRadius = 1.5f;

    public override void OnSpawned()
    {
      base.OnSpawned();
      velocity = Vector3.Zero;
      MoveDir = Vector3.Zero;
      elapsed = 0f;
    }

    public override void Init(Hero owner, Vector3 start, Vector3 targetPosition)
    {
      base.Init(owner, start, targetPosition);

      ObjectType = EGameObjectType.Skill;
      TempleteID = Owner.TempleteID + 1;
      damage = Owner.SkillDamage;

      startPosition = new Vector3(Owner.Position.X, start.Y, Owner.Position.Z);

      this.targetPosition = targetPosition;

      //HeroSkillData skillData = null;
      //if (DataManager.HeroSkilldataDict.TryGetValue(TempleteID, out skillData))
      //{
      //  heroSkillData = skillData;
      //}


      CalculateVelocity_FromHeight(startPosition, this.targetPosition, yMultiplier, lifeTime);
      Position = startPosition;
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

      if (elapsed > lifeTime)
      {
        Explode();
        return;
      }

      // 중력 적용
      velocity.Y += gravity * deltaTime;
      Vector3 delta = velocity * deltaTime;
      Vector3 nextPos = Position + delta;


      if (velocity.LengthSquared() > 0.0001f)
        MoveDir = Vector3.Normalize(new Vector3(velocity.X, velocity.Y, velocity.Z));
      // 충돌 검사 (장애물 또는 지면)
      if (DataManager.ObstacleGrid.IsBlocked(nextPos) || nextPos.Y <= 0)
      {
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
      if (_exploded)
        return;
      _exploded = true;
      IsAlive = true;
      // 풀에 돌아간 이후거나, 이미 방에서 제거된 경우 방어
      if (Owner == null || Room == null)
        return;

      GameRoom room = Room as GameRoom;
      if (room == null)
        return;

      // heroSkillData 가 예상과 다르게 null 이어도 죽지 않게 기본값 사용
      float range = 10.0f;
      if (heroSkillData != null && heroSkillData.Range > 0)
        range = heroSkillData.Range;


      foreach (var obj in room.creatures.Values)
      {
        if (obj == null || obj.ObjectID == Owner.ObjectID)
          continue;

        float distSq = (obj.Position - Position).LengthSquared();
        if (distSq < range)
        {
          Vector3 dir = Vector3.Normalize(obj.Position - Position);
          obj.OnDamageKnockback(damage, dir, 3.5f , Owner);
        }
      }

      room.Despawn(this);
    }


  }
}
