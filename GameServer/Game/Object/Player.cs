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

    public int objectId { get; set; }
    public int ObjectID
    {
      get { return objectId; }
      set { objectId = value; }
    }
    public int Gold
    {
      get { return playerStatInfo.Gold; }
      set { playerStatInfo.Gold = value; }
    }
    public int Diamond
    {
      get { return playerStatInfo.Daimond; }
      set { playerStatInfo.Daimond = value; }
    }

    public int Energy
    {
      get { return playerStatInfo.Energy; }
      set { playerStatInfo.Energy = value; }
    }

    public int Exp
    {
      get { return playerStatInfo.Exp; }
      set { playerStatInfo.Exp = value; }
    }
    public int TotalExp
    {
      get { return playerStatInfo.TotalExp; }
      set { playerStatInfo.TotalExp = value; }
    }

    public int Level
    {
      get { return playerStatInfo.Level; }
      set { playerStatInfo.Level = value; }
    }

    public string Stagename
    {
      get { return playerStatInfo.StageName; }
      set { playerStatInfo.StageName = value; }
    }
    public int CurStageOrderIndex
    {
      get
      {
        if (!string.IsNullOrEmpty(Stagename) &&
            DataManager.StageDataDict.TryGetValue(Stagename, out StageData lastCleared))
          return lastCleared.OrderIndex;

        return -1;
      }
    }


    public PlayerStatInfo playerStatInfo { get; set; } = new PlayerStatInfo();
    public ClientSession Session { get; set; }

    public Inventory inventory { get; set; }
    public InvenHero invenHero { get; set; }

    public InvenGacha invenGacha { get; set; }
    // 선택된 영웅//
    public Hero selectHero { get; set; }  
    // 주간 보상 마지막 수령시간//
    public DateTime LastDailyRewardTime;
    // 주간보상 플래그//
    public int WeeklyRewardFlags { get; set; } = 0;
    //주 시작 요일//
    public DayOfWeek WeekStartDay;
    //시간 9시로 초기화//  0~256만으로 충분함
    public byte ResetHourLocal = 9;
    


    public static Player MakePlayer(ClientSession session , PlayerDb playerDb)
    {
      Player player = new Player();
      player.Session = session;
      player.Init(playerDb);

      return  player;
    }

    public void Wallet(int gold = 0, int dia = 0, int energy = 0, int exp = 0, bool sendToClient = true)
    {
      // 1) 메모리 값 변경
      if (gold != 0)
        Gold += gold;

      if (dia != 0)
        Diamond += dia;

      if (energy != 0)
        Energy += energy;

      if (exp != 0)
      {
        Exp += exp;
        // TODO: 여기서 레벨업 체크/처리 넣으면 됨
        // CheckLevelUp();
      }

      // 2) 클라이언트로 월렛 상태 패킷 전송
      if (!sendToClient || Session == null)
        return;

      S_Wallet walletPkt = new S_Wallet();
      walletPkt.Wallet.Gold = this.Gold;
      walletPkt.Wallet.Diamond = this.Diamond;
      walletPkt.Wallet.Energy = this.Energy;
      walletPkt.Wallet.Exp = this.Exp;
      walletPkt.Wallet.Level = this.Level;  // 필요하면
      
      Session.Send(walletPkt);
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
      playerStatInfo.InventoryCapacity = playerDb.InventoryCapacity;
      playerStatInfo.Energy = playerDb.Energy;
      playerStatInfo.StageName = playerDb.StageName;
      
      playerStatInfo.Rating = playerDb.Rating;
      playerStatInfo.TimeZoneId = playerDb.TimeZoneId;  
      ///playerStatInfo.ResetHourLocal = playerDb.ResetHourLocal;

      inventory = new Inventory(this);
      invenHero = new InvenHero(this);
      invenGacha= new InvenGacha(this);


      invenHero.Init(playerDb);
      inventory.Init(playerDb);
      invenGacha.Init(playerDb);

      WeekStartDay = playerDb.WeekStartDay;
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

  }
}
