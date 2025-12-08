using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  public class GameRoomPool
  {

    private readonly ConcurrentQueue<GameRoom> _pool = new();
    private readonly ConcurrentDictionary<GameRoom, byte> _inPool = new(); // 중복 반납 방지용 멤버십
    private readonly int _capacity;           // 풀 최대 보관 개수
    private int _count;                       // 현재 풀에 보관된 개수(원자적 관리)

    public GameRoomPool(int capacity = 2000)
    {
      _capacity = Math.Max(0, capacity);
      _count = 0;

      Prewarm(200);
    }

    public int Count => Volatile.Read(ref _count);

    // 서버 기동 시 미리 생성(선택)
    public void Prewarm(int count)
    {
      for (int i = 0; i < count; i++)
      {
        var room = new GameRoom();
        if (!TryEnqueue(room)) 
          break;
      }
    }

    public GameRoom Rent()
    {
      if (_pool.TryDequeue(out var room))
      {
        // 풀에서 성공적으로 하나 꺼냈으므로 카운터/멤버십 정리
        Interlocked.Decrement(ref _count);
        _inPool.TryRemove(room, out _);
        // 활성화(IsActive=true)는 룸 스레드에서 room.Init(...)로 처리
        return room;
      }

      // 풀 비었으면 새로 생성
      return new GameRoom();
    }

    public void Return(GameRoom room)
    {
      if (room == null) 
        return;

      // 아직 활성/워커에 붙어 있으면 반납 금지
      // (Room.IsAlive: (Worker != null) && IsActive)
      if (room.IsAlive) 
        return;

      // 안전망: 정리(idempotent 가정)
      room.Close();        // IsActive=false, players/baseObjects 클리어
      room.ResetForPool(); // 참조/카운터 초기화

      // 용량/중복 반납 체크 후 큐에 삽입
      TryEnqueue(room);
    }

    private bool TryEnqueue(GameRoom room)
    {
      while (true)
      {
        // 이미 풀 안에 있으면(중복 반납) 무시
        if (!_inPool.TryAdd(room, 0))
          return false;

        int old = Volatile.Read(ref _count);
        if (old >= _capacity)
        {
          // 용량 초과: 멤버십 롤백하고 버림(가비지 컬렉션 대상)
          _inPool.TryRemove(room, out _);
          return false;
        }

        // _count 원자 증가 성공 시 큐에 넣기
        if (Interlocked.CompareExchange(ref _count, old + 1, old) == old)
        {
          _pool.Enqueue(room);
          return true;
        }

        // CAS 실패: 멤버십 롤백 후 재시도
        _inPool.TryRemove(room, out _);
        // 루프 재시도 (다른 스레드가 먼저 올렸을 수 있음)
      }
    }
  }
}
