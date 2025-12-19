using GameServer.Game.Room;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class SoldierBullet : HeroBullet
  {
    // 클라값 맞추기 (나중에 데이터로 빼면 됨)
    float explosionRadius = 1.5f;
    bool useDamageFalloff = true;
    float minFalloff = 0.5f;

    public override void Init(Hero owner, Vector3 direction, Vector3 startPos)
    {
      base.Init(owner, direction, startPos);

      // 데이터에 폭발반경을 넣을 거면 여기서 덮어쓰기
      // 예: heroSkillData.Radius를 "폭발 반경"으로 쓸 경우:
      //if (heroSkillData != null && heroSkillData.Radius > 0)
      //  explosionRadius = heroSkillData.Radius;
    }
    public override void FixedUpdate(float deltaTime)
    {
      if (!IsAlive || Owner == null || Owner.Room == null)
        return;

      //base.FixedUpdate(deltaTime);

      CheckCollision();
      ApplyMove(Direction, Speed, deltaTime);

      //float maxRange = (Range > 0 ? Range : MAXDISTANCE);
      if (Vector3.Distance(startPosition, Position) > MAXDISTANCE)
        Owner.Room.Despawn(this);
    }
    protected override void CheckCollision()
    {
      GameRoom room = Owner.Room as GameRoom;
      if (room == null) return;

      Creature primary = null;
      float bestDistSq = float.MaxValue;

      // 1) 직격 대상 찾기 (가장 가까운 1명만)
      foreach (var c in room.creatures.Values)
      {
        if (c == null || c.ObjectID == Owner.ObjectID)
          continue;

        // TODO: 팀/아군 판정 있으면 여기서 스킵


        float totalR = Radius + c.ColliderRadius;
        Vector3 xzPos = new Vector3(Position.X, 0, Position.Z);
        Vector3 cxz = new Vector3(c.Position.X, 0, c.Position.Z);

        float distSq = Vector3.Distance(cxz, xzPos);
        distSq *= distSq;

        if (distSq <= totalR * totalR && distSq < bestDistSq)
        {
          bestDistSq = distSq;
          primary = c;
        }
      }

      if (primary == null)
        return;

      // 2) 직격 데미지
      primary.OnDamageBasic(damage, Owner);

      // 3) 폭발 AoE (primary 중복타격 방지)
      Explode(room, primary);

      // 4) 탄환 제거
      Owner.Room.Despawn(this);
    }

    void Explode(GameRoom room, Creature primary)
    {
      if (explosionRadius <= 0f)
        return;

      Vector3 center = primary.Position;
      Vector3 centerXZ = new Vector3(center.X, 0, center.Z);

      foreach (var c in room.creatures.Values)
      {
        if (c == null || c.ObjectID == Owner.ObjectID)
          continue;

        if (primary != null && c.ObjectID == primary.ObjectID)
          continue;

        // TODO: 팀/아군 판정 있으면 여기서 스킵

        Vector3 cxz = new Vector3(c.Position.X, 0, c.Position.Z);

        float d = Vector3.Distance(centerXZ, cxz);
        if (d > explosionRadius + c.ColliderRadius)
          continue;

        float scale = 1f;
        if (useDamageFalloff)
        {
          float t = MathF.Min(1f, MathF.Max(0f, d / explosionRadius));
          scale = Lerp(1f, minFalloff, t);
        }

        int aoeDamage = (int)MathF.Round(damage * scale);
        if (aoeDamage <= 0) continue;

        c.OnDamageBasic(aoeDamage, Owner);
      }
    }

    float Lerp(float a, float b, float t) => a + (b - a) * t;
  }
}
