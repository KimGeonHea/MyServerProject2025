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

      while (_running)
      {
        long frameStart = sw.ElapsedMilliseconds;

        List<Room> rooms;
        lock (_lock) rooms = _rooms.ToList();

        float dt = (float)(1.0 / _hz);

        // Flush 포함: Room.Update 안에서 Flush() 호출한다고 가정
        foreach (var r in rooms)
          r.Update(dt);

        // GameRoom만 고정틱
        foreach (var r in rooms)
          if (r is GameRoom gr) gr.FixedUpdate(dt);

        long elapsed = sw.ElapsedMilliseconds - frameStart;
        int sleep = (int)Math.Max(0, targetMs - elapsed);
        if (sleep > 0) 
          Thread.Sleep(sleep);
      }
    }
  }
}

