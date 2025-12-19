using GameServer.Game;
using GameServer.Game.Object;
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
    // 풀//
    public bool IsAlive = false;
    public Room Room { get; set; } = null;  
    //포지션 , 오브젝트 정보//
    public PositionInfo PositionInfo { get => positionInfo;  set => positionInfo = value; }  
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


    //public Vector3 Direction { get; set; } = new Vector3(0, 0, 0);
    public Vector3 Direction
    {
      get => new Vector3(PositionInfo.DirX, PositionInfo.DirY, PositionInfo.DirZ);
      set
      {
        PositionInfo.DirX = value.X;
        PositionInfo.DirY = value.Y;
        PositionInfo.DirZ = value.Z;
      }
    }

    public Vector3 Position
    {
      get => new Vector3(PositionInfo.PosX, PositionInfo.PosY, PositionInfo.PosZ);
      set
      {
        PositionInfo.PosX = value.X;
        PositionInfo.PosY = value.Y;
        PositionInfo.PosZ = value.Z;
      }
    }
    /// <summary>
    /// 바라보는 방향(전방). 기본적으로 MoveDir을 사용.
    /// </summary>
    public Vector3 Forward
    {
      get
      {
        // Y는 항상 0으로 (XZ 평면 기준 방향만 사용)
        Vector3 dir = new Vector3(Direction.X, 0f, Direction.Z);

        // 방향이 0,0,0 이면 기본값 (0,0,1) 사용
        if (dir.LengthSquared() < 1e-6f)
          return new Vector3(0f, 0f, 1f);

        return Vector3.Normalize(dir);
      }
    }
    // <summary>
    /// 뒤쪽 방향
    /// </summary>
    public Vector3 Backward => -Forward;

    /// <summary>
    /// 오른쪽 방향
    /// </summary>
    public Vector3 Right
    {
      get
      {
        // 오른쪽 = up(0,1,0) x forward
        Vector3 right = Vector3.Cross(Vector3.UnitY, Forward);
        if (right.LengthSquared() < 1e-6f)
          return new Vector3(1f, 0f, 0f);

        return Vector3.Normalize(right);
      }
    }

    /// <summary>
    /// 왼쪽 방향
    /// </summary>
    public Vector3 Left => -Right;

    /// <summary>
    /// 월드 기준 위 방향 (고정)
    /// </summary>
    public Vector3 Up => Vector3.UnitY;

    public virtual void FixedUpdate(float deltaTime) { }
    public virtual void Update(float deltaTime) { }
    public virtual void ApplyMove(Vector3 dir, float speed, float deltaTime) { }

    //public virtual void OnDamage(DamageContext ctx) { }

  }
}
