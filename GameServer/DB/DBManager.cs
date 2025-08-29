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

    public static ConcurrentDictionary<int/*heroDbId*/, DBJobQueue> _jobQueueDic = new ConcurrentDictionary<int, DBJobQueue>();
    public static ConcurrentQueue<int/*heroDbId*/> _executeQueue = new ConcurrentQueue<int>();

    #region JobQueue

    public static void Push(int heroDbId, Action action)
    {
      if (_jobQueueDic.ContainsKey(heroDbId) == false)
      {
        _jobQueueDic.TryAdd(heroDbId, new DBJobQueue());
        _executeQueue.Enqueue(heroDbId);
      }

      _jobQueueDic[heroDbId].Enqueue(() => { action.Invoke(); FinishProcessing(heroDbId); });
    }

    public static Action TryPop(int heroDbId)
    {
      if (_jobQueueDic.TryGetValue(heroDbId, out DBJobQueue jobQueue) == false)
        return null;

      return jobQueue.TryDequeue();
    }

    public static void Clear(int heroDbId)
    {
      _jobQueueDic.TryRemove(heroDbId, out DBJobQueue jobQueue);
    }

    private static void FinishProcessing(int heroDbId)
    {
      if (_jobQueueDic.TryGetValue(heroDbId, out DBJobQueue jobQueue) == false)
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

        Console.WriteLine($"HeroDbIdGenerator initialized to {_heroDbIdGenerator}");
        Console.WriteLine($"ItemDbIdGenerator initialized to {_itemDbIdGenerator}");
        Console.WriteLine($"PlayerDbIdGenerator initialized to {_playerDbIdGenerator}");
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
        if (_executeQueue.TryDequeue(out int heroDbId) == false)
          continue;

        if (ContainsKey(heroDbId) == false)
          continue;

        Action action = TryPop(heroDbId);
        if (action != null)
          action.Invoke();

        _executeQueue.Enqueue(heroDbId);

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

        //DateTime nowKorea = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KoreaTimeZone);
        // 전날 오전 6시로 설정
        //DateTime initialEnergyTime = nowKorea.Date.AddHours(9).AddDays(-1);

        string tzId = string.IsNullOrWhiteSpace(loginPacket.TimeZoneId) ? "Asia/Seoul" : loginPacket.TimeZoneId;
        byte resetHour = 9;
        var weekStart = DayOfWeek.Monday;

        // “지금” 기준, 전 일일창 시작 직후로 세팅하면,
        // 가입 직후에도 중복지급 없이 다음 창에서 지급되게 만들 수 있음.
        var nowUtc = DateTime.UtcNow;
        var (startUtc, _) = TzUtil.GetDailyWindowUtc(tzId, nowUtc, resetHour);
        var initialEnergyGiven = startUtc.AddSeconds(1); // 창 시작 직후로 표시



        player = new PlayerDb()
        {
          AccountDbId = DBManager.GeneratePlayerDbId(), //TODO Oath에서 진행예정
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


        if(DataManager.heroDict.TryGetValue(101, out HeroData data))
        {
          HeroDb heroDb = new HeroDb()
          {
            HeroDbId = DBManager.GenerateHeroDbId(),
            PlayerDbId = player.PlayerDbId,
            TemplateId = data.TemplateId,
            Slot = 0,
            EnchantCount = data.enchantCount
          };
          player.Heros.Add(heroDb);
        }

        if (DataManager.heroDict.TryGetValue(301, out HeroData herodata))
        {
          HeroDb heroDb = new HeroDb()
          {
            HeroDbId = DBManager.GenerateHeroDbId(),
            PlayerDbId = player.PlayerDbId,
            TemplateId = herodata.TemplateId,
            Slot = 1,
            EnchantCount = herodata.enchantCount
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

          ResetWeeklyRewardIfNeeded(player, nowUtc);


          db.Entry(player).Collection(p => p.Heros).Load();
          db.Entry(player).Collection(p => p.Items).Load();

          if (CanGiveDailyEnergy(player , nowUtc))
          {
            int energyToGive = Math.Min(60 - player.Energy, 60);
            if (energyToGive > 0)
            {
              player.Energy += energyToGive;
              player.LastEnergyGivenTime = DateTime.UtcNow;

              if (db.SaveChangesEx())
                return player;
              Console.WriteLine($"{player.PlayerName} 하루 에너지 지급 완료!");
            }
            else
            {
              Console.WriteLine($"{player.PlayerName} 에너지가 이미 최대치입니다. 지급하지 않습니다.");
            }
          }
          else
          {
            Console.WriteLine($"{player.PlayerName} 오늘은 이미 에너지를 받았습니다.");
          }
        }

        return player;
      }
    }


    public static HeroDb CreateHeorDb(int playerDbId, HeroData herodata , int Slot)
    {

      using (GameDbContext db = new GameDbContext())
      {

        HeroDb heardb = new HeroDb()
        {
          HeroDbId = DBManager.GenerateHeroDbId(),
          PlayerDbId = playerDbId,
          TemplateId = herodata.TemplateId,
          Slot = Slot,
          EnchantCount = herodata.enchantCount
        };

        db.Heroes.Add(heardb);
        if (db.SaveChangesEx())
          return heardb;
      }
      return null;
    }

    public static ItemDb CreateItemDb(int playerDbId , int templateId)
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
    public static void ResetWeeklyRewardIfNeeded(PlayerDb p, DateTime nowUtc)
    {
      nowUtc = TzUtil.AsUtc(nowUtc);

      // “오늘”의 일일창 시작을 앵커로
      var (dayStartUtc, _) = TzUtil.GetDailyWindowUtc(p.TimeZoneId, nowUtc);
      var nowWeekStartUtc = TzUtil.GetWeekStartUtc(p.TimeZoneId, p.WeekStartDay, dayStartUtc);

      var lastDailyUtc = TzUtil.AsUtc(p.LastDailyRewardTime);
      DateTime lastWeekStartUtc = DateTime.MinValue;
      if (lastDailyUtc != default)
      {
        // 과거의 "그날" 기준으로 주 시작 계산
        var (lastDayStartUtc, _) = TzUtil.GetDailyWindowUtc(p.TimeZoneId, lastDailyUtc);
        lastWeekStartUtc = TzUtil.GetWeekStartUtc(p.TimeZoneId, p.WeekStartDay, lastDayStartUtc);
      }

      if (lastWeekStartUtc < nowWeekStartUtc)
        p.WeeklyRewardFlags = 0;
    }

    //public static bool CanGiveDailyEnergy(PlayerDb player)
    //{
    //  // 한국 시간 (UTC+9) 기준으로 변환
    //  DateTime lastGivenKorea = TimeZoneInfo.ConvertTimeFromUtc(player.LastEnergyGivenTime, KoreaTimeZone);
    //  DateTime nowKorea = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KoreaTimeZone);
    //
    //  // 오늘 오전 6시
    //  DateTime today9AM = nowKorea.Date.AddHours(9);
    //
    //  // 만약 지금 시간이 오늘 6시 이전이면, 기준 시각은 어제 6시
    //  if (nowKorea < today9AM)
    //    today9AM = today9AM.AddDays(-1);
    //
    //  return lastGivenKorea < today9AM;
    //}
    //
    //static TimeZoneInfo ResolveTz(string tzId)
    //{
    //  if (string.IsNullOrWhiteSpace(tzId)) return TimeZoneInfo.Utc;
    //  try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
    //  catch { return TimeZoneInfo.Utc; } // 폴백
    //}
    //
    //// “오늘” 리셋 창(09:00~다음날 09:00)의 [startUtc, nextUtc]
    //static (DateTime startUtc, DateTime nextUtc) GetDailyWindowUtc(PlayerDb p, DateTime nowUtc)
    //{
    //  var tz = ResolveTz(p.TimeZoneId);           // 없으면 "Asia/Seoul" 또는 "UTC"
    //  var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
    //
    //  var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, p.ResetHourLocal, 0, 0);
    //  if (nowLocal < startLocal) startLocal = startLocal.AddDays(-1); // 9시 전이면 전날 9시가 오늘분 시작
    //
    //  var nextLocal = startLocal.AddDays(1);
    //
    //  var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), tz);
    //  var nextUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), tz);
    //  return (startUtc, nextUtc);
    //}
    //
    //// “주 시작(월요일 등)”을 UTC로
    //static DateTime GetWeekStartUtc(PlayerDb p, DateTime anchorStartUtc)
    //{
    //  var tz = ResolveTz(p.TimeZoneId);
    //  var anchorLocal = TimeZoneInfo.ConvertTimeFromUtc(anchorStartUtc, tz);
    //
    //  int diff = ((7 + (int)anchorLocal.DayOfWeek - (int)p.WeekStartDay) % 7);
    //  var weekStartLocal = anchorLocal.AddDays(-diff);
    //
    //  return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(weekStartLocal, DateTimeKind.Unspecified), tz);
    //}
    //
    //
    //private static readonly TimeZoneInfo KoreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
  }

}

