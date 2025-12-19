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
  public class Monster : Creature
  {
    // ===== 팀 / 타워 =====
    public int Team { get; private set; }          // 0 / 1
    BaseObject _targetTower;                      // 내 타워만 바라봄

    // ===== 스탯 =====
    public float AttackRange { get; private set; }
    public float AttackInterval { get; private set; }

    public ECreatureState CreatureState { get; private set; } = ECreatureState.Idle;

    // ===== 공격/CC 타이머 =====
    float _attackTimer = 0f;

    // ===== 넉백 관련 상수 =====
    const float MIN_KNOCKBACK_DIST = 0.05f;
    const float MIN_AIRBORN_HEIGHT = 0.05f;
    const float MIN_AIRBORN_HANG = 0.03f;

    // ===== 초기화 =====
    public void Init(MonsterData data, int team, BaseObject enemyTower, Vector3 spawnPos)
    {
      Team = team;
      Room = enemyTower.Room;
      ObjectType = EGameObjectType.Monster;

      Position = spawnPos;
      _targetTower = enemyTower ?? throw new ArgumentNullException(nameof(enemyTower));

      MaxHp = data.Hp;
      CurHp = data.Hp;
      AttackDamage = data.Attack;
      Defence = data.Defense;

      MoveSpeed = data.MoveSpeed;
      AttackRange = data.AttackRange;
      AttackInterval = data.AttackDelay;

      KnockbackResist = Clamp01(data.KnockbackResistPct * 0.01f);
      AirborneResist = Clamp01(data.AirborneResistPct * 0.01f);

      ChangeState(ECreatureState.Moving);

      if (Room != null)
      {
        //S_SpawnMonster sSpawn = new S_SpawnMonster();
        //sSpawn.Monsters.Add(ObjectInfo);
        //Room.Broadcast(sSpawn);
      }
    }

    // GameRoom.FixedUpdate 에서 매 프레임 호출
    public override void FixedUpdate(float deltaTime)
    {
      if (IsDead) return;
      if (_targetTower == null) { Die(null); return; }

      // 1) CC/넉백 처리 (Creature의 FixedUpdate → UpdateCC 호출)
      base.FixedUpdate(deltaTime);

      // 2) 몬스터 상태머신
      switch (CreatureState)
      {
        case ECreatureState.Moving:
          if (!IsInHardCC())
            TickMove(deltaTime);
          break;

        case ECreatureState.Attack:
          if (!IsInHardCC())
            TickAttack(deltaTime);
          break;

        case ECreatureState.Dizzy:
        case ECreatureState.Airborn:
          // CC 동안은 이동/공격 안함 (타이머로 상태 복귀)
          break;

        case ECreatureState.Idle:
        case ECreatureState.Think:
          // 타워만 가면 되니까 기본은 Moving
          ChangeState(ECreatureState.Moving);
          break;

        case ECreatureState.Dead:
        default:
          break;
      }
    }

    // ===== Creature CC 처리 override =====
    protected override void UpdateCC(float dt)
    {
      // Stun(Dizzy)
      if (stunRemain > 0f)
      {
        stunRemain -= dt;
        if (stunRemain <= 0f && !IsDead)
        {
          stunRemain = 0f;
          ChangeState(ECreatureState.Moving);
        }
      }

      // Airborn
      if (airbornRemain > 0f)
      {
        airbornRemain -= dt;
        if (airbornRemain <= 0f && !IsDead)
        {
          airbornRemain = 0f;
          ChangeState(ECreatureState.Moving);
        }
      }

      // Knockback
      if (isKnockback)
        UpdateKnockback(dt);
    }

    protected override void ApplyStun(float duration)
    {
      float dur = Math.Max(0.05f, duration);
      stunRemain = dur;
      ChangeState(ECreatureState.Dizzy);
    }

    protected override void ApplyAirborn(float height, float hang)
    {
      if (ImmuneAirborne)
        return;

      float rawH = height > 0f ? height : 1.5f;
      float rawHang = hang > 0f ? hang : 1.0f;

      float remain = Clamp01(1f - AirborneResist);
      float effH = rawH * remain;
      float effHang = rawHang * remain;

      if (effH < MIN_AIRBORN_HEIGHT || effHang < MIN_AIRBORN_HANG)
        return;

      airbornRemain = effHang;
      ChangeState(ECreatureState.Airborn);
      // 실제 y로 띄우는 건 클라 애니에서 처리
    }

    protected override void ApplyKnockback(in DamageContext ctx)
    {
      base.ApplyKnockback(ctx);

      if (ImmuneKnockback)
        return;

      float rawDist = ctx.Power > 0f ? ctx.Power : 1.0f;
      float effDist = rawDist * (1f - KnockbackResist);

      if (effDist < MIN_KNOCKBACK_DIST)
        return;

      Vector3 dir = ResolveDir(ctx.Direction, ctx.Attacker);
      knockStart = Position;
      knockEnd = Position + dir * effDist;
      knockTime = 0.12f;
      knockElapsed = 0f;
      isKnockback = true;
    }

    void UpdateKnockback(float dt)
    {
      knockElapsed += dt;
      float t = knockTime <= 0f ? 1f : knockElapsed / knockTime;
      if (t >= 1f)
      {
        t = 1f;
        isKnockback = false;
      }

      Position = Vector3.Lerp(knockStart, knockEnd, t);

      if (Room != null)
      {
        //S_MonsterMove sMove = new S_MonsterMove
        //{
        //  ObjectId = ObjectID,
        //  PosInfo = PosInfo
        //};
        //Room.Broadcast(sMove);
      }
    }

    // ===== 상태 전환 & 상태 체크 =====
    void ChangeState(ECreatureState newState)
    {
      if (CreatureState == newState)
        return;

      CreatureState = newState;

      if (newState == ECreatureState.Attack)
        _attackTimer = 0f;

      if (Room != null)
      {
        //S_MonsterState s = new S_MonsterState
        //{
        //  ObjectId = ObjectID,
        //  State = CreatureState
        //};
        //Room.Broadcast(s);
      }
    }

    bool IsInHardCC()
      => CreatureState == ECreatureState.Dizzy
      || CreatureState == ECreatureState.Airborn;

    // ===== 타워 방향으로 이동 =====
    void TickMove(float dt)
    {
      Vector3 toTower = _targetTower.Position - Position;
      toTower.Y = 0;
      float dist = toTower.Length();

      // 사거리 안이면 공격
      if (dist <= AttackRange)
      {
        ChangeState(ECreatureState.Attack);
        return;
      }

      // 넉백 중이면 넉백 우선
      if (isKnockback)
      {
        UpdateKnockback(dt);
        return;
      }

      if (dist > 0.0001f)
      {
        Vector3 dir = toTower / dist;
        Position += dir * MoveSpeed * dt;

        if (Room != null)
        {
          //S_MonsterMove sMove = new S_MonsterMove
          //{
          //  ObjectId = ObjectID,
          //  PosInfo = PosInfo
          //};
          //Room.Broadcast(sMove);
        }
      }
    }

    // ===== 타워 공격 =====
    void TickAttack(float dt)
    {
      Vector3 toTower = _targetTower.Position - Position;
      toTower.Y = 0;
      if (toTower.LengthSquared() > AttackRange * AttackRange)
      {
        ChangeState(ECreatureState.Moving);
        return;
      }

      _attackTimer += dt;
      if (_attackTimer < AttackInterval)
        return;

      _attackTimer -= AttackInterval;

      if (_targetTower is ITower tower)
        tower.OnDamaged(AttackDamage, this);

      if (Room != null)
      {
        //S_MonsterAttack sAtk = new S_MonsterAttack
        //{
        //  AttackerId = ObjectID,
        //  TargetId   = _targetTower.ObjectID
        //};
        //Room.Broadcast(sAtk);
      }
    }

    // ===== Creature Hook들 =====
    protected override void UpdateHp()
    {
      base.UpdateHp();

      if (Room != null)
      {
        //S_MonsterHp sHp = new S_MonsterHp
        //{
        //  ObjectId = ObjectID,
        //  Hp       = CurHp,
        //  MaxHp    = MaxHp
        //};
        //Room.Broadcast(sHp);
      }
    }

    protected override void OnDead(BaseObject killer)
    {
      base.OnDead(killer);

      ChangeState(ECreatureState.Dead);

      if (Room != null)
      {
        //S_MonsterDie sDie = new S_MonsterDie { ObjectId = ObjectID };
        //Room.Broadcast(sDie);
        //(Room as GameRoom)?.OnMonsterDead(this, killer);
      }
    }

    // ===== 유틸 =====
    static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);

    static Vector3 ResolveDir(Vector3 dir, BaseObject attacker)
    {
      if (dir.LengthSquared() > 1e-4f)
        return Vector3.Normalize(dir);

      if (attacker != null)
      {
        Vector3 v = PositionFrom(attacker) - attacker.Position;
        v.Y = 0;
        if (v.LengthSquared() > 1e-6f)
          return Vector3.Normalize(v);
      }
      return new Vector3(0, 0, 1);
    }

    static Vector3 PositionFrom(BaseObject obj) => obj.Position;
  }

  public interface ITower
  {
    bool IsDead { get; }
    Vector3 Position { get; }
    void OnDamaged(int damage, BaseObject attacker);
  }
}

