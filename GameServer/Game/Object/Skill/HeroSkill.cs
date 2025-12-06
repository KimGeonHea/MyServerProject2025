using GameServer.Game.Room;
using GameServer.Utils;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Numerics;

namespace GameServer.Game
{
  public class HeroSkill : BaseObject, IPoolable
  {
    protected float gravity = -9.8f;

    public HeroSkillData heroSkillData;

    public int OwnerId { get; set; }
    public Hero Owner { get; set; }

    public int damage;

    protected bool _exploded;

    const int skillIndex = 1;
    public virtual void OnSpawned()
    {
      _exploded = false;
      Position = Vector3.Zero;
      MoveDir = Vector3.Zero;
      IsAlive = true;
    }

    public virtual void OnDespawned() 
    {
      //ObjectID = 0;
      //Owner = null;
      //Position = Vector3.Zero;
      //MoveDir = Vector3.Zero;
      //heroSkillData = null;
      //damage = 0;
    }

    public virtual void Init(Hero owner, Vector3 direction, Vector3 targetPosition)
    {
      Owner = owner;
      OwnerId = owner.ObjectID;

      TempleteID = owner.TempleteID + skillIndex;
      if (DataManager.HeroSkilldataDict.TryGetValue(TempleteID, out HeroSkillData data))
      {
        heroSkillData = data;
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