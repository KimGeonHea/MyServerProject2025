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

  [Serializable]
  public class HeroSkillData
  {
    public int TemplateId; //템플릿 아이디
    public string Name; //개발용
    public string PrefabName;
    public EHeroSkillType SkillType; // 스킬 타입 (예: 일반, 궁극기 등)
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
}
