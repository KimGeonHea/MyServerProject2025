using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Utils
{

  public interface IPoolable
  {
    void OnSpawned();   // 풀에서 꺼낼 때 호출 (초기화)
    void OnDespawned(); // 풀로 반환될 때 호출 (정리)
  }
  public class ObjectPool<T> where T : class, IPoolable, new()
  {
    private ConcurrentStack<T> _pool = new ConcurrentStack<T>();

    public T Rent()
    {
      if (_pool.TryPop(out T item))
      {
        item.OnSpawned(); // 상태 초기화
        return item;
      }

      T newItem = new T();
      newItem.OnSpawned();
      return newItem;
    }

    public void Return(T item)
    {
      item.OnDespawned(); // 상태 정리
      _pool.Push(item);
    }

    public int Count => _pool.Count;
  }
}
