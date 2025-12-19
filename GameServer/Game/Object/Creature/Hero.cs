using Google.Protobuf.Protocol;
using Microsoft.Identity.Client;
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
  public class TotalStatData
  {
    public int totalAttack;
    public int totalDeffence;
    public int totalHealth;
    public int totalSkillDamage;
    public float totalCriRate;
    public float totalMoveSpeed;
    public float totalSpregen;
  }

  public partial class Hero : Creature
  {
    public TotalStatData TotalStatData
    {
      get { return totalStatData; }
      set { totalStatData = value; }
    }
    public Player OwnerPlayer { get; set; }
    public int OwnerDbId { get; set; }
    public EHeroUpperState EHeroUpperState { get; set; } // 상위 상태 (예: 일반, 스킬 사용 중 등)
    public EHeroLowerState EHeroLowerState { get; set; } // 하위 상태 (예: 이동, 공격 등)
    public ETeamType TeamType { get; set; } = ETeamType.None;
    TotalStatData totalStatData { get; set; } = new TotalStatData();
    HeroStatInfo heroStatInfo { get; set; } = new HeroStatInfo();

    HeroData heroData { get; set; } = new HeroData();
    public override int TempleteID
    {
      get => heroStatInfo.TemplateId;
      set
      {
        heroStatInfo.TemplateId = value;  // 스탯/데이터용
        ObjectInfo.TemplateId = value;  // 패킷용
      }
    }

    public int Slot
    {
      get { return heroStatInfo.ItemSlotType; }
      set {heroStatInfo.ItemSlotType = value; } 
    }

    public int HeroDbId
    {
      get { return heroStatInfo.HeroDbId; }
      set { heroStatInfo.HeroDbId = value; }
    }

    public HeroData HeroData
    {
      get { return heroData; }
      set { heroData = value; }
    }

    public HeroStatInfo HeroStatInfo
    {
      get { return heroStatInfo; }
      set { heroStatInfo = value; }
    }

    //public int CurHp
    //{
    //  get => totalStatData.totalHealth;
    //  set => totalStatData.totalHealth = value;
    //}
    //public int MaxHp
    //{
    //  get => totalStatData.totalHealth;
    //  set => totalStatData.totalHealth = value;
    //}

    //public int AttackDamage
    //{
    //  get => totalStatData.totalAttack;
    //  set => totalStatData.totalAttack = value;
    //} 
    //
    //public int Defence
    //{
    //  get => totalStatData.totalDeffence;
    //  set => totalStatData.totalDeffence = value;
    //} 
    //
    //public float MoveSpeed
    //{
    //  get => totalStatData.totalMoveSpeed;
    //  set => totalStatData.totalMoveSpeed = value;
    //}
    //public int TempleteID
    //{
    //  get { return heroStatInfo.TemplateId; }
    //  set { heroStatInfo.TemplateId = value; }
    //}


    public readonly float ShotStaminaCost = 10f;
    public float CurStamina { get; set; }
    public float MaxStamina { get; set; } = 100.0f;

    public float StaminaRegenSpeed
    {
      get => baseSprejenSpeed + totalStatData.totalSpregen;
    }

    public float CriRate
    {
      get => totalStatData.totalCriRate;
      set => totalStatData.totalCriRate = value;
    } 
    public int SkillDamage
    {
      get => totalStatData.totalSkillDamage; // 스킬 데미지는 기본 공격력으로 설정
      set => totalStatData.totalSkillDamage = value; // 스킬 데미지를 변경할 수 있음
    }
    
    public override Vector3 ColliderPosition
    {
      get => new Vector3(Position.X, Position.Y + 0.8f, Position.Z);
    }
    //public override float ColliderRadius { get; set; } = 0.8f;

    float baseSprejenSpeed = 5.0f;

    public virtual void Init(HeroDb herodb , Player p)
    {
      TempleteID = herodb.TemplateId;   
      OwnerDbId = herodb.PlayerDbId ?? throw new InvalidOperationException("PlayerDbId is null");
      HeroDbId = herodb.HeroDbId;
      Slot = herodb.Slot;
      ObjectType = EGameObjectType.Hero;
      OwnerPlayer = p;
      HeroData herodata = null;

      if(DataManager.HeroDataDict.TryGetValue(herodb.TemplateId, out herodata))
      {
        heroData = herodata;
      }
      else
      {
        Console.WriteLine($"Hero Init Failed! TempleteID : {herodb.TemplateId} not found in HeroDataDict");
      }
    }

    public void InitTotalData(ToatalEquipData toatalEquipData)
    {
      if (HeroData != null || toatalEquipData != null)
      {
        totalStatData.totalAttack = HeroData.AttackDamage+ toatalEquipData.totalAttack;
        totalStatData.totalDeffence = HeroData.Defence + toatalEquipData.totalDeffence; 
        totalStatData.totalHealth = HeroData.MaxHp + toatalEquipData.totalHealth;
        
        totalStatData.totalSkillDamage  = HeroData.AttackDamage + toatalEquipData.totalSkillDamage;
        totalStatData.totalSpregen = HeroData.StaminaRegenSpeed + toatalEquipData.totalSpregen;
        totalStatData.totalMoveSpeed = HeroData.MoveSpeed + toatalEquipData.totalMoveSpeed;

        CurHp = totalStatData.totalHealth; // 현재 HP는 최대 HP로 초기화
        MaxHp = totalStatData.totalHealth; // 최대 HP 설정

        AttackDamage = totalStatData.totalAttack;
        Defence = totalStatData.totalDeffence;
        MoveSpeed = totalStatData.totalMoveSpeed;

        CurStamina = MaxStamina;
      }
    } 
    public static Hero MakeHero(HeroDb heardb)
    {
      Hero hero =new Hero();
      hero.TempleteID = heardb.TemplateId;
      hero.OwnerDbId = heardb.PlayerDbId ?? throw new InvalidOperationException("PlayerDbId is null");
      hero.HeroDbId = heardb.HeroDbId;
      hero.Slot = heardb.Slot;

      HeroData herodata = null;
      DataManager.HeroDataDict.TryGetValue(hero.TempleteID, out herodata);
      if (herodata != null)
      {
        hero.heroData = herodata;
      }

       return hero;
    }

    public static HeroStatInfo MakieHeroStatInfo(HeroDb herodb)
    {
      int templateId = herodb.TemplateId;
      if (DataManager.HeroDataDict.TryGetValue(templateId, out HeroData itemData) == false)
        return null;

      HeroStatInfo heroStatInfo = new HeroStatInfo()
      {
        HeroDbId = herodb.HeroDbId,
        TemplateId = herodb.TemplateId,
        ItemSlotType = herodb.Slot        
      };

      return heroStatInfo;
    }


  }


}
