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

    public int TempleteId;
    public HeroSkillData heroSkillData;

    public int OwnerId { get; set; }
    public Hero Owner { get; set; }

    public int damage;
 

    public virtual void OnSpawned()
    {
      ObjectID = 0;
      Owner = null;
      Position = Vector3.Zero;
      MoveDir = Vector3.Zero;

    }

    public virtual void OnDespawned() { }

    public virtual void Init(Hero owner, Vector3 direction, Vector3 targetPosition)
    {
      Owner = owner;
      OwnerId = owner.ObjectID;

      TempleteId = owner.TemplatedId + 1;
      if (DataManager.heroSkillDict.TryGetValue(TempleteId, out HeroSkillData data))
      {
        heroSkillData = data;
      }
    }
  

    public override void FixedUpdate(float deltaTime)
    {
      base.FixedUpdate(deltaTime);
     
    }


  }
}