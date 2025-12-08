using GameServer.Utils;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Game
{
  public class DBJobQueue
  {
    public Queue<Action> _jobQueue { get; } = new Queue<Action>();
    public bool Processing { get; private set; }

    object _lock = new object();

    public void Enqueue(Action job)
    {
      lock (_lock)
      {
        _jobQueue.Enqueue(job);
      }
    }

    public Action TryDequeue()
    {
      lock (_lock)
      {
        if (Processing)
          return null;

        if (_jobQueue.TryDequeue(out Action result))
        {
          Processing = true;
          return result;
        }
      }

      return null;
    }

    public void FinishProcessing()
    {
      lock (_lock)
      {
        Processing = false;
      }
    }
  }
  public partial class DBManager : JobSerializer
  {
    public static DBManager Instance { get; } = new DBManager();

    public static ConcurrentDictionary<int/*playerDbId*/, DBJobQueue> _jobQueueDic = new ConcurrentDictionary<int, DBJobQueue>();
    public static ConcurrentQueue<int/*playerDbId*/> _executeQueue = new ConcurrentQueue<int>();

    #region JobQueue

    public static void Push(int playerDbId, Action action)
    {
      if (_jobQueueDic.ContainsKey(playerDbId) == false)
      {
        _jobQueueDic.TryAdd(playerDbId, new DBJobQueue());
        _executeQueue.Enqueue(playerDbId);
      }

      _jobQueueDic[playerDbId].Enqueue(() =>
      {
        action.Invoke();
        FinishProcessing(playerDbId);
      });
    }

    public static Action TryPop(int playerDbId)
    {
      if (_jobQueueDic.TryGetValue(playerDbId, out DBJobQueue jobQueue) == false)
        return null;

      return jobQueue.TryDequeue();
    }

    public static void Clear(int playerDbId)
    {
      _jobQueueDic.TryRemove(playerDbId, out DBJobQueue jobQueue);
    }

    private static void FinishProcessing(int playerDbId)
    {
      if (_jobQueueDic.TryGetValue(playerDbId, out DBJobQueue jobQueue) == false)
        return;

      jobQueue.FinishProcessing();
    }

    private static bool ContainsKey(int heroDbId)
    {
      return _jobQueueDic.ContainsKey(heroDbId);
    }
    #endregion

    static long _threadCount = 0;

    #region DbId
    public static int _heroDbIdGenerator = 0;
    public static int GenerateHeroDbId() { return Interlocked.Increment(ref _heroDbIdGenerator); }

    public static long _itemDbIdGenerator = 0;
    public static long GenerateItemDbId() { return Interlocked.Increment(ref _itemDbIdGenerator); }

    public static int _playerDbIdGenerator = 0;
    public static int GeneratePlayerDbId() { return Interlocked.Increment(ref _playerDbIdGenerator); }

    public static int _gachaDbIdGenerator = 0;
    public static int GenerateGachaDbId() { return Interlocked.Increment(ref _gachaDbIdGenerator); }
    public static void InitDbIds()
    {
      // item
      using (var context = new GameDbContext())
      {
        // DB에서 가장 큰 ItemDbId 조회
        HeroDb heroDb = context.Heroes.OrderByDescending(x => x.HeroDbId).FirstOrDefault();
        if (heroDb != null)
          Interlocked.Exchange(ref _heroDbIdGenerator, heroDb.HeroDbId);

        ItemDb itemDb = context.Items.OrderByDescending(x => x.ItemDbId).FirstOrDefault();
        if (itemDb != null)
          Interlocked.Exchange(ref _itemDbIdGenerator, itemDb.ItemDbId);

        PlayerDb playerDb = context.playerDbs.OrderByDescending(x => x.PlayerDbId).FirstOrDefault();
        if (playerDb != null)
          Interlocked.Exchange(ref _playerDbIdGenerator, playerDb.PlayerDbId);

        GachaDb gachaDb = context.gachaDbs.OrderByDescending(x => x.GachaDbId).FirstOrDefault();
        if (gachaDb != null)
          Interlocked.Exchange(ref _gachaDbIdGenerator, gachaDb.GachaDbId);

        Console.WriteLine($"HeroDbIdGenerator initialized to {_heroDbIdGenerator}");
        Console.WriteLine($"ItemDbIdGenerator initialized to {_itemDbIdGenerator}");
        Console.WriteLine($"PlayerDbIdGenerator initialized to {_playerDbIdGenerator}");
        Console.WriteLine($"GenerateGachaDbId initialized to {_gachaDbIdGenerator}");
      }
    }
    #endregion

    public static void LaunchDBThreads(int threadCount)
    {
      _threadCount = threadCount;

      for (int i = 0; i < threadCount; i++)
      {
        Thread t = new Thread(new ParameterizedThreadStart(DBThreadJob));
        t.Name = $"DBThread_{i}";
        t.Start(i);
      }
    }

    static public void DBThreadJob(object arg)
    {
      int threadId = (int)arg;

      while (true)
      {
        if (_executeQueue.TryDequeue(out int playerDbId) == false)
          continue;

        if (ContainsKey(playerDbId) == false)
          continue;

        Action action = TryPop(playerDbId);
        if (action != null)
          action.Invoke();

        _executeQueue.Enqueue(playerDbId);

        Thread.Sleep(0);
      }
    }

    public static PlayerDb CreatePlayerDb(C_LoginRes loginPacket)
    {
      using (GameDbContext db = new GameDbContext())
      {
        PlayerDb player = db.playerDbs
        .Where(p => p.PlayerName == loginPacket.Uniqeid).FirstOrDefault();
        if (player != null)
          return null;

        // 1) 클라에서 온 값: 없으면 일단 "KR" 가정
        string rawTz = string.IsNullOrWhiteSpace(loginPacket.TimeZoneId)
          ? "KR"
          : loginPacket.TimeZoneId;     // "Asia/Seoul" 또는 "KR" 또는 "Korea Standard Time" 등

        // 2) 정규화: "KR" -> "Asia/Seoul"
        string tzId = TzUtil.NormalizeTzOrCountry(rawTz, "Asia/Seoul");

        byte resetHour = 9;
        var weekStart = DayOfWeek.Monday;

        var nowUtc = DateTime.UtcNow;
        var (startUtc, _) = TzUtil.GetDailyWindowUtc(tzId, nowUtc, resetHour);
        var initialEnergyGiven = startUtc.AddSeconds(1);

        player = new PlayerDb()
        {
          AccountDbId = DBManager.GeneratePlayerDbId(),
          PlayerDbId = DBManager.GeneratePlayerDbId(),
          PlayerName = loginPacket.Uniqeid,
          Level = 1,
          Exp = 0,
          TotalExp = 60,
          Gold = 0,
          Diamond = 0,
          Energy = 60,
          StageName = "1-1",
          InventoryCapacity = 30,

          //DB에는 정규화된 타임존만 저장
          TimeZoneId = tzId,
          WeekStartDay = weekStart,

          LastEnergyGivenTime = TzUtil.AsUtc(initialEnergyGiven),
          LastDailyRewardTime = default,
          WeeklyRewardFlags = 0,
          Heros = new List<HeroDb>(),
          Items = new List<ItemDb>()
        };

        //1차 저장
        db.playerDbs.Add(player);
        db.SaveChanges();


        if (DataManager.HeroDataDict.TryGetValue(101, out HeroData hoodieData))
        {
          HeroDb heroDb = new HeroDb()
          {
            HeroDbId = DBManager.GenerateHeroDbId(),
            PlayerDbId = player.PlayerDbId,
            TemplateId = hoodieData.TemplateId,
            Slot = 0,
            EnchantCount = hoodieData.EnchantCount
          };
          player.Heros.Add(heroDb);
        }

        if (DataManager.HeroDataDict.TryGetValue(301, out HeroData soliderData))
        {
          HeroDb heroDb = new HeroDb()
          {
            HeroDbId = DBManager.GenerateHeroDbId(),
            PlayerDbId = player.PlayerDbId,
            TemplateId = soliderData.TemplateId,
            Slot = 1,
            EnchantCount = soliderData.EnchantCount
          };
          player.Heros.Add(heroDb);
        }


        if (DataManager.HeroDataDict.TryGetValue(1201, out HeroData bearData))
        {
          HeroDb heroDb = new HeroDb()
          {
            HeroDbId = DBManager.GenerateHeroDbId(),
            PlayerDbId = player.PlayerDbId,
            TemplateId = bearData.TemplateId,
            Slot = 1,
            EnchantCount = bearData.EnchantCount
          };
          player.Heros.Add(heroDb);
        }

        ItemDb itemDb = new ItemDb()
        {
          ItemDbId = DBManager.GenerateItemDbId(),
          PlayerDbId = player.PlayerDbId,
          TemplateId = 10001,
          Count = 1,
          EquipSlot = EItemSlotType.Inventory,
          LastAcquiredAtUtc = DateTime.UtcNow,                              // 새로 들어옴
          SeenAcquiredUtc = DateTime.MinValue,                // 아직안봄
        };
        player.Items.Add(itemDb);


        if (db.SaveChangesEx())
          return player;

        return null;
      }
    }

    public static PlayerDb LoadPlayerDb(C_LoginRes loginPacket)
    {
      using (GameDbContext db = new GameDbContext())
      {
        PlayerDb player = db.playerDbs
          .Where(h => h.PlayerName == loginPacket.Uniqeid)
          .FirstOrDefault();

        if (player != null)
        {
          var nowUtc = DateTime.UtcNow;

          bool weeklyReset = ResetWeeklyRewardIfNeeded(player, nowUtc);

          db.Entry(player).Collection(p => p.Heros).Load();
          db.Entry(player).Collection(p => p.Items).Load();
          db.Entry(player).Collection(p => p.Gachas).Load();

          bool energyGiven = false;

          if (CanGiveDailyEnergy(player, nowUtc))
          {
            int energyToGive = Math.Min(Define.ENERGY_MAX - player.Energy, Define.ENERGY_MAX);
            if (energyToGive > 0)
            {
              player.Energy += energyToGive;
              player.LastEnergyGivenTime = nowUtc;
              energyGiven = true;
            }
          }

          //  주간 리셋 또는 에너지 지급 중 하나라도 있으면 DB 저장
          if (weeklyReset || energyGiven)
            db.SaveChangesEx();
        }


        return player;
      }
    }


    public static HeroDb CreateHeorDb(int playerDbId, HeroData herodata, int Slot)
    {

      using (GameDbContext db = new GameDbContext())
      {

        HeroDb heardb = new HeroDb()
        {
          HeroDbId = DBManager.GenerateHeroDbId(),
          PlayerDbId = playerDbId,
          TemplateId = herodata.TemplateId,
          Slot = Slot,
          EnchantCount = herodata.EnchantCount
        };

        db.Heroes.Add(heardb);
        if (db.SaveChangesEx())
          return heardb;
      }
      return null;
    }

    public static ItemDb CreateItemDb(int playerDbId, int templateId)
    {
      using (GameDbContext db = new GameDbContext())
      {

        ItemDb itemdb = new ItemDb()
        {
          ItemDbId = DBManager.GenerateItemDbId(),
          PlayerDbId = playerDbId,
          TemplateId = templateId,
          Count = 1,
          EquipSlot = EItemSlotType.Inventory,

        };

        db.Items.Add(itemdb);
        if (db.SaveChangesEx())
          return itemdb;

      }
      return null;
    }

    public static List<ItemDb> CreateTestDb(int playerDbId)
    {
      using (GameDbContext db = new GameDbContext())
      {
        // 특정 플레이어 찾기
        PlayerDb player = db.playerDbs.FirstOrDefault(x => x.PlayerDbId == playerDbId);
        if (player == null)
          return null;

        // 새 아이템 추가
        List<ItemDb> newItems = new List<ItemDb>
        {
            new ItemDb()
            {
                ItemDbId = DBManager.GenerateItemDbId(),
                PlayerDbId = playerDbId,
                TemplateId = 10002,
                Count = 1,
                EquipSlot = EItemSlotType.Inventory,
            },
            new ItemDb()
            {
                ItemDbId = DBManager.GenerateItemDbId(),
                PlayerDbId = playerDbId,
                TemplateId = 10011,
                Count = 1,
                EquipSlot = EItemSlotType.Inventory,
            },
            new ItemDb()
            {
                ItemDbId = DBManager.GenerateItemDbId(),
                PlayerDbId = playerDbId,
                TemplateId = 10021,
                Count = 1,
                EquipSlot = EItemSlotType.Inventory,
            }
        };

        db.Items.AddRange(newItems);
        db.SaveChangesEx();

        // 새로 추가한 아이템만 반환
        return newItems;
      }
    }
    public static HeroDb FindHeroDb(PlayerDb playerdb)
    {
      return playerdb.Heros.FirstOrDefault();
    }

    public static bool CanGiveDailyEnergy(PlayerDb p, DateTime nowUtc)
    {
      nowUtc = TzUtil.AsUtc(nowUtc);
      var last = TzUtil.AsUtc(p.LastEnergyGivenTime);

      var (startUtc, endUtc) = TzUtil.GetDailyWindowUtc(p.TimeZoneId, nowUtc);

      // 이번 일일창 안에서 이미 지급했으면 불가
      if (last >= startUtc && last < endUtc)
        return false;

      // 지급 가능
      return true;
    }

    public static void MarkEnergyGiven(PlayerDb p, DateTime nowUtc)
    {
      p.LastEnergyGivenTime = TzUtil.AsUtc(nowUtc);
    }

    // 주간 리셋(월~일 등) — 로그인 시/보상 전 호출
    public static bool ResetWeeklyRewardIfNeeded(PlayerDb p, DateTime nowUtc)
    {
      nowUtc = TzUtil.AsUtc(nowUtc);

      var lastDailyUtc = TzUtil.AsUtc(p.LastDailyRewardTime);
      if (lastDailyUtc == default)
        return false; // 한 번도 일일 보상 안 받았으면 굳이 리셋할 건 없음

      // TzUtil에 만든 IsNewWeekUtc 활용
      if (TzUtil.IsNewWeekUtc(p.TimeZoneId, p.WeekStartDay, lastDailyUtc, nowUtc))
      {
        p.WeeklyRewardFlags = 0;   // 주간 보상 0000000으로 초기화
        return true;
      }

      return false;
    }
  }
}

