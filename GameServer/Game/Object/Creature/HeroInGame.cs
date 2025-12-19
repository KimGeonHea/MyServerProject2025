using GameServer.Game.Heromove;
using GameServer.Game.Object;
using GameServer.Utils;
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
    /// <summary>
    /// 이동 인풋
    /// </summary>
    public Vector3 MoveInputDir { get; set; } = Vector3.Zero; 

    private IHeroSkillHandler skillHandler;

    bool staminaDirty = false;


    float upperStateRemain = 0f; // 0이면 Enone
    bool UpperStateLocked; // 상체 
    float lowerStateRemain = 0f;  // 0이면 Enone
    bool LowerStateLocked; //하체


    public bool IsPersistence;

    public void InitMoveTempleteId(int templateId)
    {
      TempleteID = templateId;

      if (templateId == Define.HERO_TEMPLATE_BEAR)
        skillHandler = new BearHeroSkillHandler();
      else
        skillHandler = new HeroSkillHandler();
    }
    public bool HandleBasicAttack(Vector3 dir, Vector3 startPos, out HeroBullet spawned)
    {
      spawned = null;

      if (skillHandler == null)
        return false;

      return skillHandler.OnBasicAttack(this, dir, startPos, out spawned);
    }

    public bool HandleSkillPress(Vector3 dir, Vector3 targetPos, out HeroSkill spawnedSkill)
    {
      spawnedSkill = null;

      if (skillHandler == null)
        return false;

      return skillHandler.OnSkillAttack(this, dir, targetPos, out spawnedSkill);
    }

    public void HandleSkillRelease()
    {
      if (skillHandler == null)
        return;

      skillHandler.OnSkillRelease(this);
    }

    public bool IsCC()
    {
      return stunRemain > 0f || airbornRemain > 0f || isKnockback;
    }
    //상체 수명 잠금//
    public void LockUpper(EHeroUpperState st)
    {
      EHeroUpperState = st;
      upperStateRemain = 0f;
      UpperStateLocked = true;
    }
    public void UnlockUpper()
    {
      UpperStateLocked = false;
      ClearUpperState();
    }
    // 상체 수명 관리 일반적으로 O
    public void SetUpperState(EHeroUpperState st, float durationSec)
    {
      EHeroUpperState = st;
      upperStateRemain = MathF.Max(0f, durationSec);
    }
    public void ClearUpperState()
    {
      EHeroUpperState = EHeroUpperState.Enone;
      upperStateRemain = 0f;
    }


    public void LockLower(EHeroLowerState st)
    {
      EHeroLowerState = st;
      lowerStateRemain = 0f;
      LowerStateLocked = true;
    }

    public void UnlockLower()
    {
      LowerStateLocked = false;
      ClearLowerState();
    }

    public void SetLowerState(EHeroLowerState st, float durationSec)
    {
      if (LowerStateLocked) return;
      EHeroLowerState = st;
      lowerStateRemain = MathF.Max(0f, durationSec);
    }

    public void ClearLowerState()
    {
      EHeroLowerState = EHeroLowerState.Eindle;
      lowerStateRemain = 0f;
    }


    public override void FixedUpdate(float deltaTime)
    {
      base.FixedUpdate(deltaTime);

      if (IsDead) 
        return;

      // 1) 핸들러 틱 (불곰 불꽃 갱신/스태미너 소모 등)
      skillHandler?.Tick(this, deltaTime);

      // 2) 상체 상태 수명은 CC여도 감소해야 함 (중요)
      if (!UpperStateLocked && upperStateRemain > 0f)
      {
        upperStateRemain -= deltaTime;
        if (upperStateRemain <= 0f)
          ClearUpperState(); // Enone 복귀
      }

      if (IsCC())
      {
        MoveInputDir = Vector3.Zero; 
        if (!LowerStateLocked)
          EHeroLowerState = EHeroLowerState.Eindle;
      }
      else
      {
        Vector3 inputXZ = new Vector3(MoveInputDir.X, 0, MoveInputDir.Z);

        if (inputXZ.LengthSquared() >= 1e-4f)
        {
          ApplyMove(MoveInputDir, MoveSpeed, deltaTime);

          if (!LowerStateLocked)
            EHeroLowerState = EHeroLowerState.Emove;

          if (!UpperStateLocked && EHeroUpperState == EHeroUpperState.Enone)
            Direction = Vector3.Normalize(inputXZ);
        }
        else
        {
          if (!LowerStateLocked)
            EHeroLowerState = EHeroLowerState.Eindle;
        }
      }

      // 6) PosInfo.Dir은 송출만 (MoveDir이 단일 진실)
      if (Direction.LengthSquared() >= 1e-6f)
      {
        PositionInfo.DirX = Direction.X;
        PositionInfo.DirY = 0;
        PositionInfo.DirZ = Direction.Z;
      }
    }

    public override void ApplyMove(Vector3 dir, float speed, float deltaTime)
    {
      Vector3 input = new Vector3(dir.X, 0, dir.Z);
      if (input.LengthSquared() < 0.001f) return;

      Vector3 nd = Vector3.Normalize(input);
      Vector3 delta = nd * speed * deltaTime;

      Vector3 cur = Position;
      Vector3 next = new Vector3(cur.X + delta.X, cur.Y, cur.Z + delta.Z);

      if (DataManager.ObstacleGrid.IsBlockedXZ(next))
        return;

      Position = next;
    }

    // =========================
    //   Damage / CC 오버라이드
    // =========================

    public override void OnDamage(DamageContext ctx)
    {
      // 여기서 나중에 방어력/쉴드 계산 넣어도 됨
      base.OnDamage(ctx);
    }

    protected override void UpdateHp()
    {
      base.UpdateHp();
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
      base.ApplyStun(duration);
      LockUpper(EHeroUpperState.Edizzy);
      LockLower(EHeroLowerState.Eindle);
      MoveInputDir = Vector3.Zero;
    }

    // Airborn: 서버에선 "행동 불가 + 타이머" 정도만 들고가도 됨
    protected override void ApplyAirborn(float height, float hang)
    {
      base.ApplyAirborn(height, hang);
      LockUpper(EHeroUpperState.Edizzy);
      LockLower(EHeroLowerState.Eindle);
      MoveInputDir = Vector3.Zero;
    }

    // Knockback: 짧은 시간 동안 Position을 보간 이동
    protected override void ApplyKnockback(in DamageContext ctx)
    {
      base.ApplyKnockback(ctx);
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
          UnlockUpper();
          UnlockLower();
        }
      }

      // Airborn 타이머
      if (airbornRemain > 0f)
      {
        airbornRemain -= dt;
        if (airbornRemain <= 0f)
        {
          airbornRemain = 0f;
          UnlockUpper();
          UnlockLower();
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

    public void TickStamina(float deltaTime)
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

    float _staminaSendAcc = 0f;
    int _lastSentStaminaInt = -1;


    public bool TryConsumeStaminaDirtyForSend(float dt, float intervalSec, out int cur, out int max)
    {
      cur = 0;
      max = 0;

      _staminaSendAcc += dt;

      if (!staminaDirty) return false;
      if (_staminaSendAcc < intervalSec) return false;

      int curInt = (int)CurStamina * 1000;
      int maxInt = (int)MaxStamina * 1000;

      // 같은 값이면 굳이 보낼 필요 없음
      if (curInt == _lastSentStaminaInt)
        return false;

      // 여기서 "보내기로 확정" 처리
      staminaDirty = false;
      _staminaSendAcc = 0f;
      _lastSentStaminaInt = curInt;

      cur = curInt;
      max = maxInt;
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
            PosX = this.PositionInfo.PosX,
            PosY = this.PositionInfo.PosY,
            PosZ = this.PositionInfo.PosZ,
            DirX = this.PositionInfo.DirX,
            DirY = this.PositionInfo.DirY,
            DirZ = this.PositionInfo.DirZ
          }
        }
      };

      Room.Broadcast(pkt);
    }
  }
}
