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

namespace GameServer.Game.Object.Packtory
{
  public class SkillFactory
  {
    private readonly Dictionary<int, ObjectPool<HeroSkill>> _pools =
     new Dictionary<int, ObjectPool<HeroSkill>>();

    // 기본 스킬 풀 (등록 안 된 템플릿용)
    private readonly ObjectPool<HeroSkill> _defaultPool;

    public SkillFactory()
    {
      _defaultPool = new ObjectPool<HeroSkill>(() => new HeroSkill());

      _pools[Define.HOODIE_TEMPLATE_ID + 1] =
        new ObjectPool<HeroSkill>(() => new HoodieSkill());

      _pools[Define.SOLDIER_TEMPLATE_ID + 1] =
        new ObjectPool<HeroSkill>(() => new SoldierSkill());
    }

    // 여기서 스킬 종류에 따라 다르게 처리
    public HeroSkill UseSkill(Hero owner, Vector3 dir, Vector3 targetPos, GameRoom room)
    {
      if (owner == null || room == null)
        return null;

      int skillTemplateId = owner.TempleteID + 1;

      if (!DataManager.HeroSkilldataDict.TryGetValue(skillTemplateId, out HeroSkillData data))
        return null;

      switch (data.SkillType)
      {
        case EHeroSkillType.EskillTypeProjectile:
        case EHeroSkillType.EskillTypeHomingProjectile:
          return SpawnProjectileSkill(owner, dir, targetPos, room, skillTemplateId);

        case EHeroSkillType.EskillTypeDash:
          ExecuteTeleportLikeDash(owner, dir, targetPos, room, data);
          return null;

        case EHeroSkillType.EskillTypeAreaexplosion:
          ExecuteAreaExplosion(owner, targetPos, room, data);
          return null;

        // 나중에 FIELD/AURA/BEAM 등은 여기서 계속 추가
        default:
          return null;
      }
    }

    // ======================
    //   1) Projectile 계열
    // ======================
    private HeroSkill SpawnProjectileSkill(Hero owner, Vector3 dir, Vector3 targetPos, GameRoom room, int skillTemplateId)
    {
      if (!_pools.TryGetValue(skillTemplateId, out var pool))
        pool = _defaultPool;

      HeroSkill skill = pool.Rent();        // 내부에서 _factory()로 SoldierSkill/HoodieSkill 생성
      skill.Init(owner, dir, targetPos);
      room.EnterGame(skill);                //  Room 세팅
      return skill;
    }

    // ======================
    //   2) 순간이동/대시 계열
    // ======================
    private void ExecuteTeleportLikeDash(Hero owner, Vector3 dir, Vector3 targetPos, GameRoom room, HeroSkillData data)
    {
      // 데이터에 range 있으면 사용, 아니면 targetPos 그대로
      float range = data.Range > 0 ? data.Range : 5.0f;
      dir = dir.LengthSquared() > 0.0001f ? Vector3.Normalize(dir) : owner.MoveDir;

      Vector3 dst = owner.Position + dir * range;
      dst.Y = owner.Position.Y;

      // 충돌/벽 체크 필요하면 여기서 ObstacleGrid 한 번 감싸주고 보정
      if (DataManager.ObstacleGrid.IsBlocked(dst))
      {
        // 벽이면 살짝 줄이거나, 그냥 막아도 되고
        // 여기선 간단히: 못 가면 취소
        return;
      }

      // 서버에서 순간이동
      owner.Position = dst;

      //S_HeroTeleport pkt = new S_HeroTeleport
      //{
      //  HeroInfo = new HeroInfo
      //  {
      //    ObjectInfo = new ObjectInfo
      //    {
      //      ObjectId = owner.ObjectID,
      //      TemplateId = owner.TemplatedId,
      //      ObjectType = owner.ObjectType
      //    },
      //    PosInfo = new PositionInfo
      //    {
      //      PosX = dst.X,
      //      PosY = 0,
      //      PosZ = dst.Z,
      //      DirX = dir.X,
      //      DirY = dir.Y,
      //      DirZ = dir.Z
      //    }
      //  }
      //};
      //room.Broadcast(pkt);
    }

    // ======================
    //   3) 위치 지정 폭발 계열(AREAEXPLOSION)
    // ======================
    private void ExecuteAreaExplosion(Hero owner, Vector3 targetPos, GameRoom room, HeroSkillData data)
    {
      float radius = data.Radius > 0 ? data.Radius : 3.0f;
      float radiusSq = radius * radius;
      int damage = owner.AttackDamage; // or owner.SkillDamage 기반으로

      foreach (var hero in room.heroes.Values)
      {
        if (hero == null || hero.ObjectID == owner.ObjectID)
          continue;

        float distSq = (hero.Position - targetPos).LengthSquared();
        if (distSq <= radiusSq)
        {
          hero.OnDamageBasic(damage, owner);
        }
      }

      //S_HeroAreaExplosion pkt = new S_HeroAreaExplosion
      //{
      //  CasterId = owner.ObjectID,
      //  PosInfo = new PositionInfo
      //  {
      //    PosX = targetPos.X,
      //    PosY = 0,
      //    PosZ = targetPos.Z
      //  },
      //  Radius = radius
      //};
      //room.Broadcast(pkt);
    }

    public void ReturnSkill(HeroSkill skill)
    {
      if (skill == null)
        return;

      int key = skill.TempleteID;

      if (key != 0 && _pools.TryGetValue(key, out var pool))
        pool.Return(skill);
      else
        _defaultPool.Return(skill);
    }
  }
}
