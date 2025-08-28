using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class InvenHero
  {
    public Dictionary<int, Hero> allHeroes { get; set; } = new Dictionary<int, Hero>();

    public Hero selectHero = new Hero();

    public InvenHero(Player owner) 
    {
      Owner = owner;
    }
    public Player Owner { get; set; }


    

    public void Init(PlayerDb playerdb)
    {
      var list = playerdb.Heros.ToList();

      foreach (var heroDb in list)
      {
        Hero hero = new Hero();
        hero.Init(heroDb);
 
        Add(hero);
      }
    }


    public void EquipSelectHero(int heroDbId)
    {
      Hero hero = GetHeroByDbId(heroDbId);
      if (hero == selectHero)
        return;

      if (hero != null)
      {
        Hero prevSelectHero = selectHero; // 이전 선택 저장

        if (prevSelectHero != null)
          prevSelectHero.Slot = 1;

        hero.Slot = 0;
        selectHero = hero;

        Player owner = Owner;
        owner.selectHero = selectHero;

        if (owner != null)
        {
          // prevSelectHero와 hero를 함께 저장
          DBManager.EquipHeroNoti(owner, hero, prevSelectHero);
        }

        SendChangeItemSlotPacket(owner, hero);
      }
    }



    public void Add(Hero hero)
    {
      if (hero.HeroStatInfo.ItemSlotType.Equals(0))
      {
        //SelctHero sHero = new SelctHero(hero);
        selectHero = hero;
        Owner.selectHero = hero;
      }

      allHeroes.Add(hero.HeroDbId, hero);
    }

    public Hero GetHeroByDbId(int heroDbId)
    {
      Hero hero = null;
      if (allHeroes.TryGetValue(heroDbId, out hero))
        return hero;
      else return null;
    }
   
    public List<HeroStatInfo> GetAllHeroInfos()
    {
      return allHeroes.Values.Select(i => i.HeroStatInfo).ToList();
    }

    public void SendChangeItemSlotPacket(Player owner , Hero hero)
    {
      S_SelctHero packet = new S_SelctHero();
      packet.HeroDbId = hero.HeroDbId;

      owner.Session?.Send(packet);
    }

  }
}
