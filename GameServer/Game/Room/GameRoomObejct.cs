using GameServer.Utils;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{

  public partial class GameRoom : Room
  {
    private ObjectPool<HeroBullet> bulletPool = new ObjectPool<HeroBullet>();
    private ObjectPool<HeroSkill> skillPool = new ObjectPool<HeroSkill>();
    public int SkillPoolCount => bulletPool.Count;
    public int BulletPoolCount => bulletPool.Count;
    public HeroBullet RentBullet()
    {
      return bulletPool.Rent();
    }

    public void ReturnBullet(HeroBullet bullet)
    {
      bulletPool.Return(bullet);
    }

    public HeroSkill RentSkill(Hero hero)
    {
      if (hero.TemplatedId == Define.HOODIE_TEMPLATE_ID)
        return new HoodieSkill();
      else if (hero.TemplatedId == Define.SOLDIER_TEMPLATE_ID)
        return new SoldierSkill();
      //else if (hero.TemplatedId == Define.BERAR_TEMPLATE_ID)
      //{
      //  // 곰 스킬은 따로 풀 관리 안 함
      //  return new BearSkill();
      //}
      //else if (hero.TemplatedId == Define.ROBOT_TEMPLATE_ID)
      //{
      //  return new RobotSkill();  
      //}

        // 일반 스킬은 풀에서 꺼냄
        return skillPool.Rent();
    }
    public void ReturnBullet(HeroSkill bullet)
    {
      skillPool.Return(bullet);
    }


    public void ShotHero(Player player, C_HeroShot c_HeroSkill)
    {
      if (player == null || player.selectHero == null)
        return;

      Hero hero = player.selectHero;
      // 1. 방향 및 애니메이션 상태 저장
      Vector3 dir = new Vector3(
          c_HeroSkill.HeroInfo.PosInfo.DirX,
          c_HeroSkill.HeroInfo.PosInfo.DirY,
          c_HeroSkill.HeroInfo.PosInfo.DirZ
      );
      Vector3 startPos = new Vector3(
          c_HeroSkill.HeroInfo.PosInfo.PosX,
          c_HeroSkill.HeroInfo.PosInfo.PosY,
          c_HeroSkill.HeroInfo.PosInfo.PosZ
      );

      if (dir.LengthSquared() > 0.01f)
      {
        dir = Vector3.Normalize(dir);
      }
      else
      {
        BaseObject nearest = FindClosestEnemy(hero); //오토공격
        if (nearest != null)
        {
          Vector3 toTarget = nearest.Position - hero.Position;
          dir = Vector3.Normalize(toTarget);
        }
      }
      dir = Vector3.Normalize(dir);
      hero.MoveDir = dir;
      hero.EHeroUpperState = c_HeroSkill.HeroInfo.UpperState;
      hero.EHeroLowerState = c_HeroSkill.HeroInfo.LowerState;

      // 3. Bullet 생성
      HeroBullet bullet = RentBullet();
      bullet.Init(hero, dir, startPos);
      // 4. 게임룸에 추가
      EnterGame(bullet);
      // 5. 클라에게 브로드캐스트
      S_HeroShot pkt = new S_HeroShot()
      {
        HeroInfo = new HeroInfo
        {
          ObjectInfo = new ObjectInfo
          {
            ObjectId = hero.ObjectID,
            TemplateId = hero.TempleteID,
            ObjectType = hero.ObjectType
          },
          UpperState = hero.EHeroUpperState,
          LowerState = hero.EHeroLowerState,
          PosInfo = new PositionInfo
          {
            PosX = hero.PosInfo.PosX,
            PosY = 0, //hero.PosInfo.PosY, // Y축은 0으로 고정
            PosZ = hero.PosInfo.PosZ,
            DirX = dir.X,
            DirY = dir.Y,
            DirZ = dir.Z
          }
        },
        ObjectInfo = bullet.ObjectInfo,
        PosInfo = new PositionInfo
        {
          PosX = bullet.Position.X,
          PosY = bullet.Position.Y,
          PosZ = bullet.Position.Z,
          DirX = dir.X,
          DirY = dir.Y,
          DirZ = dir.Z
        },
        OwnerId = hero.ObjectID
      };
      Broadcast(pkt);
    }

    public BaseObject FindClosestEnemy(Hero hero)
    {
      BaseObject closest = null;
      float minDistSq = float.MaxValue;

      foreach (Hero obj in heros.Values)
      {
        Hero other = obj as Hero;
        if (obj.ObjectID == hero.ObjectID)
          continue;

        // 전체 3D 거리
        float distSq = (hero.Position - other.Position).LengthSquared();


        if (distSq < minDistSq)
        {
          minDistSq = distSq;
          closest = obj;
        }
      }

      return closest;
    }


    public void SkillHero(Player player, C_HeroSkill c_HeroSkill)
    {
      if (player == null || player.selectHero == null)
        return;

      Hero hero = player.selectHero;

      // 1. 방향 및 애니메이션 상태 저장
      Vector3 dir = new Vector3(
          c_HeroSkill.HeroInfo.PosInfo.DirX,
          c_HeroSkill.HeroInfo.PosInfo.DirY,
          c_HeroSkill.HeroInfo.PosInfo.DirZ
      );
      Vector3 endPos = new Vector3(
          c_HeroSkill.HeroInfo.PosInfo.PosX,
          c_HeroSkill.HeroInfo.PosInfo.PosY,
          c_HeroSkill.HeroInfo.PosInfo.PosZ
      );

     
      dir = Vector3.Normalize(dir);
      hero.MoveDir = dir;
      hero.EHeroUpperState = c_HeroSkill.HeroInfo.UpperState;
      hero.EHeroLowerState = c_HeroSkill.HeroInfo.LowerState;


      // 3. Skill 생성
      HeroSkill skill = RentSkill(hero);
      skill.Init(hero, dir, endPos);
      // 4. 게임룸에 추가
      EnterGame(skill);
      // 5. 클라에게 브로드캐스트
      S_HeroSkill pkt = new S_HeroSkill()
      {
        HeroInfo = new HeroInfo
        {
          ObjectInfo = new ObjectInfo
          {
            ObjectId = hero.ObjectID,
            TemplateId = hero.TempleteID,
            ObjectType = hero.ObjectType
          },
          UpperState = hero.EHeroUpperState,
          LowerState = hero.EHeroLowerState,
          PosInfo = new PositionInfo
          {
            PosX = hero.PosInfo.PosX,
            PosY = 0, //hero.PosInfo.PosY, // Y축은 0으로 고정
            PosZ = hero.PosInfo.PosZ,
            DirX = dir.X,
            DirY = dir.Y,
            DirZ = dir.Z
          }
        },
        ObjectInfo = skill.ObjectInfo,
        PosInfo = new PositionInfo
        {
          PosX = skill.Position.X,
          PosY = skill.Position.Y,
          PosZ = skill.Position.Z,
          DirX = dir.X,
          DirY = dir.Y,
          DirZ = dir.Z
        },
        OwnerId = hero.ObjectID
      };
      Broadcast(pkt);
    }

  }
}
