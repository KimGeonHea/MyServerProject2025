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
    public int TempleteId { get => templeteId; set => templeteId = value; }
    public int OwnerId { get; set; } = 0; // 발사체 소유자 ID

    public HeroSkillData heroSkillData;
    public Hero Owner { get; set; }



    private int templeteId;
    private int damage;
    private float bulletSpeed;
    private float bulletRange;
    private Vector3 startPosition;

    const int MAXDISTANCE = 10;

    public virtual void OnSpawned()
    {
      Owner = null;
      ObjectID = 0;
      Position = Vector3.Zero;
      MoveDir = Vector3.Zero;
      bulletRange = 0;
      bulletSpeed = 0;
      IsAlive = true;
    }

    public virtual void OnDespawned()
    {
      
    }

    /// <summary>
    /// Skill 발사체 초기화
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="direction"></param>
    /// <param name="startPos"></param>
    /// <param name="isSkill"> 101  = 총알 , 102 = 스킬</param>
    public void Init(Hero owner, Vector3 direction, Vector3 startPos, bool isSkill = false)
    {
      Owner = owner;
      TempleteId = owner.TempleteID;
      ObjectType = EGameObjectType.Bullet;
      

      HeroSkillData skillData = null;
      if (DataManager.HeroSkilldataDict.TryGetValue(TempleteId , out skillData))
      {
        heroSkillData = skillData;
      }


      this.MoveDir = Vector3.Normalize(new Vector3(direction.X, 0, direction.Z));
      bulletSpeed = skillData.Speed;
      bulletRange = skillData.Range;
      startPosition = owner.Position;

      startPosition = new Vector3(owner.Position.X, startPos.Y, owner.Position.Z); // 발사 위치 조정 (Y축 1.0f 위로) 
      TempleteID = skillData.TemplateId;
      Position = startPosition;
      damage = owner.HeroData.AttackDamage; // 발사체 피해량은 소유자의 공격력으로 설정
      //Console.WriteLine(startPosition);
    }

    public override void FixedUpdate(float deltaTime)
    {
      base.FixedUpdate(deltaTime);
      CheckCollision();
      ApplyMove(MoveDir, bulletSpeed, deltaTime);
      
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

      Vector3 newPos = new Vector3(
          PosInfo.PosX + delta.X,
          PosInfo.PosY,
          PosInfo.PosZ + delta.Z
      );
      //충돌체크

      var grid = DataManager.ObstacleGrid;
      if (grid.IsBlockedXZ(newPos))
      {
        Owner?.Room.Despawn(this);
        return;
      }

      // 이동 적용
      PosInfo.PosX = newPos.X;
      PosInfo.PosY = newPos.Y;
      PosInfo.PosZ = newPos.Z;

      PosInfo.DirX = normalizedDir.X;
      PosInfo.DirY = 0;
      PosInfo.DirZ = normalizedDir.Z;
    }
    private void CheckCollision()
    {
      GameRoom room = Owner.Room as GameRoom;

      foreach (var c in room.creatures.Values)
      {
        if (c == null || c.ObjectID == Owner.ObjectID)
          continue;
        
        float dist = Vector3.Distance(c.ColliderPosition, this.Position);
        float totalRadius = heroSkillData.Radius + c.ColliderRadius; // 각 반지름 합

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
