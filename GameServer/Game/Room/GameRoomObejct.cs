using GameServer.Game.Object.Packtory;
using GameServer.Utils;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{

  public partial class GameRoom : Room
  {

    private readonly BulletFactory _bulletFactory = new BulletFactory();
    private readonly SkillFactory _skillFactory = new SkillFactory();

    // === 팩토리 래핑 메서드만 남김 ===
    public HeroBullet RentBullet(Hero owner)
      => _bulletFactory.RentBullet(owner);

    public void ReturnBullet(HeroBullet bullet)
      => _bulletFactory.ReturnBullet(bullet);

    //public HeroSkill RentSkill(Hero hero)
    //  => _skillFactory.RentSkill(hero);

    public void ReturnSkill(HeroSkill skill)
      => _skillFactory.ReturnSkill(skill);


    // ============================================================
    //  총알 발사
    // ============================================================
    public void ShotHero(Player player, C_HeroShot c_HeroSkill)
    {
      if (player == null || player.selectHero == null)
        return;

      Hero hero = player.selectHero;

      if (!hero.TryConsumeStamina(hero.ShotStaminaCost))
        return;

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

      bool hasInput = dir.LengthSquared() > 0.01f;

      if (hasInput)
      {
        // 입력이 있으면 그 방향으로 정규화
        dir = Vector3.Normalize(dir);
      }
      else
      {
        // 입력이 없으면 오토 타겟
        BaseObject nearest = FindClosestEnemy(hero);
        if (nearest != null)
        {
          Vector3 toTarget = nearest.Position - hero.Position;
          if (toTarget.LengthSquared() > 0.0001f)
            dir = Vector3.Normalize(toTarget);
        }
      }

      // 아직도 방향이 없다면(입력도 없고 타겟도 없음) 발사 취소
      if (dir.LengthSquared() < 0.0001f)
      {
        return;
      }

      dir = Vector3.Normalize(dir);
      hero.MoveDir = dir;

      hero.EHeroUpperState = c_HeroSkill.HeroInfo.UpperState;
      hero.EHeroLowerState = c_HeroSkill.HeroInfo.LowerState;

      // 3. Bullet 생성 -  여기 중요: 이제 팩토리 버전 사용
      HeroBullet bullet = RentBullet(hero);
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
            PosY = 0, // Y축은 0으로 고정
            PosZ = hero.PosInfo.PosZ,
            DirX = dir.X,
            DirY = dir.Y,
            DirZ = dir.Z
          }
        },
        /* 현재 클라에서 처리중
        //ObjectInfo = bullet.ObjectInfo,
        //PosInfo = new PositionInfo
        //{
        //  PosX = bullet.Position.X,
        //  PosY = bullet.Position.Y,
        //  PosZ = bullet.Position.Z,
        //  DirX = dir.X,
        //  DirY = dir.Y,
        //  DirZ = dir.Z
        //},
        //OwnerId = hero.ObjectID
        */
      };
      Broadcast(pkt);
    }

    // ============================================================
    //  타겟 찾기 (플레이어 우선, 없으면 몬스터)
    // ============================================================
    public BaseObject FindClosestEnemy(Hero hero)
    {
      if (hero == null || heroes == null)
        return null;

      const float heroRange = 10.0f;
      const float heroRangeSq = heroRange * heroRange;

      BaseObject closest = null;
      float bestDistSq = float.MaxValue;

      // 1) 먼저 다른 플레이어(영웅)부터 찾기
      foreach (Hero other in heroes.Values)
      {
        if (other == null)
          continue;

        // 자기 자신 스킵
        if (other.ObjectID == hero.ObjectID)
          continue;

        float distSq = (hero.Position - other.Position).LengthSquared();

        // 10 유닛 안에 있는 플레이어만 후보
        if (distSq <= heroRangeSq && distSq < bestDistSq)
        {
          bestDistSq = distSq;
          closest = other;
        }
      }

      // 10유닛 안에 플레이어가 있으면 바로 리턴
      if (closest != null)
        return closest;

      // 2) 플레이어가 없으면 몬스터 중에서 가장 가까운 놈
      if (monsters != null)
      {
        foreach (Monster mon in monsters.Values)
        {
          if (mon == null)
            continue;

          float distSq = (hero.Position - mon.Position).LengthSquared();

          if (distSq < bestDistSq)
          {
            bestDistSq = distSq;
            closest = mon;
          }
        }
      }

      return closest; // 아무것도 없으면 null
    }

    // ============================================================
    //  스킬 발사
    // ============================================================
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
      Vector3 targetPos = new Vector3(
          c_HeroSkill.HeroInfo.PosInfo.PosX,
          c_HeroSkill.HeroInfo.PosInfo.PosY,
          c_HeroSkill.HeroInfo.PosInfo.PosZ
      );

      if (dir.LengthSquared() < 0.0001f)
      {
        // 필요하면 여기서도 오토타겟 or 그냥 return
        return;
      }

      dir = Vector3.Normalize(dir);
      hero.MoveDir = dir;
      hero.EHeroUpperState = c_HeroSkill.HeroInfo.UpperState;
      hero.EHeroLowerState = c_HeroSkill.HeroInfo.LowerState;

      // 3. Skill 생성 - 팩토리 호출
      HeroSkill skill = _skillFactory.UseSkill(hero , dir , targetPos ,this);
      //skill.Init(hero, dir, endPos);

      // 4. 게임룸에 추가 
      //EnterGame(skill); 이젠 팩토리에서 진행함

      // 5. 클라에게 브로드캐스트

      if (skill != null)
      {
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
              PosY = 0, // Y축은 0으로 고정
              PosZ = hero.PosInfo.PosZ,
              DirX = dir.X,
              DirY = dir.Y,
              DirZ = dir.Z
            }
          },

          /* 현재 생성은 클라에서 자발적으로 처리중
          //SkillObjectInfo = new SkillObjectInfo
          //{
          //  ObjectInfo = new ObjectInfo
          //  {
          //    ObjectId = skill.ObjectID,
          //    TemplateId = skill.TempleteID,
          //    ObjectType = skill.ObjectType
          //  },
          //  PosInfo = new PositionInfo
          //  {
          //    PosX = skill.Position.X,
          //    PosY = skill.Position.Y,
          //    PosZ = skill.Position.Z,
          //    DirX = dir.X,
          //    DirY = dir.Y,
          //    DirZ = dir.Z
          //  }
          //}
          */
        };
        Broadcast(pkt);
      }      
    }
  }
}

