using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Object.Creature
{
  public class Tower : BaseObject
  {
    public int MaxHp { get; private set; }
    public int Hp { get; private set; }

    public Hero owner { get; private set; }
    public ETeamType teamType { get; private set; }

    public void Init(ETeamType teamType, int maxHp, Hero owner = null)
    {
      ObjectType = EGameObjectType.Tower;

      this.teamType = teamType;
      this.owner = owner;

      MaxHp = maxHp;
      Hp = maxHp;
    }

    public void OnDamaged(int damage)
    {
      Hp = Math.Max(0, Hp - damage);

      // 타워 HP 브로드캐스트
      //Room?.BroadcastTowerHp(this);
      //
      //if (Hp <= 0)
      //{
      //  Room?.OnTowerDestroyed(this);
      //}
    }

    public bool IsDead() => Hp <= 0;
  }
}
