using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class HoodieBullet : HeroBullet
  {
    public override void ApplyMove(Vector3 dir, float speed, float deltaTime)
    {
      base.ApplyMove(dir, speed, deltaTime);
    }

    public override void FixedUpdate(float deltaTime)
    {
      base.FixedUpdate(deltaTime);
    }

    public override void Init(Hero owner, Vector3 direction, Vector3 startPos)
    {
      base.Init(owner, direction, startPos);
    }

    public override void Update(float deltaTime)
    {
      base.Update(deltaTime);
    }

    protected override void CheckCollision()
    {
      base.CheckCollision();
    }
  }

}
