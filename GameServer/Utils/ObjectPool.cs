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

    private readonly Func<T> _factory;

    // 기본 생성자 : 기존처럼 new T() 사용
    public ObjectPool()
    {
      _factory = () => new T();
    }

    // 커스텀 팩토리 사용
    public ObjectPool(Func<T> factory)
    {
      _factory = (factory != null) ? factory : (() => new T());
    }
    public T Rent()
    {
      if (_pool.TryPop(out T item))
      {
        item.OnSpawned();
        return item;
      }

      T newItem = _factory();
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
