using Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameServer.Game;
using GameServer;
using Server.Game;
using Google.Protobuf.Protocol;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using GameServer.Game.Room;
using ServerCore;
using Server.Data;

namespace GameServer
{
  public class Player
  {

    public Room Room { get; set; }
    public int PlayerDbId { get; set; }
    public int ObjectID
    {
      get { return objectId; }
      set { objectId = value; }
    }

    public int objectId { get;set; }

    public PlayerStatInfo playerStatInfo { get; set; }  = new PlayerStatInfo();
   
    public ClientSession Session { get; set; }

    public Inventory inventory { get; set; }
    public InvenHero invenHero { get; set; }

    public Hero selectHero { get; set; }  

    public DateTime LastDailyRewardTime;
    public int WeeklyRewardFlags { get; set; } = 0; 

    public static Player MakePlayer(ClientSession session , PlayerDb playerDb)
    {
      Player player = new Player();
      player.Session = session;
      player.Init(playerDb);

      return  player;
    }

    public void Init(PlayerDb playerDb)
    {
      PlayerDbId = playerDb.PlayerDbId;
      playerStatInfo.Level = playerDb.Level;
      playerStatInfo.Exp = playerDb.Exp;
      playerStatInfo.TotalExp = playerDb.TotalExp;
      playerStatInfo.Gold = playerDb.Gold;
      playerStatInfo.Daimond = playerDb.Diamond;
      playerStatInfo.PlayerName = playerDb.PlayerName;

      inventory = new Inventory(this);
      invenHero = new InvenHero(this);
      
      invenHero.Init(playerDb);
      inventory.Init(playerDb);


      LastDailyRewardTime = playerDb.LastDailyRewardTime;
      WeeklyRewardFlags = playerDb.WeeklyRewardFlags;

    }
    public void OnRoomChanged(LobbyRoom newRoom)
    {
      BroadcastPlyerInternalEvent(EHeroInternalEventType.TravelToRoom, 0, newRoom.MapTemplateId, 1);
    }
    public void BroadcastPlyerInternalEvent(EHeroInternalEventType type, long targetId = 0, int templateId = 0, int count = 0)
    {
      OnBroadcastHeroInternalEvent(type, targetId, templateId, count);
    }

    public void OnBroadcastHeroInternalEvent(EHeroInternalEventType type, long targetId, int templateId, int count)
    {
      switch (type)
      {
        case EHeroInternalEventType.LevelUp:
          OnLevelup();
          break;
        case EHeroInternalEventType.CollectItem:
          OnReward();
          break;

      }
    }

    private void OnReward()
    {

    }

    public void ApplyAddOrDeleteGoldDiaEnergy(ERewardType type , int count)
    {
      S_DailyReward packet = new S_DailyReward();
      switch (type)
      {
        case ERewardType.ErwardTypeGold:
          packet.Gold = count;
          break;

        case ERewardType.ErwardTypeDiamod:
          packet.Diamond = count;
          break;
      }
      
      packet.DailyRewardOpen = WeeklyRewardFlagToString(WeeklyRewardFlags);
      packet.LastDailyRewardTime = LastDailyRewardTime.ToString();

      Console.WriteLine(Convert.ToString(WeeklyRewardFlags, 2).PadLeft(7, '0'));

      Session?.Send(packet);
    }

    public string WeeklyRewardFlagToString(int flags)
    {
      char[] s = new char[7];
      for (int i = 0; i < 7; i++)
        s[i] = ((flags >> i) & 1) == 1 ? '1' : '0'; // 비트0이 s[0](왼쪽)
      return new string(s);
    }
    public string WeeaklyRewardFlagTostring(int flags)
    {
      char[] result = new char[7];
      for (int i = 0; i < 7; i++)
        result[i] = (flags & (1 << (6 - i))) != 0 ? '1' : '0';  // 6-i로 순서 변경
      return new string(result);
    }
    private void OnLevelup()
    {
      // 1. 레벨에 맞는 BaseStat 적용
      //StatComp.InitHeroStat(HeroInfoComp.Level);
      //// 3. 장비보너스 적용
      //Inven.ApplyEquipmentBonus(false);
      //// 5. 장식품보너스 적용
      //CosmeticComp.ApplyEquipmentBonus();
      //// 4. 기존에 걸려 있던 이펙트 적용
      //EffectComp.ApplyEffectsAgain();
      //SendRefreshStat();
    }


    //public static player MakeHero(HeroDb heardb)
    //{
    //  player hero = new player();
    //  hero.TemplatedId = heardb.TemplateId;
    //  hero = heardb.PlayerDbId ?? throw new InvalidOperationException("PlayerDbId is null");
    //  hero.HeroDbId = heardb.HeroDbId;
    //  hero.Slot = heardb.Slot;
    //
    //  HeroData herodata = null;
    //  DataManager.heroDict.TryGetValue(hero.TemplatedId, out herodata);
    //  if (herodata != null)
    //  {
    //    hero.heroData = herodata;
    //  }
    //
    //  return hero;
    //}
  }
}
