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

    private readonly BulletFactory bulletFactory = new BulletFactory();
    private readonly SkillFactory skillFactory = new SkillFactory();

    // === 팩토리 래핑 메서드 ===
    public HeroBullet UseBullet(Hero owner, Vector3 dir, Vector3 startPos)
      => bulletFactory.UseBullet(owner, dir, startPos, this);

    public void ReturnBullet(HeroBullet bullet)
      => bulletFactory.ReturnBullet(bullet);

    public HeroSkill UseSkill(Hero hero, Vector3 dir, Vector3 targetPos)
      => skillFactory.UseSkill(hero, dir, targetPos, this);

    public void ReturnSkill(HeroSkill skill)
      => skillFactory.ReturnSkill(skill);

    // ============================================================
    //  총알 발사
    // ============================================================
    public void ShotHero(Player player, C_HeroShot pkt)
    {
      if (player == null) return;
      Hero hero = player.selectHero;
      if (hero == null) return;
      if (hero.IsDead || hero.IsCC()) return;

      // 1) 클라 입력은 그대로(raw). 0벡터여도 그대로 둔다 (오토타겟을 위해)
      Vector3 dir = new Vector3(pkt.HeroInfo.PosInfo.DirX, 0, pkt.HeroInfo.PosInfo.DirZ);

      Vector3 muzzleWorldPos = new Vector3(
        pkt.HeroInfo.PosInfo.PosX,
        pkt.HeroInfo.PosInfo.PosY,
        pkt.HeroInfo.PosInfo.PosZ);


      //hero.Direction = dir;

      HeroBullet spawned;
      bool ok = hero.HandleBasicAttack(dir, muzzleWorldPos, out spawned);
      if (!ok) return;

      // 스폰 이벤트가 없으면(베어 OFF/유지 등) 브로드캐스트 생략(원하면 별도 상태패킷)
      if (spawned == null)
        return;

      //Broadcast(new S_HeroShot
      //{
      //  HeroInfo = new HeroInfo
      //  {
      //    ObjectInfo = new ObjectInfo
      //    {
      //      ObjectId = hero.ObjectID,
      //      TemplateId = hero.TempleteID,
      //      ObjectType = hero.ObjectType
      //    },
      //    UpperState = hero.EHeroUpperState, // ✅ 핸들러가 이미 세팅한 값
      //    PosInfo = new PositionInfo
      //    {
      //      PosX = hero.PositionInfo.PosX,
      //      PosY = 0,
      //      PosZ = hero.PositionInfo.PosZ,
      //      DirX = dir.X,
      //      DirY = 0,
      //      DirZ = dir.Z
      //    }
      //  }
      //});
    }

    // ============================================================
    //  스킬 발사
    // ============================================================
    public void SkillHero(Player player, C_HeroSkill pkt)
    {
      if (player == null || player.selectHero == null) 
        return;

      Hero hero = player.selectHero;
      if (hero.IsDead || hero.IsCC()) 
        return;

      Vector3 dir = new Vector3(pkt.HeroInfo.PosInfo.DirX, pkt.HeroInfo.PosInfo.DirY, pkt.HeroInfo.PosInfo.DirZ);
      Vector3 targetPos = new Vector3(pkt.HeroInfo.PosInfo.PosX, pkt.HeroInfo.PosInfo.PosY, pkt.HeroInfo.PosInfo.PosZ);

      if (dir.LengthSquared() < 0.0001f) 
        return;
      dir = Vector3.Normalize(dir);
      hero.Direction = dir;

      if (hero.TempleteID == Define.HERO_TEMPLATE_BEAR)
      {
        hero.HandleSkillRelease();
        hero.SetUpperState(EHeroUpperState.Enone, 0f);
      }

      hero.SetUpperState(EHeroUpperState.Eskill, 0.15f);

      HeroSkill skill = skillFactory.UseSkill(hero, dir, targetPos, this);
      if (skill == null) 
        return;

      Broadcast(new S_HeroSkill
      {
        HeroInfo = new HeroInfo
        {
          UpperState = hero.EHeroUpperState,
          ObjectInfo = new ObjectInfo { ObjectId = hero.ObjectID, TemplateId = hero.TempleteID, ObjectType = hero.ObjectType },
          PosInfo = new PositionInfo { PosX = hero.PositionInfo.PosX, PosY = 0, PosZ = hero.PositionInfo.PosZ, DirX = dir.X, DirY = dir.Y, DirZ = dir.Z }
        }
      });

    }
    // ============================================================
    //  타겟 찾기 (플레이어 우선, 없으면 몬스터)
    // ============================================================
    public Creature FindClosestEnemy(Hero hero)
    {
      if (hero == null || heroes == null)
        return null;

      const float heroRange = 10.0f;
      const float heroRangeSq = heroRange * heroRange;

      Creature closest = null;
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
  }
}

