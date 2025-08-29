using GameServer.Game.Object;
using GameServer.Utils;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.Extensions.Logging;
using Server.Data;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using ObjectManager = GameServer.Game.Object.ObjectManager;

namespace GameServer.Game.Room
{
  public partial class GameRoom : Room
  {

    public Dictionary<int/*OjbectId*/, Hero> heroes = new Dictionary<int, Hero>();

    
    
    public override void EnterGame(Player player)
    {
      // 0) 가드
      if (player == null || !IsAlive)
        return;

      // 1) 1vs1 방 용량 체크(넘치면 무시 or 거절 정책)
      if (players.Count >= 2)
        return;

      base.EnterGame(player);

      // 3) 세션 상태 전환
      player.Session.ServerState = PlayerServerState.ServerStateGame;

      // 4) 영웅 접속 처리
      var hero = player.selectHero;                // (오타 주의: selectHero 일관)
      if (hero != null)
      {
        // 영웅을 '플레이어와 동일한 ObjectID'로 쓸 거라면 그대로 매핑
        hero.ObjectID = player.ObjectID;
        hero.Room = this;

        // 장비/스탯 합산
        hero.SetTotalData(player.inventory.GetTotalDataEquipItems());


        base.EnterGame(hero);
        // 등록(키를 hero.ObjectID로 통일해 두면 찾기 쉬움)
        heroes[hero.ObjectID] = hero;
      }


      // 6) 2명 모이면 시작
      if (players.Count == 2)
        TryStartGame();
    }

   

    public void TryStartGame()
    {
      var playerList = players.Values.ToList();
      if (playerList.Count < 2)
        return;

      Player p1 = playerList[0];
      Player p2 = playerList[1];

      S_EnterMultyGame s_EnterMultyGameP1 = new S_EnterMultyGame()
      {
        FHeroInfo = MakeHeroInfo(p1),
        SHroInfo = MakeHeroInfo(p2)
      };
      p1.Session?.Send(s_EnterMultyGameP1);

      S_EnterMultyGame s_EnterMultyGameP2 = new S_EnterMultyGame()
      {
        FHeroInfo = MakeHeroInfo(p1),
        SHroInfo = MakeHeroInfo(p2)
      };
      p2.Session?.Send(s_EnterMultyGameP2);

      
    }

    private HeroInfo MakeHeroInfo(Player player)
    {
      Hero hero = player.selectHero;
      return new HeroInfo()
      {
        ObjectInfo = MakeObjectInfo(hero),
        PosInfo = new PositionInfo()
        {
          PosX = hero.PosInfo.PosX,
          PosY = hero.PosInfo.PosY,
          PosZ = hero.PosInfo.PosZ,
          DirX = hero.PosInfo.DirX,
          DirY = hero.PosInfo.DirY,
          DirZ = hero.PosInfo.DirZ,
        }
      };
    }


    private ObjectInfo MakeObjectInfo(Hero obj)
    {
      var info = new ObjectInfo()
      {
        ObjectId = obj.ObjectID,
        TemplateId = obj.TempleteID,
        ObjectType = obj.ObjectType
      };

      return info;
    }


    public override void Update(float deltatime)
    {
      base.Update(deltatime);
    }

    public void FixedUpdate(float deltaTime)
    {
      FixedUpdateHero(deltaTime);
      FixedUpdateObject(deltaTime);
    }

    void FixedUpdateHero(float deltaTime)
    {
      foreach (var player in players.Values)
      {
        Hero hero = player.selectHero;
        if (hero == null) continue;
        hero.ApplyMove(hero.MoveDir, hero.HeroData.MoveSpeed, deltaTime); // 예시로 0.02초 간격으로 이동 적용
        S_HeroMove s_move = CreateS_HeroMove(hero);
        Broadcast(s_move);
      }
    }
    void FixedUpdateObject(float deltaTime)
    {

      foreach (var obj in baseObjects.Values)
      {
        if (obj is HeroBullet bullet)
        {
          bullet.FixedUpdate(deltaTime);

          // 이동 브로드캐스트
          S_HeroShotMove movePkt = CreateHeroShotMove(bullet);
          Console.WriteLine($"[Packet Pos] = {movePkt.PosInfo.PosX}, {movePkt.PosInfo.PosY}, {movePkt.PosInfo.PosZ}");
          Console.WriteLine($"[Packet Dir] = {movePkt.PosInfo.DirX}, {movePkt.PosInfo.DirY}, {movePkt.PosInfo.DirZ}");
          Broadcast(movePkt);
        }

        if(obj is HeroSkill skill)
        {
          skill.FixedUpdate(deltaTime);
          S_HeroSkillMove movePkt = CreateHeroSkillMove(skill);
          Broadcast(movePkt);
        }
      }
    }
    private S_HeroShotMove CreateHeroShotMove(HeroBullet bullet)
    {
      return new S_HeroShotMove
      {
        ObjectInfo = new ObjectInfo
        {
          ObjectId = bullet.ObjectID,
          TemplateId = bullet.TempleteID,
          ObjectType = bullet.ObjectType
        },
        PosInfo = new PositionInfo
        {
          PosX = bullet.Position.X,
          PosY = bullet.Position.Y,
          PosZ = bullet.Position.Z,
          DirX = bullet.MoveDir.X,
          DirY = bullet.MoveDir.Y,
          DirZ = bullet.MoveDir.Z
        }
      };
    }

    private S_HeroMove CreateS_HeroMove(Hero hero)
    {
      return new S_HeroMove
      {
        HeroInfo = new HeroInfo
        {
          ObjectInfo = new ObjectInfo
          {
            ObjectId = hero.ObjectID,
            TemplateId = hero.TempleteID,
            ObjectType = hero.ObjectType
          },
          UpperState = hero.EHeroUpperState,
          LowerState = hero.EHeroLowerState,
          PosInfo = new PositionInfo
          {
            PosX = hero.PosInfo.PosX,
            PosY = 0,//hero.PosInfo.PosY,
            PosZ = hero.PosInfo.PosZ,
            DirX = hero.PosInfo.DirX,
            DirY = hero.PosInfo.DirY,
            DirZ = hero.PosInfo.DirZ
          }
        }
      };
    }


    private S_HeroSkillMove CreateHeroSkillMove(HeroSkill skill)
    {
      S_HeroSkillMove movepkt = new S_HeroSkillMove
      {
        ObjectInfo = new ObjectInfo
        {
          ObjectId = skill.ObjectID,
          TemplateId = skill.TempleteID,
          ObjectType = skill.ObjectType
        },
        PosInfo = new PositionInfo
        {
          PosX = skill.Position.X,
          PosY = skill.Position.Y,
          PosZ = skill.Position.Z,
          DirX = skill.MoveDir.X,
          DirY = skill.MoveDir.Y,
          DirZ = skill.MoveDir.Z
        }
      };
        return movepkt;
    }
    public override void LeaveGame(Player player)
    {
      base.LeaveGame(player);
    }

    public override void Despawn(BaseObject obj)
    {
      if (obj == null) return;

      switch (obj.ObjectType)
      {
        case EGameObjectType.Hero:
          if (obj is Hero hero)
          {
            heroes.Remove(hero.ObjectID); // heroes 전용
            Broadcast(new S_Despawn { ObjectId = hero.ObjectID });
            hero.Room = null;             // 링크 해제
          }

          return; 

        case EGameObjectType.Bullet:
          if (obj is HeroBullet bullet)
            ReturnBullet(bullet);
          break;

        case EGameObjectType.Skill:
          if (obj is HeroSkill skill)
            ReturnSkill(skill);
          break;

        case EGameObjectType.Projectile:
          // 중간 타일 같은거 (장애물)만들까
          break;
      }

      Broadcast(new S_Despawn { ObjectId = obj.ObjectID });
      base.Despawn(obj); // baseObjects.Remove + obj.Room=null
    }

    public override void Remove(int objectId)
    {
      if (!players.TryGetValue(objectId, out var player) || player == null)
        return;

      var ownerHero = player.selectHero;           // 플레이어가 조종하던 영웅
      int? ownerHeroId = ownerHero?.ObjectID;      // 영웅 오브젝트 ID (null 가능)

      // 1) 플레이어 소유 오브젝트(총알/스킬 등) 먼저 정리
      //    - 컬렉션 변경 중 열거 예외 방지 위해 스냅샷 사용
      var list = baseObjects.Values.ToList();
      foreach (var obj in list)
      {
        if (ownerHeroId == null)
          break;

        // 총알 소유자 검사
        if (obj is HeroBullet bullet)
        {
          var bulletOwnerId = bullet.Owner?.ObjectID;
          if (bulletOwnerId == ownerHeroId)
          {
            Despawn(bullet); // Broadcast(S_Despawn) + base.Despawn 내부에서 처리
            continue;
          }
        }

        // 스킬 소유자 검사 (추가)
        if (obj is HeroSkill skill)
        {
          var skillOwnerId = skill.Owner?.ObjectID;
          if (skillOwnerId == ownerHeroId)
          {
            Despawn(skill);  // ReturnSkill 등은 Despawn/타입별 분기에서 처리
            continue;
          }
        }
      }

      // 2) 영웅 자체도 룸 오브젝트로 등록되어 있다면 Despawn
      if (ownerHeroId != null && baseObjects.TryGetValue(ownerHeroId.Value, out var heroObj))
        Despawn(heroObj);

      // 3) 영웅 맵에서 제거 (키가 플레이어ID인지 영웅ID인지에 맞게 조정)
      //    - 현재 코멘트대로 '플레이어 ObjectID'를 키로 쓰는 구조라면 아래 유지
      heroes.Remove(objectId);

      // 4) 마지막으로 플레이어 제거(링크 해제 등은 base.Remove에서 처리)
      base.Remove(objectId);
    }

    public override void Close()
    {
      base.Close();
    }


    public bool IsEmptyRoom()
    {
      return players.Count == 0 ? true : false;
    }

    public override void Broadcast(IMessage packet)
    {
      base.Broadcast(packet);

    }


    public void MoveHero(Player player, C_HeroMove c_move)
    {
      if (player == null)
        return;

      Hero hero = player.selectHero;
      if (hero == null)
        return;

      hero.EHeroUpperState = c_move.HeroInfo.UpperState;
      hero.EHeroLowerState = c_move.HeroInfo.LowerState;

      Vector3 cleanDir = new Vector3(c_move.HeroInfo.PosInfo.DirX, c_move.HeroInfo.PosInfo.DirY, c_move.HeroInfo.PosInfo.DirZ);
      if (cleanDir.LengthSquared() <0.001)
      {
        return;
      }

      hero.MoveDir = new Vector3(c_move.HeroInfo.PosInfo.DirX, c_move.HeroInfo.PosInfo.DirY, c_move.HeroInfo.PosInfo.DirZ);

    }

  }
}
