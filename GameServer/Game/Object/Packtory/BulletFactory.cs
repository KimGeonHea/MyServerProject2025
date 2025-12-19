using GameServer.Game.Room;
using GameServer.Utils;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Object.Packtory
{
  public class BulletFactory
  {
    // 템플릿 ID 벌릿 풀
    private readonly Dictionary<int, ObjectPool<HeroBullet>> pools = new Dictionary<int, ObjectPool<HeroBullet>>();

    // 기본 벌릿 풀 (등록 안 된 템플릿용)
    private readonly ObjectPool<HeroBullet> defaultPool;

    /// <summary>
    /// 팩터로 생성
    /// </summary>
    public BulletFactory()
    {
      defaultPool = new ObjectPool<HeroBullet>(() => new HeroBullet());

      // 후디 기본 공격 벌릿
      pools[Define.HERO_TEMPLATE_HOODIE] = new ObjectPool<HeroBullet>(() => new HeroBullet());
      // 솔저 기본 공격 벌릿
      pools[Define.HERO_TEMPLATE_SOLDIER] = new ObjectPool<HeroBullet>(() => new SoldierBullet());

      pools[Define.HERO_TEMPLATE_BEAR] = new ObjectPool<HeroBullet>(() => new BearBullet());
    }

    /// <summary>
    /// 이젠 여기서 부터 사용 처리
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="dir"></param>
    /// <param name="targetPos"></param>
    /// <param name="room"></param>
    /// <returns></returns>
    public HeroBullet UseBullet(Hero owner, Vector3 dir, Vector3 targetPos, GameRoom room)
    {
      if (owner == null || room == null)
        return null;

      int bulletTemp = owner.TempleteID;

      if (!DataManager.HeroSkilldataDict.TryGetValue(bulletTemp, out HeroSkillData data))
        return null;

      switch (data.SkillType)
      {
        case EHeroSkillType.EskillTypeProjectile:
        case EHeroSkillType.EskillTypeHomingProjectile:
          return SpawnProjectileBullet(owner, dir, targetPos, room, bulletTemp);

        case EHeroSkillType.EskillTypeDash:
          //ExecuteTeleportLikeDash(owner, dir, targetPos, room, data);
          return null;

        case EHeroSkillType.EskillTypeAreaexplosion:
          //ExecuteAreaExplosion(owner, targetPos, room, data);
          return null;

        case EHeroSkillType.EskillTypeCone:
          return SpawnConeBullet(owner, dir, targetPos, room, bulletTemp);

        default:
          return null;
      }
    }
    private HeroBullet SpawnProjectileBullet(Hero owner, Vector3 dir, Vector3 targetPos, GameRoom room, int bulletTemplateId)
    {
      if (!pools.TryGetValue(bulletTemplateId, out var pool))
        pool = defaultPool;

      HeroBullet bullet = pool.Rent();    
      bullet.TempleteID = bulletTemplateId;
      bullet.Init(owner, dir, targetPos);
      room.EnterGame(bullet);                //  Room 세팅
      return bullet;
    }
    private HeroBullet SpawnConeBullet( Hero owner,Vector3 dir,Vector3 targetPos,GameRoom room,int bulletTemplateId)
    {
      if (!pools.TryGetValue(bulletTemplateId, out var pool))
        pool = defaultPool;

      HeroBullet bullet = pool.Rent();
      bullet.TempleteID = bulletTemplateId;
      bullet.Init(owner, dir, targetPos);
      room.EnterGame(bullet);
      return bullet;
    }


    /// <summary>
    /// owner 기준으로 “이 영웅의 기본 공격 벌릿” 객체를 빌려온다.
    /// </summary>
    //public HeroBullet RentBullet(Hero owner)
    //{
    //  if (owner == null)
    //    return null;
    //
    //  // 지금 구조상 : 영웅 템플릿ID = 벌릿 종류 결정 키로 사용
    //  int bulletTemplateId = owner.TempleteID;
    //
    //  if (_pools.TryGetValue(bulletTemplateId, out var pool))
    //  {
    //    var bullet = pool.Rent();
    //    // 나중에 반환할 때 어떤 풀에서 왔는지 알 수 있게 TempleteId에 키 저장
    //    bullet.TempleteID = bulletTemplateId;
    //    return bullet;
    //  }
    //  else
    //  {
    //    var bullet = _defaultPool.Rent();
    //    bullet.TempleteID = 0; // 기본 풀
    //    Console.WriteLine($"bullet :_defaultPool.Rent ,  bullet.TempleteID : {bullet.TempleteID} ");
    //    return bullet;
    //  }
    //}

    public void ReturnBullet(HeroBullet bullet)
    {
      if (bullet == null)
        return;

      int key = bullet.TempleteID;

      if (key != 0 && pools.TryGetValue(key, out var pool))
      {
        pool.Return(bullet);
      }
      else
      {
        defaultPool.Return(bullet);
      }
    }
  }
}
