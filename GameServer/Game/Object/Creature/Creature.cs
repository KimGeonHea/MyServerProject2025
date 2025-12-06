using GameServer.Game.Object.Creature;
using GameServer.Game.Room;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class Creature : BaseObject
  {
    public virtual Vector3 ColliderPosition { get; set; }
    public virtual float ColliderRadius { get; set; } = 0.5f;
    public int MaxHp { get; protected set; }
    public int CurHp { get; protected set; }

    public int AttackDamage { get; protected set; }
    public int Defence { get; protected set; }
    public float MoveSpeed { get; protected set; }
    public float KnockbackResist { get; protected set; }
    public float AirborneResist { get; protected set; }
    public bool ImmuneKnockback { get; set; }
    public bool ImmuneAirborne { get; set; }

    // CC 타이머 & 넉백
    protected float stunRemain;
    protected float airbornRemain;

    protected bool isKnockback;
    protected Vector3 knockStart, knockEnd;
    protected float knockTime, knockElapsed;

    // ===== 공통 CC / 데미지 =====
    public virtual void OnDamage(DamageContext ctx)
    {
      if (IsDead) return;

      CurHp = Math.Clamp(CurHp - ctx.Amount, 0, MaxHp);
      OnHpChanged();

      if (CurHp <= 0)
      {
        Die(ctx.Attacker);
        return;
      }

      switch (ctx.SubType)
      {
        case EHeroSubSkillType.EskillSubtypeStun:
          ApplyStun(ctx.Duration);
          break;
        case EHeroSubSkillType.EskillSubtypeAirborne:
          ApplyAirborn(ctx.Power, ctx.Duration);
          break;
        case EHeroSubSkillType.EskillSubtypeKnockback:
          ApplyKnockback(ctx);
          break;
      }
    }

    public bool IsDead => CurHp <= 0;

    public override void FixedUpdate(float deltatime)
    {
      UpdateCC(deltatime);
      //CC구현//
    }

    protected virtual void UpdateCC(float dt) {  }
    protected virtual void ApplyStun(float duration) {  }
    protected virtual void ApplyAirborn(float height, float hang) { }
    protected virtual void ApplyKnockback(in DamageContext ctx) {  }

    protected virtual void Die(BaseObject killer)
    {
      if (IsDead == false)
        CurHp = 0;
      OnDead(killer);
    }

    // === 패킷/이벤트용 Hook ===
    protected virtual void OnHpChanged() 
    {
      if (Room == null)
        return;

      S_HeroChangeHp hpPkt = new S_HeroChangeHp
      {
        ObjectId = this.ObjectID,
        CurHp = this.CurHp,
        MaxHp = this.MaxHp
      };

      Room.Broadcast(hpPkt);
    }
    protected virtual void OnDead(BaseObject killer) { }


    // 순수 데미지
    public void OnDamageBasic(int amount, BaseObject attacker = null, bool crit = false)
      => OnDamage(new DamageContext(
           amount: amount,
           isCritical: crit,
           subType: EHeroSubSkillType.EskillSubtypeNone,
           power: 0f,
           duration: 0f,
           dir: null,
           attacker: attacker));

    public void OnDamageKnockback(
  int amount,
  Vector3 dir,
  float dist,
  BaseObject attacker = null,
  bool crit = false)
    {
      Vector3 useDir = dir;
      if (useDir.LengthSquared() > 1e-4f)
        useDir = Vector3.Normalize(useDir);
      else if (attacker != null)
      {
        useDir = Position - attacker.Position;
        useDir.Y = 0;
        if (useDir.LengthSquared() > 1e-6f)
          useDir = Vector3.Normalize(useDir);
      }

      OnDamage(new DamageContext(
        amount: amount,
        isCritical: crit,
        subType: EHeroSubSkillType.EskillSubtypeKnockback,
        power: dist,
        duration: 0f,
        dir: useDir,
        attacker: attacker));
    }
    // 스턴
    public void OnDamageStun(
      int amount,
      float sec,
      BaseObject attacker = null,
      bool crit = false)
      => OnDamage(new DamageContext(
           amount: amount,
           isCritical: crit,
           subType: EHeroSubSkillType.EskillSubtypeStun,
           power: 0f,
           duration: sec,
           dir: null,
           attacker: attacker));

    // 에어본 (height + hangTime)
    public void OnDamageAirborne(
      int amount,
      float height,
      float hangTime = 0.1f,
      BaseObject attacker = null,
      bool crit = false,
      Vector3? dir = null)
      => OnDamage(new DamageContext(
           amount: amount,
           isCritical: crit,
           subType: EHeroSubSkillType.EskillSubtypeAirborne,
           power: height,         // Power = 높이로 재사용
           duration: hangTime,
           dir: dir,
           attacker: attacker));
  }
}

