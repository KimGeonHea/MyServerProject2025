using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Utils
{
  public class VectorUtil
  {
    //public static float Distance(Vector3 a, Vector3 b)
    //{
    //  return (a - b).Magnitude;
    //}
    //
    //public static float Dot(Vector3 a, Vector3 b)
    //{
    //  return a.x * b.x + a.y * b.y + a.z * b.z;
    //}
    //
    //public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
    //{
    //  t = Clamp(t, 0f, 1f);
    //  return a + (b - a) * t;
    //}
    //
    //public static float Clamp(float value, float min, float max)
    //{
    //  if (value < min) return min;
    //  else if (value > max) return max;
    //  else return value;
    //}
    //
    //public static int Clamp(int value, int min, int max)
    //{
    //  if (value < min) return min;
    //  else if (value > max) return max;
    //  else return value;
    //}

  }

  public struct Vector3Int : IEquatable<Vector3Int>
  {
    public int X, Y, Z;
    public Vector3Int(int x, int y, int z)
    {
      X = x; Y = y; Z = z;
    }

    public bool Equals(Vector3Int other) => X == other.X && Y == other.Y && Z == other.Z;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override bool Equals(object obj) => obj is Vector3Int other && Equals(other);
  }

  public struct Vector2Int : IEquatable<Vector2Int>
  {
    public int X;
    public int Y;

    public Vector2Int(int x, int y)
    {
      this.X = x;
      this.Y = y;
    }

    public override bool Equals(object obj)
    {
      return obj is Vector2Int other && Equals(other);
    }

    public bool Equals(Vector2Int other)
    {
      return X == other.X && Y == other.Y;
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(X, Y);
    }

    public override string ToString()
    {
      return $"({X}, {Y})";
    }

    public static bool operator ==(Vector2Int left, Vector2Int right) => left.Equals(right);
    public static bool operator !=(Vector2Int left, Vector2Int right) => !(left == right);
  }
}
