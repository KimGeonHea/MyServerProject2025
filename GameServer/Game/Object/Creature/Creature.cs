using GameServer.Game.Room;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public readonly struct DamageContext
  {
    public readonly int Amount;
    public readonly bool IsCritical;
    public readonly EHeroSubSkillType SubType;
    public readonly float Duration;       // Stun/Airborn 시간
    public readonly float Power;          // Knockback 거리 or Airborn 높이
    public readonly Vector3 Direction;    // Knockback 방향
    public readonly BaseObject Attacker; //현재 어택커 들고 있지만 TODO objectID로 가지고 있는게 맞음

    public DamageContext(
      int amount,
      bool isCritical = false,
      EHeroSubSkillType subType = EHeroSubSkillType.EskillSubtypeNone,
      float power = 0f,
      float duration = 0f,
      Vector3? dir = null,
      BaseObject attacker = null)
    {
      Amount = amount;
      IsCritical = isCritical;
      SubType = subType;
      Power = power;
      Duration = duration;
      Direction = dir ?? Vector3.Zero;
      Attacker = attacker;
    }
  }

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
    protected float knockbackDist;

    // ===== 공통 CC / 데미지 =====
    public virtual void OnDamage(DamageContext ctx)
    {
      if (IsDead) return;

      CurHp = Math.Clamp(CurHp - ctx.Amount, 0, MaxHp);
      UpdateHp();

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
    }

    protected virtual void UpdateCC(float dt) {  }
    protected virtual void ApplyStun(float duration) 
    {
      float dur = MathF.Max(0.05f, duration);
      stunRemain = dur;   // Creature 쪽 protected 필드
      
    }
    protected virtual void ApplyAirborn(float height, float hang) 
    {
      float rawH = height > 0f ? height : 1.5f;
      float rawHang = hang > 0f ? hang : 1.0f;

      // 에어본 저항 적용 
      float remain = Math.Clamp(1f - AirborneResist, 0f, 1f);
      float effH = rawH * remain;
      float effHang = rawHang * remain;

      if (effH <= 0.01f || effHang <= 0.01f)
        return;

      airbornRemain = effHang;
    }

    protected virtual void ApplyKnockback(in DamageContext ctx) 
    {
      if (ImmuneKnockback)
        return;

      float dist = ctx.Power;
      if (KnockbackResist > 0f)
        dist *= MathF.Max(0f, 1f - KnockbackResist);

      if (dist <= 0f)
        return;

      Vector3 dir = ctx.Direction;
      dir.Y = 0f;

      if (dir.LengthSquared() < 1e-4f)
        return;                // 방향 정보가 없다  넉백 스킵

      dir = Vector3.Normalize(dir);

      var grid = DataManager.ObstacleGrid;

      Vector3 start = Position;
      start.Y = 0f;
      Vector3 lastValid = start;

      ///스텝 사이즈///
      ///
      //넉백시 벽을 통과하는 문제를 해결하기위해 사용//
      float stepSize = 0.15f;
      int steps = (int)MathF.Ceiling(dist / stepSize);
      if (steps <= 0) steps = 1;

      for (int i = 1; i <= steps; i++)
      {
        float d = stepSize * i;
        if (d > dist)
          d = dist;

        Vector3 p = start + dir * d;

        if (grid.IsBlockedXZ(p))
          break;

        lastValid = p;
      }

      Vector3 final = lastValid;
      final.Y = Position.Y;
      Position = final;
    }

    protected virtual void Die(BaseObject killer)
    {
      if (IsDead == false)
        CurHp = 0;
      OnDead(killer);
    }

    // === 패킷/이벤트용 Hook ===
    protected virtual void UpdateHp() 
    {
      GameRoom room = this.Room as GameRoom;

      if (room == null || Room == null)
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

    public void OnDamageKnockback(int amount,Vector3 dir,float dist,BaseObject attacker = null,bool crit = false)
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
    public void OnDamageStun(int amount,float sec,BaseObject attacker = null,bool crit = false)
      => OnDamage(new DamageContext(
           amount: amount,
           isCritical: crit,
           subType: EHeroSubSkillType.EskillSubtypeStun,
           power: 0f,
           duration: sec,
           dir: null,
           attacker: attacker));

    // 에어본 (height + hangTime)
    public void OnDamageAirborne(int amount,float height,float hangTime = 0.1f, BaseObject attacker = null,
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

