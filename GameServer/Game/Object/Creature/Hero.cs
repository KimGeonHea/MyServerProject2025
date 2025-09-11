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

  public partial class Hero : BaseObject
  {
    public int OwnerDbid { get; set; }
    public EHeroUpperState EHeroUpperState { get; set; } // 상위 상태 (예: 일반, 스킬 사용 중 등)
    public EHeroLowerState EHeroLowerState { get; set; } // 하위 상태 (예: 이동, 공격 등)
    ToatalEquipData toatalEquipData { get; set; } = new ToatalEquipData();
    TotalStatData totalStatData { get; set; } = new TotalStatData();
    HeroStatInfo heroStatInfo { get; set; } = new HeroStatInfo();

    HeroData heroData { get; set; } = new HeroData();

    public int TemplatedId
    {
      get { return heroStatInfo.TemplateId; }
      set { heroStatInfo.TemplateId = value; }
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


    //totalStatData를 통해 영웅의 총 스탯을 관리합니다.

    public int CurHp
    {
      get => totalStatData.totalHealth;
      set => totalStatData.totalHealth = value;
    }
    public int MaxHp
    {
      get => totalStatData.totalHealth;
      set => totalStatData.totalHealth = value;
    }

    public int AttackDamage
    {
      get => totalStatData.totalAttack;
      set => totalStatData.totalAttack = value;
    } 

    public int Defence
    {
      get => totalStatData.totalDeffence;
      set => totalStatData.totalDeffence = value;
    } 

    //public float CriDamage
    //{
    //  //get => totalStatData.totalCriDamage;
    //  //set => totalStatData.totalCriDamage = value;
    //} 

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



    public virtual void Init(HeroDb herodb)
    {
      TemplatedId = herodb.TemplateId;
      TempleteID = herodb.TemplateId; // BaseObject의 템플릿 아이디 설정 
      OwnerDbid = herodb.PlayerDbId ?? throw new InvalidOperationException("PlayerDbId is null");
      HeroDbId = herodb.HeroDbId;
      Slot = herodb.Slot;
      ObjectType = EGameObjectType.Hero;

      HeroData herodata = null;
      DataManager.heroDict.TryGetValue(herodb.TemplateId, out herodata);
      if (herodata != null)
      {
        heroData = herodata;
      }
    }

    public void SetTotalData(ToatalEquipData toatalEquipData)
    {
      if (HeroData != null)
      {
        totalStatData.totalAttack = HeroData.AttackDamage+ toatalEquipData.totalAttack;
        totalStatData.totalDeffence = HeroData.Defence + toatalEquipData.totalDeffence; //+ HeroStatInfo.TotalDefence;
        totalStatData.totalHealth = HeroData.MaxHp + toatalEquipData.totalHealth; //+ HeroStatInfo.TotalHealth;
        
        totalStatData.totalSkillDamage  = HeroData.AttackDamage + toatalEquipData.totalSkillDamage; // 스킬 데미지는 기본 공격력으로 설정
        totalStatData.totalSpregen = HeroData.staminaRegenSpeed + toatalEquipData.totalSpregen;
        totalStatData.totalMoveSpeed = HeroData.MoveSpeed + toatalEquipData.totalMoveSpeed;

        CurHp = totalStatData.totalHealth; // 현재 HP는 최대 HP로 초기화
        MaxHp = totalStatData.totalHealth; // 최대 HP 설정
      }
    } 
    public static Hero MakeHero(HeroDb heardb)
    {
      Hero hero =new Hero();
      hero.TemplatedId = heardb.TemplateId;
      hero.OwnerDbid = heardb.PlayerDbId ?? throw new InvalidOperationException("PlayerDbId is null");
      hero.HeroDbId = heardb.HeroDbId;
      hero.Slot = heardb.Slot;

      HeroData herodata = null;
      DataManager.heroDict.TryGetValue(hero.TemplatedId, out herodata);
      if (herodata != null)
      {
        hero.heroData = herodata;
      }

       return hero;
    }

    public static HeroStatInfo MakieHeroStatInfo(HeroDb herodb)
    {
      int templateId = herodb.TemplateId;
      if (DataManager.heroDict.TryGetValue(templateId, out HeroData itemData) == false)
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
