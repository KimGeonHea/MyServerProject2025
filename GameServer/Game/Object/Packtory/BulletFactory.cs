using GameServer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Object.Packtory
{
  public class BulletFactory
  {
    // 템플릿 ID -> 벌릿 풀
    private readonly Dictionary<int, ObjectPool<HeroBullet>> _pools =
      new Dictionary<int, ObjectPool<HeroBullet>>();

    // 기본 벌릿 풀 (등록 안 된 템플릿용)
    private readonly ObjectPool<HeroBullet> _defaultPool;

    public BulletFactory()
    {
      _defaultPool = new ObjectPool<HeroBullet>(() => new HeroBullet());

      // 후디 기본 공격 벌릿
      _pools[Define.HOODIE_TEMPLATE_ID] =
        new ObjectPool<HeroBullet>(() => new HeroBullet());
      // 솔저 기본 공격 벌릿
      _pools[Define.SOLDIER_TEMPLATE_ID] =
        new ObjectPool<HeroBullet>(() => new HeroBullet());

    }

    /// <summary>
    /// owner 기준으로 “이 영웅의 기본 공격 벌릿” 객체를 빌려온다.
    /// </summary>
    public HeroBullet RentBullet(Hero owner)
    {
      if (owner == null)
        return null;

      // 지금 구조상 : 영웅 템플릿ID = 벌릿 종류 결정 키로 사용
      int bulletTemplateId = owner.TempleteID;

      if (_pools.TryGetValue(bulletTemplateId, out var pool))
      {
        var bullet = pool.Rent();
        // 나중에 반환할 때 어떤 풀에서 왔는지 알 수 있게 TempleteId에 키 저장
        bullet.TempleteId = bulletTemplateId;
        return bullet;
      }
      else
      {
        var bullet = _defaultPool.Rent();
        bullet.TempleteId = 0; // 기본 풀
        return bullet;
      }
    }

    public void ReturnBullet(HeroBullet bullet)
    {
      if (bullet == null)
        return;

      int key = bullet.TempleteId;

      if (key != 0 && _pools.TryGetValue(key, out var pool))
      {
        pool.Return(bullet);
      }
      else
      {
        _defaultPool.Return(bullet);
      }
    }
  }
}
