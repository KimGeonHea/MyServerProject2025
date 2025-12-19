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
    private readonly Dictionary<int, ObjectPool<HeroSkill>> pools = new Dictionary<int, ObjectPool<HeroSkill>>();

    // 기본 스킬 풀 (등록 안 된 템플릿용)
    private readonly ObjectPool<HeroSkill> _defaultPool;

    public SkillFactory()
    {
      _defaultPool = new ObjectPool<HeroSkill>(() => new HeroSkill());

      pools[Define.HERO_TEMPLATE_HOODIE + 1] =new ObjectPool<HeroSkill>(() => new HoodieSkill());
      pools[Define.HERO_TEMPLATE_SOLDIER + 1] = new ObjectPool<HeroSkill>(() => new SoldierSkill());
      //pools[Define.HERO_TEMPLATE_BEAR + 1] = new ObjectPool<HeroSkill>(() => new BearSkill()); //베어 즉발기
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

        default:
          return null;
      }
    }

    // ======================
    //   1) Projectile 계열
    // ======================
    private HeroSkill SpawnProjectileSkill(Hero owner, Vector3 dir, Vector3 targetPos, GameRoom room, int skillTemplateId)
    {
      if (!pools.TryGetValue(skillTemplateId, out var pool))
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
      dir = dir.LengthSquared() > 0.0001f ? Vector3.Normalize(dir) : owner.Direction;

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
    private void ExecuteAreaExplosion(Hero owner, Vector3 targetPos, GameRoom room, HeroSkillData data )
    {
      if (owner == null || room == null || data == null) return;
      if (owner.ObjectID < 0) return;

      float delay = data.DelayTime;  

      room.PushAfter((int)(delay * 1000), () =>
      {

        Vector3 center = targetPos;
        if (center.LengthSquared() < 1e-6f)
          center = owner.Position;
        center.Y = 0f;

        // owner/room 유효성 재확인(딜레이 동안 despawn 될 수 있음)
        if (owner == null || owner.ObjectID < 0) 
          return;

        float radius = data.Radius > 0 ? data.Radius : 3.0f;
        float radiusSq = radius * radius;

        int damage = owner.AttackDamage;
        float ccDuration = data.CcDuration; 
        float ccPower = data.CcPower;

        var targets = room.creatures.Values.ToArray();

        foreach (var c in targets)
        {
          if (c == null || c.ObjectID == owner.ObjectID) continue;

          Vector3 delta = c.Position - center;
          delta.Y = 0f;
          if (delta.LengthSquared() > radiusSq) continue;

          switch (data.SubSkillType)
          {
            case EHeroSubSkillType.EskillSubtypeNone:
              c.OnDamageBasic(damage, owner);
              break;

            case EHeroSubSkillType.EskillSubtypeKnockback:
              {
                Vector3 knockDir = c.Position - center; // targetPos 말고 center 기준 추천
                knockDir.Y = 0;
                if (knockDir.LengthSquared() > 1e-6f)
                  knockDir = Vector3.Normalize(knockDir);

                c.OnDamageKnockback(damage, knockDir, ccPower, owner);
                break;
              }

            case EHeroSubSkillType.EskillSubtypeStun:
              c.OnDamageStun(damage, ccDuration, owner);
              break;

            case EHeroSubSkillType.EskillSubtypeAirborne:
              c.OnDamageAirborne(damage, ccPower, ccDuration, owner);
              break;
          }
        }

        // 2) (선택) 이 “히트 시점”에 이펙트도 같이 Broadcast
        room.Broadcast(new S_SkillEvent
        {
          OwnerId = owner.ObjectID,
          TemplateId = data.TemplateId,
          PosInfo = new PositionInfo { PosX = center.X, PosY = 0.1f, PosZ = center.Z }
        });
      });
    }

    public void ReturnSkill(HeroSkill skill)
    {
      if (skill == null)
        return;

      int key = skill.TempleteID;

      if (key != 0 && pools.TryGetValue(key, out var pool))
        pool.Return(skill);
      else
        _defaultPool.Return(skill);
    }
  }
}
