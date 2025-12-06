using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;



namespace GameServer.Game
{
  public partial class Hero : Creature
  {
    bool staminaDirty = false;
    public override void FixedUpdate(float deltaTime)
    {
      base.FixedUpdate(deltaTime);

      if (IsDead)
        return;
      TickStamina(deltaTime);
      // 2) 하드 CC(스턴/에어본) 중이면 이동 X
      if (stunRemain > 0f || airbornRemain > 0f || isKnockback)
        return;

      // 3) 입력 방향으로 이동
      ApplyMove(MoveDir, MoveSpeed, deltaTime);

    }
    public override void ApplyMove(Vector3 dir, float speed, float deltaTime)
    {
      Vector3 cleanDir = new Vector3(dir.X, 0, dir.Z);

      if (cleanDir.LengthSquared() < 0.001f)
      {
        return;
      }
      Vector3 normalizedDir = Vector3.Normalize(cleanDir);
      Vector3 delta = normalizedDir * speed * deltaTime;
      Vector3 newPos = new Vector3(PosInfo.PosX + delta.X, 0, PosInfo.PosZ + delta.Z);
      //충돌체크
      var grid = DataManager.ObstacleGrid;
      if (grid.IsBlockedXZ(newPos))
        return;


      if (EHeroLowerState == EHeroLowerState.Eindle)
      {
        PosInfo.DirX = MoveDir.X;
        PosInfo.DirY = 0;
        PosInfo.DirZ = MoveDir.Z;
        PosInfo.PosX = Position.X;
        PosInfo.PosY = 0;
        PosInfo.PosZ = Position.Z;
      }
      else
      {
        PosInfo.DirX = normalizedDir.X;
        PosInfo.DirY = 0;
        PosInfo.DirZ = normalizedDir.Z;
        PosInfo.PosX = newPos.X;
        PosInfo.PosY = 0;
        PosInfo.PosZ = newPos.Z;
      }
    }


    // =========================
    //   Damage / CC 오버라이드
    // =========================

    public override void OnDamage(DamageContext ctx)
    {
      // 여기서 나중에 방어력/쉴드 계산 넣어도 됨
      base.OnDamage(ctx);
    }

    protected override void OnHpChanged()
    {
      base.OnHpChanged();
      //
      //if (Room == null)
      //  return;
      //
      //S_HeroChangeHp hpPkt = new S_HeroChangeHp
      //{
      //  ObjectId = this.ObjectID,
      //  CurHp = this.CurHp,
      //  MaxHp = this.MaxHp
      //};
      //
      //Room.Broadcast(hpPkt);
    }

    protected override void OnDead(BaseObject killer)
    {
      base.OnDead(killer);

      Hero killerHero = killer as Hero;
      if (killerHero != null)
      {
        // TODO: 킬 카운트, 점수, 골드 등
      }

      // TODO: 사망 처리 (리스폰, 게임 종료, S_HeroDie 브로드캐스트 등)
    }

    // =========================
    //      CC 구체 구현부
    // =========================

    // Stun: 일정 시간 동안 행동/이동 불가
    protected override void ApplyStun(float duration)
    {
      float dur = MathF.Max(0.05f, duration);
      stunRemain = dur;   // Creature 쪽 protected 필드

      // TODO: 상체/하체 상태 갱신, 스킬 취소 등
      // ex) EHeroUpperState = EHeroUpperState.Stun; 이런 거 있으면 여기서 바꾸기
      // TODO: 클라에 "스턴 걸림" 패킷 보내고 싶으면 여기서 Room.Broadcast(...)
    }

    // Airborn: 서버에선 "행동 불가 + 타이머" 정도만 들고가도 됨
    protected override void ApplyAirborn(float height, float hang)
    {
      float rawH = height > 0f ? height : 1.5f;
      float rawHang = hang > 0f ? hang : 1.0f;

      // 에어본 저항 적용 (필요 없으면 이 부분 삭제해도 됨)
      float remain = Math.Clamp(1f - AirborneResist, 0f, 1f);
      float effH = rawH * remain;
      float effHang = rawHang * remain;

      if (effH <= 0.01f || effHang <= 0.01f)
        return;

      airbornRemain = effHang;

      // TODO: 상체 상태를 "에어본" 같은 걸로 바꾸고,
      // 실제 y 이동/애니메이션은 클라에서 처리
      // ex) EHeroUpperState = EHeroUpperState.Airborn;
    }

    // Knockback: 짧은 시간 동안 Position을 보간 이동
    protected override void ApplyKnockback(in DamageContext ctx)
    {
      if (ImmuneKnockback)
        return;

      float rawDist = ctx.Power > 0f ? ctx.Power : 1.0f;
      float effDist = rawDist * (1f - KnockbackResist);
      if (effDist <= 0.01f)
        return;

      // 방향 결정
      Vector3 dir = ctx.Direction;
      if (dir.LengthSquared() < 1e-4f && ctx.Attacker != null)
      {
        Vector3 v = this.Position - ctx.Attacker.Position;
        v.Y = 0;
        if (v.LengthSquared() > 1e-6f)
          dir = Vector3.Normalize(v);
      }
      else if (dir.LengthSquared() > 1e-4f)
      {
        dir = Vector3.Normalize(dir);
      }
      else
      {
        dir = new Vector3(0, 0, 1); // 최후 보정
      }

      knockStart = Position;
      knockEnd = Position + dir * effDist;
      knockTime = 0.12f;
      knockElapsed = 0f;
      isKnockback = true;
    }

    protected override void UpdateCC(float dt)
    {
      // Stun 타이머
      if (stunRemain > 0f)
      {
        stunRemain -= dt;
        if (stunRemain <= 0f)
        {
          stunRemain = 0f;
          // TODO: 스턴 해제  상태 복구
          // ex) EHeroUpperState = EHeroUpperState.Normal;
        }
      }

      // Airborn 타이머
      if (airbornRemain > 0f)
      {
        airbornRemain -= dt;
        if (airbornRemain <= 0f)
        {
          airbornRemain = 0f;
          // TODO: 에어본 해제  상태 복구
        }
      }

      // Knockback 보간
      if (isKnockback)
      {
        knockElapsed += dt;
        float t = knockTime <= 0f ? 1f : knockElapsed / knockTime;
        if (t >= 1f)
        {
          t = 1f;
          isKnockback = false;
        }

        Position = Vector3.Lerp(knockStart, knockEnd, t);

        // TODO: 히어로 위치 브로드캐스트 패킷 (S_HeroMove 같은 거) 보내고 싶으면 여기서
      }
    }

    private void TickStamina(float deltaTime)
    {
      if (CurStamina >= MaxStamina)
        return;

      // 클라랑 같은 개념: 초당 절대 회복량
      CurStamina = MathF.Min(MaxStamina, CurStamina + StaminaRegenSpeed * deltaTime);
      staminaDirty = true;
    }
    public bool TryConsumeStamina(float amount)
    {
      if (CurStamina < amount)
        return false;

      CurStamina -= amount;
      staminaDirty = true;
      return true;
    }
    private void BroadcastHeroStateAndPos()
    {
      if (Room == null)
        return;

      S_HeroMove pkt = new S_HeroMove
      {
        HeroInfo = new HeroInfo
        {
          ObjectInfo = new ObjectInfo
          {
            ObjectId = this.ObjectID,
            TemplateId = this.TempleteID,
            ObjectType = this.ObjectType
          },
          UpperState = this.EHeroUpperState,
          LowerState = this.EHeroLowerState,
          PosInfo = new PositionInfo
          {
            PosX = this.PosInfo.PosX,
            PosY = this.PosInfo.PosY,
            PosZ = this.PosInfo.PosZ,
            DirX = this.PosInfo.DirX,
            DirY = this.PosInfo.DirY,
            DirZ = this.PosInfo.DirZ
          }
        }
      };

      Room.Broadcast(pkt);
    }
  }
}
