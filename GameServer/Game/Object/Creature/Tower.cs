using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class Tower : Creature
  {
    public Hero owner { get; private set; }
    public ETeamType teamType { get; private set; }

    public void Init(ETeamType teamType, int maxHp, Hero owner = null)
    {
      ObjectType = EGameObjectType.Tower;

      this.teamType = teamType;
      this.owner = owner;

      MaxHp = maxHp;
      CurHp = maxHp;
    }
  }
}
