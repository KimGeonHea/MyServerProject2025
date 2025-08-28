using GameServer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Object
{
  public class Grid
  {
    private readonly float cellSize;
    private readonly Dictionary<Vector3Int, List<Obstacle>> grid = new();

    public Grid(float cellSize)
    {
      this.cellSize = cellSize;
    }

    private Vector2Int GetGridCell(Vector3 pos)
    {
      int x = (int)Math.Floor(pos.X / cellSize);
      int z = (int)Math.Floor(pos.Z / cellSize);
      return new Vector2Int(x, z);
    }

    public void AddObstacle(Obstacle obs)
    {
      Vector3 half = obs.Size * 0.5f;

      int minX = (int)Math.Floor((obs.Center.X - half.X) / cellSize);
      int maxX = (int)Math.Floor((obs.Center.X + half.X) / cellSize);

      int minY = (int)Math.Floor((obs.Center.Y - half.Y) / cellSize);
      int maxY = (int)Math.Floor((obs.Center.Y + half.Y) / cellSize);

      int minZ = (int)Math.Floor((obs.Center.Z - half.Z) / cellSize);
      int maxZ = (int)Math.Floor((obs.Center.Z + half.Z) / cellSize);

      for (int x = minX; x <= maxX; x++)
      {
        for (int y = minY; y <= maxY; y++)
        {
          for (int z = minZ; z <= maxZ; z++)
          {
            var cell = new Vector3Int(x, y, z);
            if (!grid.ContainsKey(cell))
              grid[cell] = new List<Obstacle>();

            grid[cell].Add(obs);
          }
        }
      }
    }

    public bool IsBlocked(Vector3 pos)
    {
      int x = (int)Math.Floor(pos.X / cellSize);
      int y = (int)Math.Floor(pos.Y / cellSize);
      int z = (int)Math.Floor(pos.Z / cellSize);
      var cell = new Vector3Int(x, y, z);

      if (grid.TryGetValue(cell, out var list))
      {
        foreach (var obs in list)
        {
          if (obs.IsBlockedY(pos))
            return true;
        }
      }

      return false;
    }

    public bool IsBlockedXZ(Vector3 pos)
    {
      int x = (int)Math.Floor(pos.X / cellSize);
      int y = (int)Math.Floor(pos.Y / cellSize);
      int z = (int)Math.Floor(pos.Z / cellSize);
      var cell = new Vector3Int(x, y, z);

      if (grid.TryGetValue(cell, out var list))
      {
        foreach (var obs in list)
        {
          if (obs.IsBlockedXZ(pos))
            return true;
        }
      }

      return false;
    }

    //public bool IsBlockedY(Vector3 pos)
    //{
    //  Vector2Int cell = GetGridCell(pos);
    //
    //  if (grid.TryGetValue(cell, out var list))
    //  {
    //    foreach (var obs in list)
    //    {
    //      if (obs.IsBlockedY(pos))
    //        return true;
    //    }
    //  }
    //  return false;
    //}
  }
}


