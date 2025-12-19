using GameServer.Game.Room;
using GameServer.Utils;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class HeroBullet : BaseObject , IPoolable
  {

    protected float gravity = -9.8f;
    /// <summary>
    /// 여기서부터 데이터
    /// </summary>
    public HeroSkillData heroSkillData;
    protected float Speed;
    protected float Range;
    protected float Radius;
    protected float CcPower;
    protected float CcDuration;
    /// <summary>
    /// 여기서부터 오너 
    /// </summary>
    public Hero Owner { get; set; }
    public int OwnerId { get; set; } = 0; // 발사체 소유자 

    protected int damage;

    protected Vector3 startPosition;

    public bool GameplayEnabled { get; private set; } = true;
    public void SetGameplayEnabled(bool enabled)
      => GameplayEnabled = enabled;

    protected const int MAXDISTANCE = 10;

    public virtual void OnSpawned()
    {
      IsAlive = true;
      Owner = null;
      ObjectID = 0;
      Position = Vector3.Zero;
      Direction = Vector3.Zero;
      Range = 0;
      Speed = 0;
      GameplayEnabled = true;
    }

    public virtual void OnDespawned()
    {
      IsAlive = false;
      //Owner = null;
      //ObjectID = 0;
      //Position = Vector3.Zero;
      //Direction = Vector3.Zero;
      //Range = 0;
      //Speed = 0;
    }

    /// <summary>
    /// Bullet 발사체 초기화
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="direction"></param>
    /// <param name="startPos"></param>
    /// <param name="isSkill"> 101  = 총알 , 102 = 스킬</param>
    public virtual void Init(Hero owner, Vector3 direction, Vector3 startPos)
    {
      Owner = owner;
      OwnerId = owner.ObjectID;
      TempleteID = owner.TempleteID;
      ObjectType = EGameObjectType.Bullet;
      

      HeroSkillData skillData = null;
      if (DataManager.HeroSkilldataDict.TryGetValue(TempleteID, out skillData))
        heroSkillData = skillData;

      if(skillData != null)
      {
        TempleteID = skillData.TemplateId;
        Speed = skillData.Speed;
        Range = skillData.Range;
        CcDuration = skillData.CcDuration;
        CcPower = skillData.CcPower;  
      }

      this.Direction = Vector3.Normalize(new Vector3(direction.X, 0, direction.Z));

      startPosition = owner.Position;
      startPosition = new Vector3(owner.Position.X, startPos.Y, owner.Position.Z); // 발사 위치 조정 무기 포지션으로 받아서 처리
      Position = startPosition;


      if(owner != null)
      damage = owner.AttackDamage; // 발사체 피해량은 소유자의 공격력으로 설정

    }

    public override void FixedUpdate(float deltaTime)
    {
      if (!IsAlive || Owner == null || Owner.Room == null)
        return;
      base.FixedUpdate(deltaTime);
      CheckCollision();
      ApplyMove(Direction, Speed, deltaTime);
      
      // 발사체가 범위를 벗어났는지 확인
      if (Vector3.Distance(startPosition, Position) > MAXDISTANCE)
      {
        Owner?.Room.Despawn(this);
      }      
    }
    public override void Update(float deltaTime)
    {
      base.Update(deltaTime);
    }
    public override void ApplyMove(Vector3 dir, float speed, float deltaTime)
    {
      // Y축 노이즈 제거: XZ 평면 이동만 처리
      Vector3 cleanDir = new Vector3(dir.X, 0, dir.Z);

      if (cleanDir.LengthSquared() < 0.001f)
        return;

      Vector3 normalizedDir = Vector3.Normalize(cleanDir);
      Vector3 delta = normalizedDir * speed * deltaTime;

      Vector3 newPos = new Vector3(PositionInfo.PosX + delta.X,PositionInfo.PosY, PositionInfo.PosZ + delta.Z);
      //충돌체크

      var grid = DataManager.ObstacleGrid;
      if (grid.IsBlockedXZ(newPos))
      {
        Owner?.Room.Despawn(this);
        return;
      }
      Position = newPos;
      // 이동 적용
      PositionInfo.PosX = newPos.X;
      PositionInfo.PosY = newPos.Y;
      PositionInfo.PosZ = newPos.Z;

      PositionInfo.DirX = normalizedDir.X;
      PositionInfo.DirY = 0;
      PositionInfo.DirZ = normalizedDir.Z;
    }
    protected virtual void CheckCollision()
    {
      GameRoom room = Owner.Room as GameRoom;

      foreach (var c in room.creatures.Values)
      {
        if (c == null || c.ObjectID == Owner.ObjectID)
          continue;

        Vector3 xzPos = new Vector3(Position.X , 0 , Position.Z);
        
        float dist = Vector3.Distance(c.Position, xzPos);
        float totalRadius = Radius + c.ColliderRadius; // 각 반지름 합

        if (dist < totalRadius)
        {
          c.OnDamageBasic(damage, Owner);
          Owner?.Room.Despawn(this);
          break;
        }
      }
    }
  }
}
