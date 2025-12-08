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
using ObjectManager = GameServer.Game.Object.ObjectManager;

namespace GameServer.Game.Room
{
  public partial class GameRoom : Room
  {

    readonly Vector3[] _spawnPositions = { new Vector3(-4f, 0f, 25), new Vector3(-4f, 0f, 0f) };// 플레이어1 시작 위치}

    double _sendAccum = 0;
    double _sendInterval = 1.0 / 30.0f; // 30Hz로 전송

    public Dictionary<int/*OjbectId*/, Hero> heroes = new Dictionary<int, Hero>();
    public Dictionary<int/*OjbectId*/, Creature> creatures = new Dictionary<int, Creature>();

    readonly List<BaseObject> baseObjectUpdateBuffer = new(256);

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
      player.Session.ServerState = PlayerServerState.ServerStateMultigame;

      // 4) 영웅 접속 처리
      var hero = player.selectHero;                // (오타 주의: selectHero 일관)
      if (hero != null)
      {
        // 영웅을 '플레이어와 동일한 ObjectID'로 쓸 거라면 그대로 매핑
        //hero.ObjectID = player.ObjectID;
        hero.Room = this;

        // 장비/스탯 합산
        hero.SetTotalData(player.inventory.GetTotalDataEquipItems());


        base.EnterGame(hero);
        // 등록(키를 hero.ObjectID로 통일해 두면 찾기 쉬움)
        heroes[hero.ObjectID] = hero;
        creatures[hero.ObjectID] = hero;
      }


      // 6) 2명 모이면 시작
      if (players.Count == 2)
        TryStartGame();
    }
    public override void EnterGame(BaseObject baseObject)
    {
      base.EnterGame(baseObject);

      if (baseObject is Creature c)
        creatures[c.ObjectID] = c;

      // 몬스터는 기존 딕셔너리에도 계속 넣고
      if (baseObject is Monster m)
        monsters[m.ObjectID] = m;

    }



    public void TryStartGame()
    {
      var playerList = players.Values.ToList();
      if (playerList.Count < 2)
        return;

      Player p1 = playerList[0];
      Player p2 = playerList[1];

      Hero h1 = p1.selectHero;
      Hero h2 = p2.selectHero;

      h1.TeamType = ETeamType.Red;
      h2.TeamType = ETeamType.Blue;

      // 1) 서버에서 스타팅 포지션 확정
      PlaceHeroAtSpawn(h1, 0);
      PlaceHeroAtSpawn(h2, 1);

      InitPvpTowers(h1,h2);
      // 2) HeroInfo 미리 만들어두기 (동일 객체 재사용)
      HeroInfo h1Info = MakeHeroInfo(p1);
      HeroInfo h2Info = MakeHeroInfo(p2);

      // 3) p1 입장: F = 나, S = 상대
      p1.Session?.Send(new S_EnterMultyGame
      {
        FHeroInfo = h1Info,
        SHroInfo = h2Info,
        MyTeam = h1.TeamType
      });

      // 4) p2 입장: F = 나, S = 상대 (순서 반대)
      p2.Session?.Send(new S_EnterMultyGame
      {
        FHeroInfo = h2Info,
        SHroInfo = h1Info,
        MyTeam = h2.TeamType
      });


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

      _sendAccum += deltatime;
      if (_sendAccum >= _sendInterval)
      {
        _sendAccum = 0;

        BroadcastHeroMoves();
        BroadcastBulletMoves();
        BroadcastSkillMoves();
      }
    }

    public void FixedUpdate(float deltaTime)
    {
      FixedUpdateHero(deltaTime);
      FixedUpdateObject(deltaTime);
      FixedUpdateMonster(deltaTime);
      FixedUpdateWave(deltaTime);
    }

    void FixedUpdateMonster(float deltaTime)
    {
      foreach (var monster in monsters.Values)
      {
        monster.FixedUpdate(deltaTime);
      }
    }

    void FixedUpdateHero(float deltaTime)
    {
      foreach (var player in players.Values)
      {
        Hero hero = player.selectHero;
        if (hero == null)
          continue;

        hero.FixedUpdate(deltaTime);
       // S_HeroMove s_move = CreateHeroMovePkt(hero);
       // Broadcast(s_move);
      }
    }
    
    void FixedUpdateObject(float deltaTime)
    {

      baseObjectUpdateBuffer.Clear();
      baseObjectUpdateBuffer.AddRange(baseObjects.Values);

      foreach (var obj in baseObjectUpdateBuffer)
      {
        // --- 총알 처리 ---
        if (obj is HeroBullet bullet)
        {
          // 1) 이미 죽은 총알이면 스킵
          if (!bullet.IsAlive)
            continue;

          // 2) 업데이트 (여기서 Despawn으로 IsAlive가 false 될 수도 있음)
          bullet.FixedUpdate(deltaTime);

          // 3) 업데이트 중에 죽었으면 Move 보내지 말고 스킵
          //if (!bullet.IsAlive)
          //  continue;

          // 4) 살아 있는 총알만 Move 브로드캐스트
          //S_HeroShotMove movePkt = CreateHeroShotMovePkt(bullet);
          //Broadcast(movePkt);
          //continue;
        }

        // --- 스킬 처리 ---
        if (obj is HeroSkill skill)
        {
          // 1) 이미 죽은 스킬이면 스킵
          if (!skill.IsAlive)
            continue;

          // 2) 업데이트 (여기서 Despawn 호출 가능)
          skill.FixedUpdate(deltaTime);

          // 3) 업데이트 중에 죽었으면 Move 보내지 말기
          //if (!skill.IsAlive)
          //  continue;

          // 4) 살아 있는 스킬만 Move 브로드캐스트
          //S_HeroSkillMove movePkt = CreateHeroSkillMovePkt(skill);
          //Broadcast(movePkt);
          //continue;
        }

        // TODO: 다른 타입 있으면 여기서 추가 처리
      }
    }
    private S_HeroShotMove CreateHeroShotMovePkt(HeroBullet bullet)
    {
      return new S_HeroShotMove
      {
        OwnerId = bullet.OwnerId,
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

    private S_HeroMove CreateHeroMovePkt(Hero hero)
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

    private S_HeroSkillMove CreateHeroSkillMovePkt(HeroSkill skill)
    {
      S_HeroSkillMove movepkt = new S_HeroSkillMove
      {
        OwnerId = skill.OwnerId,
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
      if (player == null)
        return;

      // 1) 클라에 나가기 알림
      // PVP 쪽은 LeaveReason을 어떻게 쓸지에 따라 다르지만
      // - 경기 도중 유저가 나가기 버튼: Voluntary
      // - 경기 끝나고 결과 처리 후 방에서 빼줄 때: MatchComplete
      // 지금 C_LeaveGame 기준이면 '나가기 버튼'이니까 Voluntary로 가자.
      NotifyLeave(player, ELeaveReason.Voluntary, goLobby: true);

      // 2) 플레이어 제거 (총알/스킬/영웅 정리 포함)
      PlayerRomve(player.ObjectID);  // GameRoom.Remove 오버라이드가 잘 정리해줌

      // 3) 로비로 보내기
      var lobby = RoomManager.Instance.LobbyRoom;
      if (lobby?.Worker != null)
      {
        lobby.Push(lobby.EnterGame, player);
      }

      // 4) 방 비었으면 제거
      if (players.Count == 0)
      {
        RoomManager.Instance.Remove(GameRoomId);
      }
    }

    public override void Despawn(BaseObject obj)
    {
      if (obj == null) return;

      switch (obj.ObjectType)
      {
        case EGameObjectType.Hero:
          if (obj is Hero hero)
          {
            heroes.Remove(hero.ObjectID);
            creatures.Remove(hero.ObjectID);
            Broadcast(new S_Despawn { ObjectId = hero.ObjectID });
            hero.Room = null;             // 링크 해제
          }

          return; 

        case EGameObjectType.Bullet:
          if (obj is HeroBullet bullet)
          {
            ReturnBullet(bullet);
            obj.IsAlive = false;
          }
          break;

        case EGameObjectType.Skill:
          if (obj is HeroSkill skill)
          {
            ReturnSkill(skill);
            obj.IsAlive = false;
          }
          break;

        case EGameObjectType.Projectile:
          // 중간 타일 같은거 (장애물)만들까
          break;

        case EGameObjectType.Monster:
          if(obj is Monster monster)
          {
            monsters.Remove(monster.ObjectID);
            creatures.Remove(monster.ObjectID);
          }
          break;

        case EGameObjectType.Tower:
          if(obj is Tower tower)
          {
            //towers.Remove(tower.ObjectID);
          }
          break;
      }

      Broadcast(new S_Despawn { ObjectId = obj.ObjectID });
      base.Despawn(obj); // baseObject에서 제거
    }

    public override void PlayerRomve(int objectId)
    {
      if (!players.TryGetValue(objectId, out var player) || player == null)
        return;

      var ownerHero = player.selectHero;           // 플레이어가 조종하던 영웅
      int? ownerHeroId = ownerHero?.ObjectID;      // 영웅 오브젝트 ID (null 가능)

      // 1) 플레이어 소유 오브젝트(총알/스킬 등) 먼저 정리
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
      base.PlayerRomve(objectId);
    }

    public override void Close()
    {
      base.Close();
    }


    public bool IsEmptyRoom()
    {
      return players.Count == 0 ? true : false;
    }

    void BroadcastBulletMoves()
    {
      // baseObjects에서 HeroBullet만 골라서 전송
      foreach (var obj in baseObjects.Values)
      {
        if (obj is not HeroBullet bullet)
          continue;
        if (!bullet.IsAlive)
          continue;

        S_HeroShotMove movePkt = CreateHeroShotMovePkt(bullet);
        Broadcast(movePkt);
      }
    }
    void BroadcastHeroMoves()
    {
      foreach (var hero in heroes.Values)
      {
        if (hero == null)
          continue;

        S_HeroMove pkt = CreateHeroMovePkt(hero);
        Broadcast(pkt);
      }
    }
    void BroadcastSkillMoves()
    {
      foreach (var obj in baseObjects.Values)
      {
        if (obj is not HeroSkill skill)
          continue;
        if (!skill.IsAlive)
          continue;

        S_HeroSkillMove movePkt = CreateHeroSkillMovePkt(skill);
        Broadcast(movePkt);
      }
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

    private void PlaceHeroAtSpawn(Hero hero, int index)
    {
      if (hero == null)
        return;

      if (index < 0 || index >= _spawnPositions.Length)
        index = 0;

      Vector3 pos = _spawnPositions[index];

      hero.Position = pos;  // BaseObject.Position 통해 PosInfo도 같이 세팅

      // 혹시나 Dir도 초기화하고 싶으면
      hero.PosInfo.DirX = 0;
      hero.PosInfo.DirY = 0;
      hero.PosInfo.DirZ = (index == 0) ? 1f : -1f; // 서로 마주보게
    }

    public override void ResetForPool()
    {
      base.ResetForPool();
      heroes.Clear();
      monsters.Clear();
    }

  }
}
