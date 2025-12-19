using GameServer.Game.Room;
using GameServer.Utils;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public interface IHeroSkillHandler
  {
    // 평타
    bool OnBasicAttack(Hero hero, Vector3 dir, Vector3 startPos, out HeroBullet spawnedBullet);

    // 스킬 버튼 눌렀을 때: 스킬 오브젝트 스폰 가능(투사체형이면)
    // 즉발형이면 spawnedSkill = null로 두고 true 반환(“성공” 의미)
    bool OnSkillAttack(Hero hero, Vector3 dir, Vector3 targetPos, out HeroSkill spawnedSkill);

    // 스킬 버튼 뗐을 때 (홀드형에서만 의미)
    void OnSkillRelease(Hero hero);

    void Tick(Hero hero, float dt);
    void OnDead(Hero hero);
  }


  public class HeroSkillHandler : IHeroSkillHandler
  {
    const float AIM_DEADZONE = 0.20f;
    const float AIM_DEADZONE_SQ = AIM_DEADZONE * AIM_DEADZONE;
    public virtual bool OnBasicAttack(Hero hero, Vector3 dir, Vector3 startPos, out HeroBullet spawnedBullet)
    {
      spawnedBullet = null;

      GameRoom room = hero?.Room as GameRoom;
      if (room == null) 
        return false;

      if (!hero.TryConsumeStamina(hero.ShotStaminaCost))
        return false;

      Vector3 aim = ResolveAimDir(room, hero, dir);
      if (aim.LengthSquared() < 0.0001f)
        return false;

      hero.Direction = aim;

      spawnedBullet = room.UseBullet(hero, aim, startPos);
      if (spawnedBullet == null)
        return false;

      //  성공한 경우에만 애니 상태

      hero.SetUpperState(EHeroUpperState.Eattack, 0.15f);
      return true;
    }

    public virtual bool OnSkillAttack(Hero hero, Vector3 dir, Vector3 targetPos, out HeroSkill spawnedSkill)
    {
      spawnedSkill = null;

      GameRoom room = hero?.Room as GameRoom;
      if (room == null)
        return false;

      // 여기선 예시로 스태미너를 ShotStaminaCost로 쓰고 있는데
      // 스킬이면 SkillStaminaCost 같은 걸로 분리하는 게 더 좋음
      if (!hero.TryConsumeStamina(hero.ShotStaminaCost))
        return false;

      Vector3 aim = ResolveAimDir(room, hero, dir);
      if (aim.LengthSquared() < 0.0001f)
        return false;

      hero.Direction = aim;

      //  스킬 팩토리로 스폰되는 구조면 room.UseSkill(...)을 호출
      // 즉발형이면 spawnedSkill == null이어도 true를 반환해서 “성공”을 알릴 수 있음
      spawnedSkill = room.UseSkill(hero, aim, targetPos);
      return true; // UseSkill이 즉발이면 null 리턴하니까, 성공/실패는 UseSkill 내부 정책에 맞춰 조절
    }

    public virtual void OnSkillRelease(Hero hero)
    {
      // 기본은 아무것도 안 함
    }

    public virtual void Tick(Hero hero, float dt)
    {
      if (hero == null)
        return;

      hero.TickStamina(dt);
    }

    public virtual void OnDead(Hero hero)
    {
      // 기본은 아무것도 안 함
    }


    //  Bear가 쓰게 protected 로 제공
    protected Vector3 ResolveAimDir(GameRoom room, Hero hero, Vector3 inputDir)
    {
      Vector3 dir = new Vector3(inputDir.X, 0, inputDir.Z);
      // 데드 존 이내입력 0.04 기준//
      if (dir.LengthSquared() > AIM_DEADZONE_SQ)
        return Vector3.Normalize(dir);
      //오토 공격//
      Creature nearest = room.FindClosestEnemy(hero);

      if (nearest != null)
      {
        Vector3 to = nearest.Position - hero.Position;
        to.Y = 0;
        if (to.LengthSquared() > 0.0001f)
          return Vector3.Normalize(to);
      }

      Vector3 view = hero.Direction; 
      view.Y = 0;
      //hero.Direction 기준//
      if (view.LengthSquared() < 0.0001f)
        view = new Vector3(hero.PositionInfo.DirX, 0, hero.PositionInfo.DirZ);

      if (view.LengthSquared() < 0.0001f)
        return Vector3.Zero;

      return Vector3.Normalize(view);
    }
  }
}
