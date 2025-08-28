using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;



namespace GameServer.Game
{
  public partial class Hero : BaseObject
  {
    public override void ApplyMove(Vector3 dir, float speed, float deltaTime)
    {
      Vector3 cleanDir = new Vector3(dir.X, 0, dir.Z);

      if (cleanDir.LengthSquared() < 0.001f)
      {
        return;
      }
      Vector3 normalizedDir = Vector3.Normalize(cleanDir);
      Vector3 delta = normalizedDir * speed * deltaTime;
      Vector3 newPos = new Vector3(PosInfo.PosX + delta.X, 0, PosInfo.PosZ + delta.Z);
      //충돌체크
      var grid = DataManager.ObstacleGrid;
      if (grid.IsBlockedXZ(newPos))
        return;


      if (EHeroLowerState == EHeroLowerState.Eindle)
      {
        PosInfo.DirX = MoveDir.X;
        PosInfo.DirY = 0;
        PosInfo.DirZ = MoveDir.Z;
        PosInfo.PosX = Position.X;
        PosInfo.PosY = 0;
        PosInfo.PosZ = Position.Z;
      }
      else
      {
        PosInfo.DirX = normalizedDir.X;
        PosInfo.DirY = 0;
        PosInfo.DirZ = normalizedDir.Z;
        PosInfo.PosX = newPos.X;
        PosInfo.PosY = 0;
        PosInfo.PosZ = newPos.Z;
      }
    }

    public void OnDamaged(int damage, Hero attacker)
    {
      CurHp -= damage;
      if (CurHp <= 0)
      {
        CurHp = 0;
        OnDead(attacker);
      }
      S_HeroChangeHp hpPkt = new S_HeroChangeHp()
      {
        ObjectId = this.ObjectID,
        CurHp = this.CurHp,
        MaxHp = this.MaxHp
      };

      Room?.Broadcast(hpPkt); // 🔁 클라 전체에게 HP 갱신 전송
      // HP 갱신 패킷 전송 등 처리
    }


    public void OnDead(Hero attacker)
    {
      //킬 갱신//
    }

  }
}
