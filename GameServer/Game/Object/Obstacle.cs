using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Object
{
  public class Obstacle
  {
    public string Type;
    public Vector3 Center;
    public Vector3 Size;

    public bool IsBlockedXZ(Vector3 pos)
    {
      Vector3 half = Size * 0.5f;
      float minX = Center.X - half.X;
      float maxX = Center.X + half.X;
      float minZ = Center.Z - half.Z;
      float maxZ = Center.Z + half.Z;

      bool blocked = pos.X >= minX && pos.X <= maxX &&
                     pos.Z >= minZ && pos.Z <= maxZ;

      if (blocked)
      {
        Console.WriteLine($"충돌: Pos={pos}, Center={Center}, Size={Size}");
      }

      return blocked;
    }

    public bool IsBlockedY(Vector3 pos)
    {
      Vector3 half = Size * 0.5f;
      float minX = Center.X - half.X;
      float maxX = Center.X + half.X;
      float minY = Center.Y - half.Y;
      float maxY = Center.Y + half.Y;
      float minZ = Center.Z - half.Z;
      float maxZ = Center.Z + half.Z;

      bool blocked = pos.X >= minX && pos.X <= maxX &&
                     pos.Y >= minY && pos.Y <= maxY &&
                     pos.Z >= minZ && pos.Z <= maxZ;

      if (blocked)
      {
        Console.WriteLine($"충돌: Pos={pos}, Center={Center}, Size={Size}");
      }

      return blocked;
    }
  }
}
