using Google.Protobuf.Protocol;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static System.Collections.Specialized.BitVector32;

namespace Server.Game
{
  [Table("Player")]
  public class PlayerDb
  {
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int PlayerDbId { get; set; }

    public long AccountDbId { get; set; } 

    [Required]
    [MaxLength(50)]
    public string PlayerName { get; set; }

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime LastEnergyGivenTime { get; set; } = DateTime.UtcNow;
    public DateTime LastDailyRewardTime { get; set; } = DateTime.MinValue;
    public int WeeklyRewardFlags { get; set; } = 0; // 1~7일 중 현재 며칠차인지
    public int Level { get; set; }
    public int Exp { get; set; }
    public int TotalExp { get; set; } // 누적을 통해 계산할껀지
    public int Gold { get; set; }
    public int Diamond { get; set; }
    public int RoomId { get; set; } // MMO에 만필요
    public int Energy { get; set; }
    public int Rating { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Seoul"; // 유저 타임존(IANA)
    public DayOfWeek WeekStartDay { get; set; } = DayOfWeek.Monday; // 주 시작 요일

    public int InventoryCapacity { get; set; }
    public string StageName { get; set; }
    
    // 플레이어가 보유한 영웅들 (1:N 관계)
    public ICollection<HeroDb> Heros { get; set; } = new List<HeroDb>();

    // 플레이어의 인벤토리 아이템 (1:N 관계)
    public ICollection<ItemDb> Items { get; set; } = new List<ItemDb>();

    public ICollection<GachaDb> Gachas { get; set; } = new List<GachaDb>(); 

    [NotMapped]
    public bool[] WeeklyRewardsClaimed
    {
      get
      {
        bool[] result = new bool[7];
        for (int i = 0; i < 7; i++)
          result[i] = (WeeklyRewardFlags & (1 << i)) != 0;
        return result;
      }
      set
      {
        WeeklyRewardFlags = 0;
        for (int i = 0; i < 7; i++)
        {
          if (value[i])
            WeeklyRewardFlags |= (1 << i);
        }
      }
    }

  }

  // 영웅 테이블
  [Table("Hero")]
  public class HeroDb
  {
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int HeroDbId { get; set; }

    [ForeignKey("Owner")]
    public int? PlayerDbId { get; set; }  // FK 설정 유지
    public PlayerDb Owner { get; set; }
    public int Slot { get; set; }
    public int TemplateId { get; set; }  // 아이템 타입
    public int EnchantCount { get; set; }  // 강화 수치

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastAcquiredAtUtc { get; set; } = DateTime.UtcNow; // 드롭/구매/우편/스택때만 갱신
    public DateTime SeenAcquiredUtc { get; set; } = DateTime.MinValue; // “봤다”로 처리한 최신 획득분 시각


  }

  // 아이템 테이블
  [Table("Item")]
  public class ItemDb
  {
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long ItemDbId { get; set; }

    public int TemplateId { get; set; }  // 아이템 타입

    public EItemSlotType EquipSlot { get; set; }
    public EItemDBState DbState { get; set; }

    [ForeignKey("Owner")]
    public int? PlayerDbId { get; set; }  // 이 아이템을 보유한 플레이어
    public PlayerDb Owner { get; set; }
    public int Count { get; set; }  // 소유 개수
    public int EnchantCount { get; set; }  // 강화 수치

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastAcquiredAtUtc { get; set; } = DateTime.UtcNow; // 드롭/구매/우편/스택 때만 갱신
    public DateTime SeenAcquiredUtc { get; set; } = DateTime.MinValue; // “봤다”로 처리한 최신 획득분 시각
  }

  //가차시스템//
  [Table("Gacha")]
  public class GachaDb
  {
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int GachaDbId { get; set; }
    public int TemplateId { get; set; } 
    [ForeignKey("Owner")]
    public int? PlayerId { get; set; }
    public PlayerDb Owner { get; set; }
    public int PityCount { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
  }
}
