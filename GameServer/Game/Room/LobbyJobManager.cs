using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using GameServer.Game.Room;
using GameServer;
using System.Collections.Concurrent;
using System.Collections;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Server.Game
{
  public class LobbyJobQueue
  {
    private Queue<Action> _jobQueue = new Queue<Action>();
    private bool _processing = false;
    private object _lock = new object();

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
        if (_processing)
          return null;

        if (_jobQueue.TryDequeue(out Action job))
        {
          _processing = true;
          return job;
        }
      }
      return null;
    }
    public void FinishProcessing()
    {
      lock (_lock)
      {
        _processing = false;
      }
    }

    public bool HasPendingJobs => _jobQueue.Count > 0;
    public bool IsProcessing => _processing;
  }

  public class LobbyJobManager
  {
    public static LobbyJobManager Instance { get; } = new LobbyJobManager();

    public LobbyRoom LobbyRoom = LobbyRoom.Instance;

    private static int workerThreadCount = 0; // 병렬 실행 스레드 수
    private static readonly ConcurrentDictionary<int, LobbyJobQueue> _jobQueueMap = new();
    private static readonly ConcurrentQueue<int> _executeQueue = new();
    private static int _roundRobinIndex = 0;

    public static void LuanchLobbyThreads(int threadCount)
    {
      workerThreadCount = threadCount;

      for (int i = 0; i < threadCount; i++)
      {
        Thread t = new Thread(new ParameterizedThreadStart(Run));
        t.Name = $"LobbyWorkerThread-{i}";
        t.Start(i);
      }
    }

    public static void Push(int playerId, Action action)
    {
      if (_jobQueueMap.ContainsKey(playerId) == false)
      {
        _jobQueueMap.TryAdd(playerId, new LobbyJobQueue());
        _executeQueue.Enqueue(playerId);
      }

      _jobQueueMap[playerId].Enqueue(() =>
      {
        action.Invoke();
        FinishProcessing(playerId);
      });
    }

    public static void Run(object arg)
    {
      int threadId = (int)arg;

      while (true)
      {
        if (_executeQueue.TryDequeue(out int playerId) == false)
          continue;

        if (ContainsKey(playerId) == false)
          continue;

        Action action = TryPop(playerId);
        if (action != null)
          action.Invoke();

       // if (queue.HasPendingJobs && !queue.IsProcessing)
       //   _executeQueue.Enqueue(playerId);

        _executeQueue.Enqueue(playerId);

        Thread.Sleep(0);
      }
    }

    private static bool ContainsKey(int playerDbid)
    {
      return _jobQueueMap.ContainsKey(playerDbid);
    }
    private static Action TryPop(int playerId)
    {
      if (_jobQueueMap.TryGetValue(playerId, out LobbyJobQueue queue) == false)
        return null;

      return queue.TryDequeue();
    }

    private static void FinishProcessing(int playerId)
    {
      if (_jobQueueMap.TryGetValue(playerId, out LobbyJobQueue queue))
        queue.FinishProcessing();
    }
    public static void Clear(int playerId)
    {
      _jobQueueMap.TryRemove(playerId, out LobbyJobQueue queue);
    }

  }
}

