using GameServer.Game;
using GameServer.Game.Object;
using GameServer.Game.Object.Creature;
using GameServer.Game.Room;
using Google.Protobuf.Protocol;
using Server;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace GameServer
{
  public class BaseObject
  {
    public bool IsAlive = false;
    public Room Room { get; set; } = null;  
    public PositionInfo PosInfo { get => positionInfo;  set => positionInfo = value; }  

    public ObjectInfo ObjectInfo { get => objectInfo; set => objectInfo = value; }
    private ObjectInfo objectInfo { get; set; } = new ObjectInfo();

    private PositionInfo positionInfo = new PositionInfo();
    public int ObjectID
    {
      get => objectInfo.ObjectId;
      set => objectInfo.ObjectId = value; 
    }

    public virtual int TempleteID 
    { 
      get => objectInfo.TemplateId; 
      set => objectInfo.TemplateId =value; 
    }

    public EGameObjectType ObjectType
    {
      get => objectInfo.ObjectType;
      set => objectInfo.ObjectType = value; 
    } 


    public Vector3 MoveDir { get; set; } = new Vector3(0, 0, 0);
    public Vector3 Position
    {
      get => new Vector3(PosInfo.PosX, PosInfo.PosY, PosInfo.PosZ);
      set
      {
        PosInfo.PosX = value.X;
        PosInfo.PosY = value.Y;
        PosInfo.PosZ = value.Z;
      }
    }

    public virtual void FixedUpdate(float deltaTime) { }
    public virtual void Update(float deltaTime) { }
    public virtual void ApplyMove(Vector3 dir, float speed, float deltaTime) { }
    //public virtual void OnDamage(DamageContext ctx) { }

  }
}
