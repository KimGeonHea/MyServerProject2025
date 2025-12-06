using GameServer.Game.Room;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Data
{
  #region Hero

  [Serializable]
  public class HeroData
  {
    public int TemplateId; //템플릿 아이디
    public string Name; //개발용
    public string NameTextId; // TODO 랭기지로 빼야함\
    public EHeroGrade Grade;
    public string DescriptionTextID;
    public string SkillName;
    public string SkillDescription;
    public string IconImage;
    public string PrefabName;
    public int MaxHp;
    public int AttackDamage;
    public int Defence;
    public float MoveSpeed;
    public float CriRate;
    public int SkillDamage;
    public float StaminaRegenSpeed;
    public int EnchantCount;
  }


  [Serializable]
  public class HeroLoader : ILoader<int, HeroData>  
  {
    public List<HeroData> HeroData = new List<HeroData>();

    public Dictionary<int, HeroData> MakeDict()
    {
      Dictionary<int, HeroData> dict = new Dictionary<int, HeroData>();
      foreach (HeroData hero in HeroData)
        dict.Add(hero.TemplateId, hero);
      return dict;    
    }
  }
  #endregion

 
  #region Item
  [Serializable]
  public class ItemData
  {
    public int TemplateId; //템플릿 아이디
    public string Name; //아이템 이름
    public EItemType ItemType;
    public EItemSubType SubType;
    public EConsumableType ConsumableType;
    public EItemGrade Grade;
    public string IconImage;
    public string PrefabName;
    public bool Stacable;
    public int MaxCount;
    public int Damage;
    public int Def;
    public int SpRegen;
    public int Health;
    public float MoveSpeed;
    public float CriRate;
    public int SkillDamage;
    public int enchantCount;
    public int SellCount;
    public string DescriptionTextID;
  }
  
  public class EquipData : ItemData
  {
    public int Attack;
    public int Def;
    public int HpRegen;
    public int MoveSpeed;
    public float AtkSpeed;
    public float CriRate;
    public float CriDamage;
    public int enchantCount;
  }
  
  public class ConsumableData : ItemData
  {
    public int maxCount;
  }
  
  [Serializable]
  public class ItemLoader : ILoader<int, ItemData>
  {
    public List<ItemData> ItemData = new List<ItemData>();
  
    public Dictionary<int, ItemData> MakeDict()
    {
      Dictionary<int, ItemData> dict = new Dictionary<int, ItemData>();
      foreach (ItemData item in ItemData)
      {
        dict.Add(item.TemplateId, item);
      }
      return dict;
    }
  }
  #endregion


  #region HeroSkillData
  [Serializable]
  public class HeroSkillData
  {
    public int TemplateId; //템플릿 아이디
    public string Name; //개발용
    public string PrefabName;
    public EHeroSkillType SkillType; // 스킬 타입 (예: 일반, 궁극기 등)
    public EHeroSubSkillType SubSkillType; // 서브 스킬 타입 (예: 넉백, 스턴 등)
    public float Speed;
    public float Range;
    public float Radius;
    public float CenterX;
    public float CenterY;
    public float CenterZ;
  }


  [System.Serializable]
  public class HeroSkillLoader : ILoader<int, HeroSkillData>
  {
    public List<HeroSkillData> HeroSkillData = new List<HeroSkillData>(); // 이름 변경

    public Dictionary<int, HeroSkillData> MakeDict()
    {
      Dictionary<int, HeroSkillData> dict = new Dictionary<int, HeroSkillData>();
      foreach (HeroSkillData item in HeroSkillData)
      {
        dict[item.TemplateId] = item;
      }

      return dict;
    }

    public bool Validate() => true;
  }
  #endregion


  #region StageData
  [System.Serializable]
  public class StageData
  {
    public string StageId;
    public string MapPrefabAddress;
    public string BgmAddress;
    public int ConsumeEnergy;
    public int ClearExp;
    public string StageRewardDataId;
    public int ClearGold;
    public float MinClearTime;
    public bool HasBoss;
    public int OrderIndex;
    public int UiSlotIndex;
    public EStageType EStageType;
  }
  [System.Serializable]
  public class StageLoader : ILoader<string, StageData>
  {
    public List<StageData> StageData = new List<StageData>();

    public Dictionary<string, StageData> MakeDict()
    {
      Dictionary<string, StageData> dict = new Dictionary<string, StageData>();
      foreach (var entry in StageData)
      {
        if (!string.IsNullOrEmpty(entry.StageId))
          dict[entry.StageId] = entry;
      }
      return dict;
    }

    public bool Validate() => true;
  }

  #endregion


  #region StageReward
  [System.Serializable]
  public class StageRewardData
  {
    public string StageRewardId;
    public ERewardType ERewardType; // ErwardTypeGold / ErwardTypeDiamod / ErwardTypeObject ...
    public int Count;               // 골드/다이아/아이템 개수
    public int ItemId;              // 아이템이면 템플릿 ID, 골드/다이아면 0
  }

  [System.Serializable]
  public class StageRewardDataGroup
  {
    public string StageRewardId;
    public List<StageRewardData> Rewards = new List<StageRewardData>();
  }

  [System.Serializable]
  public class RewardDataLoader : ILoader<string, StageRewardDataGroup>
  {
    //  변경 1: Group 리스트가 아니라 "row 리스트"로 받는다
    public List<StageRewardData> StageRewardData = new List<StageRewardData>();

    public Dictionary<string, StageRewardDataGroup> MakeDict()
    {
      Dictionary<string, StageRewardDataGroup> dict = new Dictionary<string, StageRewardDataGroup>();

      foreach (var row in StageRewardData)
      {
        if (row == null )
          continue;

        //  같은 StageRewardId 그룹 찾기 (없으면 생성)
        if (!dict.TryGetValue(row.StageRewardId, out StageRewardDataGroup group))
        {
          group = new StageRewardDataGroup
          {
            StageRewardId = row.StageRewardId,
            Rewards = new List<StageRewardData>()
          };
          dict[row.StageRewardId] = group;
        }

        //  이 그룹에 현재 row 추가
        group.Rewards.Add(row);
      }

      return dict;
    }

    public bool Validate()
    {
      return true;
    }
  }

  #endregion


  #region MonsterData
  [Serializable]
  public class MonsterData
  {
    public int TemplateId;
    public string MonsterName;
    public int Hp;
    public int Attack;
    public int Defense;              // CSV의 "Defence"도 매핑
    public float MoveSpeed;
    public float AttackRange;          // 근접은 0(콜라이더로 판정)
    public float AttackDelay;          // 초 단위
    public int KnockbackResistPct;   // 0~100
    public int AirborneResistPct;    // 0~100
  }


  [System.Serializable]
  public class MonsterServerDataLoader : ILoader<int, MonsterData>
  {
    public List<MonsterData> MonsterData = new List<MonsterData>();

    public Dictionary<int, MonsterData> MakeDict()
    {
      Dictionary<int, MonsterData> dict = new Dictionary<int, MonsterData>();
      foreach (var entry in MonsterData)
      {
        //if (!string.IsNullOrEmpty(entry.MonsterName))
        dict[entry.TemplateId] = entry;
      }
      return dict;
    }

    public bool Validate() => true;
  }

  #endregion
}
