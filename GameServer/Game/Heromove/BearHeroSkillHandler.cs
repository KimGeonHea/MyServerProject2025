using GameServer.Game.Room;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Heromove
{
  public class BearHeroSkillHandler : HeroSkillHandler
  {
    private bool flameOn;
    private HeroBullet bearBullet;

    // 마지막 유효 조준 방향(입력/오토타겟/시선 폴백으로 갱신)
    private Vector3 lastAimDir = Vector3.UnitZ;

    // 총구 오프셋을 로컬 성분으로 저장(앞/오른쪽/위)
    private float _weaponForward;
    private float _weaponRight;
    private float _weaponUp;

    private const float WeaponUpOffset = 0.5f;

    private const float MinToStart = 10f;
    private const float CostPerSec = 5f;

    // wantOn == true  & _flameOn == false  => Spawn (켜기)
    // wantOn == true  & _flameOn == true   => 유지(스폰 없음)
    // wantOn == false & _flameOn == true   => Despawn (끄기)
    // wantOn == false & _flameOn == false  => 아무것도 안 함

    public override bool OnBasicAttack(Hero hero, Vector3 inputDir, Vector3 muzzleWorldPos, out HeroBullet spawned)
    {
      spawned = null;

      GameRoom room = hero?.Room as GameRoom;
      if (room == null) return false;

      //  토글: 여기서만 뒤집는다 (Room에서 건드리면 또 꼬임)
      hero.IsPersistence = !hero.IsPersistence;

      bool wantOn = hero.IsPersistence;

      if (!wantOn)
      {
        if (flameOn) Stop(room);
        hero.SetUpperState(EHeroUpperState.Enone, 0f); //  OFF도 여기서
        return true; // spawned=null 정상
      }

      // ON 시도
      if (flameOn)
      {
        // 이미 켜져있는데 토글로 또 ON 들어온 거면 그냥 유지로 볼지/무시할지 선택
        hero.SetUpperState(EHeroUpperState.Eattack, 0f);
        return true;
      }

      float startCost = hero.ShotStaminaCost;   // 10f
      if (!hero.TryConsumeStamina(startCost))
      {
        hero.IsPersistence = false;             // 토글 원복(중요)
        return false;
      }

      // 스폰 로직(너 기존 로직 사용)
      Vector3 aim = ResolveAimDir(room, hero, inputDir);
      aim.Y = 0;
      if (aim.LengthSquared() < 1e-4f) aim = Vector3.UnitZ;
      Vector3 face = Vector3.Normalize(aim);

      bearBullet = room.UseBullet(hero, face, muzzleWorldPos);
      if (bearBullet == null)
      {
        hero.IsPersistence = false;
        return false;
      }

      bearBullet.Direction = face;
      bearBullet.Position = muzzleWorldPos;

      ApplyFacingToHero(hero, face);

      flameOn = true;
      spawned = bearBullet;

      hero.SetUpperState(EHeroUpperState.Eattack, 0f); //  ON 성공시에만
      return true;
    }

    public override void Tick(Hero hero, float dt)
    {
      if (!flameOn)
      {
        base.Tick(hero, dt);
        return;
      }

      GameRoom room = hero?.Room as GameRoom;
      if (room == null)
      {
        flameOn = false;
        bearBullet = null;
        return;
      }

      // 지속 스태미너 소모
      float need = CostPerSec * dt;
      if (!hero.TryConsumeStamina(need))
      {
        // 서버 권위로 강제 OFF
        hero.EHeroUpperState = EHeroUpperState.Enone;
        Stop(room);
        return;
      }

      if (bearBullet == null || !bearBullet.IsAlive)
      {
        flameOn = false;
        bearBullet = null;
        return;
      }

      // face 결정:
      // - 움직이면 이동방향(또는 너 MoveInputDir 같은 값)으로
      // - 안 움직이면 lastAim 유지
      Vector3 face = hero.Direction; // 네 프로젝트에서 "이동 방향"으로 쓰고 있으면 이게 맞고
      face.Y = 0;

      if (face.LengthSquared() >= 1e-4f)
        face = Vector3.Normalize(face);
      else
        face = lastAimDir;

      if (face.LengthSquared() < 1e-4f)
        face = Vector3.UnitZ;
      else
        face = Vector3.Normalize(face);

      ApplyFacingToHero(hero, face);

      Vector3 right = Vector3.Cross(Vector3.UnitY, face);
      if (right.LengthSquared() < 1e-6f)
        right = Vector3.UnitX;
      else
        right = Vector3.Normalize(right);

      Vector3 bulletPos = hero.Position + face * _weaponForward + right * _weaponRight;
      bulletPos.Y = hero.Position.Y + _weaponUp;

      bearBullet.Direction = face;
      bearBullet.Position = bulletPos;
    }

    public override void OnDead(Hero hero)
    {
      GameRoom room = hero?.Room as GameRoom;
      if (room != null)
        Stop(room);
    }

    private void Stop(GameRoom room)
    {
      if (bearBullet != null)
      {
        room.Despawn(bearBullet);
        bearBullet = null;
      }
      flameOn = false;
    }
    public override void OnSkillRelease(Hero hero)
    {
      GameRoom room = hero?.Room as GameRoom;
      if (room == null) 
        return;

      hero.EHeroUpperState = EHeroUpperState.Enone;
      Stop(room);
    }

    private static void ApplyFacingToHero(Hero hero, Vector3 face)
    {
      hero.PositionInfo.DirX = face.X;
      hero.PositionInfo.DirY = 0;
      hero.PositionInfo.DirZ = face.Z;
    }
  }
}


