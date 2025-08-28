using GameServer.Game.Object;
using GameServer.Utils;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.Extensions.Logging;
using Server.Data;
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

    public Dictionary<int/*OjbectId*/, Hero> heros = new Dictionary<int, Hero>();

    
    
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

        // 등록(키를 hero.ObjectID로 통일해 두면 찾기 쉬움)
        heros[hero.ObjectID] = hero;
      }

      // 5) (선택) 클라에게 EnterGame ACK
      //    로비에서만 보내고 있다면 생략 가능. 필요하면 사용.
      //player.Session?.Send(new S_EnterGame { PlayerObjectId = player.ObjectID });

      // 6) 2명 모이면 시작
      if (players.Count == 2)
        TryStartGame();

      ////base.EnterGmae(player);
      //if (player == null)
      //  return;
      //
      //if (player.selectHero == null)
      //  return;
      //
      //int templateId = player.selectHero.TemplatedId; // Player가 선택한 영웅 템플릿 ID
      //player.Session.ServerState = PlayerServerState.ServerStateGame;
      //
      //player.Room = this;
      //players.Add(player.ObjectID, player);
      //
      //Hero hero = player.selectHero;
      //
      //hero.ObjectID = player.ObjectID;
      //hero.Room = this;
      //hero.SetTotalData(player.inventory.GetTotalDataEquipItems());
      //heros.Add(hero.ObjectID, hero);
      //
      //if (players.Count >= 2)
      //  TryStartGame();
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
      base.Despawn(obj);
      if (obj == null)
        return;

      // BaseObject 딕셔너리에서 제거

      if (obj is HeroBullet bullet)
      {
        baseObjects.Remove(obj.ObjectID);
        // Bullet은 풀에 반환
        ReturnBullet(bullet); // GameRoom의 풀로 반환
      }

      if (obj is HeroSkill skill)
      {
        baseObjects.Remove(obj.ObjectID);
        // Bullet은 풀에 반환
        ReturnBullet(skill); // GameRoom의 풀로 반환
      }

      switch (obj.ObjectType)
      {
        case EGameObjectType.Hero:
          if (obj is Hero hero)
            heros.Remove(hero.ObjectID);
          break;

        case EGameObjectType.Projecttile:
        

        
          break;

          // TODO: 다른 오브젝트 타입도 추가 가능
      }

      // 클라이언트에게 오브젝트 제거 통지
      S_Despawn despawnPacket = new S_Despawn();
      despawnPacket.ObjectId = obj.ObjectID;

      Broadcast(despawnPacket);
    }

    public override void Remove(int objectId)
    {
      // 0) 플레이어 조회 (한 번만)
      if (!players.TryGetValue(objectId, out var player) || player == null)
        return;

      // 소유 영웅 가져오기 (프로퍼티 이름에 주의)
      var ownerHero = player.selectHero; // ← 실제 이름이 'selectHero'/'SelectedHero'면 그걸로

      // 영웅 ID(또는 소유자 ID)로 비교하는 편이 안전
      int? ownerHeroId = ownerHero?.ObjectID;

      // 1) 플레이어 소유 오브젝트(예: 총알) 먼저 제거
      //    - 컬렉션 수정 중 열거 예외 방지를 위해 스냅샷 사용
      foreach (var obj in baseObjects.Values.ToList())
      {
        if (obj is HeroBullet bullet)
        {
          // ID 기준 비교가 참조 비교보다 안전
          var bulletOwnerId = bullet.Owner?.ObjectID;
          if (ownerHeroId != null && bulletOwnerId == ownerHeroId)
          {
            // 공통 정리 경로 사용 (링크 해제/브로드캐스트 등 내부에서 처리)
            Despawn(obj);
          }
        }
      }

      // 2) 영웅 자체도 룸 오브젝트로 등록되어 있다면 디스폰
      if (ownerHeroId != null && baseObjects.TryGetValue(ownerHeroId.Value, out var heroObj))
        Despawn(heroObj);

      // 3) 영웅 맵에서 제거 (키가 플레이어ID인지 영웅ID인지에 따라 조정)
      //    - heros의 키가 '플레이어 ObjectID'라면 아래처럼
      heros.Remove(objectId);
      //    - 만약 '영웅 ObjectID'로 키를 쓴다면:
      // if (ownerHeroId != null) heros.Remove(ownerHeroId.Value);

      // 4) 마지막으로 플레이어 제거 (링크 해제 등은 base.Remove에서 처리)
      base.Remove(objectId);
    }

    public override void Close()
    {
      base.Close();
    }

    public void AllClear()
    {
      foreach (var player in players.Values.ToArray())
      {
        player.Room = null; 
        Remove(player.PlayerDbId);
      }
      foreach (var obj in baseObjects.Values.ToArray())
      {
        obj.Room = null;  
        Remove(obj.ObjectID);
      }
      foreach (var hero in heros.Values.ToArray())
      {
        hero.Room = null;
        Remove(hero.ObjectID);
      }

      players.Clear();
      baseObjects.Clear();
      heros.Clear();
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
