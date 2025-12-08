using Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  public sealed class RoomWorker
  {
    readonly Thread _thread;
    readonly List<Room> _rooms = new();
    readonly object _lock = new();

    readonly int _hz;
    volatile bool _running = true;
    public bool DoSessionTimeoutCheck { get; set; } = false;

    public RoomWorker(int hz, string name)
    {
      _hz = hz;
      _thread = new Thread(Loop) { IsBackground = true, Name = name };
      _thread.Start();
    }

    public int RoomCount { get { lock (_lock) return _rooms.Count; } }

    public void Add(Room room)
    {
      if (room == null) return;
      lock (_lock)
      {
        _rooms.Add(room);
        room.Worker = this;
      }
    }

    public void Remove(Room room)
    {
      if (room == null) return;
      lock (_lock)
      {
        _rooms.Remove(room);
        room.Worker = null;
      }
    }

    void Loop()
    {
      var sw = System.Diagnostics.Stopwatch.StartNew();
      double targetMs = 1000.0 / _hz;

      // 타임아웃 체크용 누적 타이머
      double timeoutAccMs = 0.0;
      const double timeoutIntervalMs = 1000.0; // 1초마다 한 번

      while (_running)
      {
        long frameStart = sw.ElapsedMilliseconds;

        List<Room> rooms;
        lock (_lock) rooms = _rooms.ToList();

        float dt = (float)(1.0 / _hz);

        // 1) Room.Update
        foreach (var r in rooms)
          r.Update(dt);

        // 2) GameRoom만 FixedUpdate
        foreach (var r in rooms)
          if (r is GameRoom gr) gr.FixedUpdate(dt);

        long elapsed = sw.ElapsedMilliseconds - frameStart;

        // 실제 지난 시간만큼 누적
        timeoutAccMs += elapsed;

        // 3) 세션 타임아웃 체크
        if (DoSessionTimeoutCheck && timeoutAccMs >= timeoutIntervalMs)
        {
          timeoutAccMs = 0.0;
          SessionManager.Instance.CheckTimeout();
        }

        int sleep = (int)Math.Max(0, targetMs - elapsed);
        if (sleep > 0)
          Thread.Sleep(sleep);
      }
    }
  }
}

