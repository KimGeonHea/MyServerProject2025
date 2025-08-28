using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class SelctHero : Hero
  {

    public SelctHero(Hero baseHero)
    {
      this.HeroDbId = baseHero.HeroDbId;
      this.HeroStatInfo = baseHero.HeroStatInfo;
      // 필요한 속성 복사
    }

    public SelctHero()
    {

    }

  }


}
