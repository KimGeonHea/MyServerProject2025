using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class BearBullet : HeroBullet
  {
    public override void ApplyMove(Vector3 dir, float speed, float deltaTime)
    {
      base.ApplyMove(dir, speed, deltaTime);
    }

    public override void FixedUpdate(float deltaTime)
    {
      base.FixedUpdate(deltaTime);
    }

    public override void Update(float deltaTime)
    {
      base.Update(deltaTime);
    }
  }
}
