using GameServer.Game.Room;
using GameServer.Utils;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Diagnostics.Contracts;
using System.Numerics;

namespace GameServer.Game
{
  public class HeroSkill : BaseObject, IPoolable
  {
    protected float gravity = -9.8f;
    protected bool exploded;
    /// <summary>
    /// 여기서부터 데이터
    /// </summary>
    public HeroSkillData heroSkillData;
    protected float Speed;
    protected float Range;
    protected float Radius;
    protected float CcPower;
    protected float CcDuration;

    /// <summary>
    /// 여기서부터 오우더 
    /// </summary>
    public int OwnerId { get; set; }
    public Hero Owner { get; set; }

    protected int damage;


    const int skillIndex = 1;
    public virtual void OnSpawned()
    {
      IsAlive = true;
      exploded = false;
      ObjectID = 0;
      Owner = null;
      Position = Vector3.Zero;
      Direction = Vector3.Zero;
      damage = 0;
    }

    public virtual void OnDespawned()
    {
      IsAlive = false;
      //exploded = false;
      //ObjectID = 0;
      //Owner = null;
      //Position = Vector3.Zero;
      //Direction = Vector3.Zero;
      //damage = 0;
    }
    /// <summary>
    /// Skill 초기화
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="direction"></param>
    /// <param name="targetPosition"></param>
    public virtual void Init(Hero owner, Vector3 direction, Vector3 targetPosition)
    {
      Owner = owner;
      OwnerId = owner.ObjectID;
      TempleteID = owner.TempleteID + skillIndex;

      ObjectType = EGameObjectType.Skill;
      if (DataManager.HeroSkilldataDict.TryGetValue(TempleteID, out HeroSkillData data))
      {
        heroSkillData = data;
        Speed = data.Speed;
        Range = data.Range;
        Radius = data.Radius;
        CcPower = data.CcPower;
        CcDuration = data.CcDuration;
      }
    }
  

    public override void FixedUpdate(float deltaTime)
    {
      if(IsAlive == false)
        return;
      base.FixedUpdate(deltaTime);
     
    }


  }
}