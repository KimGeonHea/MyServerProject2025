using GameServer.Game.Room;
using GameServer.Utils;
using Google.Protobuf.Protocol;
using Microsoft.IdentityModel.Protocols.OpenIdConnect.Configuration;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace GameServer.Game
{
  public class HeroBullet : BaseObject , IPoolable
  {
    public int TempleteId;

    public HeroSkillData heroSkillData;
    public int OwnerId { get; set; } = 0; // 발사체 소유자 ID
    public Hero Owner { get; set; }
    //public float Speed { get; set; } = heroSkillData.Speed;
    private int damage;
    private float bulletSpeed;
    private float bulletRange = 10;
    private Vector3 startPosition;
    private float heroRadius = 0.3f;

    public void OnSpawned()
    {
      Owner = null;
      ObjectID = 0;
      Position = Vector3.Zero;
      MoveDir = Vector3.Zero;
      bulletRange = 0;
      bulletSpeed = 0;
    }

    public void OnDespawned()
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
      TempleteId = owner.TemplatedId;

      HeroSkillData skillData = null;
      if (DataManager.heroSkillDict.TryGetValue(TempleteId , out skillData))
      {
        heroSkillData = skillData;
      }


      this.MoveDir = Vector3.Normalize(new Vector3(direction.X, 0, direction.Z));
      bulletSpeed = skillData.Speed;
      bulletRange = skillData.Range;
      startPosition = owner.Position;


      startPosition = new Vector3(owner.Position.X, startPos.Y, owner.Position.Z); // 발사 위치 조정 (Y축 1.0f 위로) 

      ObjectType = EGameObjectType.Projecttile;
      TempleteID = skillData.TemplateId;

      Position = startPosition;
      damage = owner.HeroData.AttackDamage; // 발사체 피해량은 소유자의 공격력으로 설정
      Console.WriteLine(startPosition);
    }

    public override void FixedUpdate(float deltaTime)
    {
      base.FixedUpdate(deltaTime);
      CheckCollision();
      ApplyMove(MoveDir, bulletSpeed, deltaTime);
      
      // 발사체가 범위를 벗어났는지 확인
      if (Vector3.Distance(startPosition, Position) > 10)
      {
        // 범위를 벗어나면 제거
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
      {
        // 이동 없음, 방향 유지 (PosInfo.DirX/Y/Z 덮어쓰기 안 함)
        return;
      }

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
      float totalRadius = heroSkillData.Radius + heroRadius; // 피격 범위
      totalRadius *= totalRadius;
      GameRoom room = Owner.Room as GameRoom;

      foreach (var obj in room.heros.Values)
      {
        if (obj.ObjectType != EGameObjectType.Hero)
          continue;

        if (obj == null || obj.ObjectID == Owner.ObjectID)
          continue;

        Hero target = obj;
        float distSq = new Vector3(target.Position.X - Position.X,0,target.Position.Z - Position.Z).LengthSquared();
        Console.WriteLine
          ($"[Check] BulletPos={Position}, TargetPos={target.Position}, DistSq={distSq} , totalRadius = {totalRadius}");
        if (distSq < totalRadius)
        {
          // 피격 처리
          target.OnDamaged(damage, Owner);
          //// 총알 제거
          Owner?.Room.Despawn(this);
          break;
        }
      }
    }


  }


}
