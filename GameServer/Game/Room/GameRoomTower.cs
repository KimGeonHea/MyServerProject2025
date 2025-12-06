using GameServer.Game.Object.Creature;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  public partial class GameRoom : Room
  {
    public Tower RedTower { get; private set; }
    public Tower BlueTower { get; private set; }

    // 맵에 맞게 좌표는 네가 조정
    readonly Vector3 _redTowerPos = new Vector3(-10, 0, 0);
    readonly Vector3 _blueTowerPos = new Vector3(10, 0, 0);

    // 1) 타워 하나 스폰하는 헬퍼
    Tower SpawnTower(ETeamType teamType, Hero owner, Vector3 pos, int maxHp)
    {
      var tower = new Tower();

      // Room의 공통 로직을 그대로 사용
      tower.Position = pos;
      tower.Init(teamType, maxHp, owner);

      EnterGame(tower);           // objectCount 올리고 baseObjects에 넣고 Room 세팅

      // 클라에 스폰 패킷
      BroadcastSpawnTower(tower);

      return tower;
    }
    

    void InitPvpTowers(Hero redHero, Hero blueHero)
    {
      // RED 타워
      RedTower = new Tower();
      RedTower.Position = new Vector3(-4f, 0f, 26); 
      RedTower.Init(ETeamType.Red, 5000,redHero );
      EnterGame(RedTower);

      // BLUE 타워
      BlueTower = new Tower();
      BlueTower.Position = new Vector3(-4f, 0f, -1); 
      BlueTower.Init(ETeamType.Blue, 5000, blueHero);
      EnterGame(BlueTower);
    }

    // 3) 스폰 패킷 만들기 (네 proto 구조에 맞게만 필드 이름 바꿔)
    void BroadcastSpawnTower(Tower tower)
    {
      //S_SpawnObject pkt = new S_SpawnObject(); // 너가 쓰는 스폰 패킷 타입
      //ObjectInfo info = new ObjectInfo
      //{
      //  ObjectId = tower.ObjectID,
      //  ObjectType = tower.ObjectType,
      //  TeamType = tower.teamType,
      //  Hp = tower.Hp,
      //  MaxHp = tower.MaxHp,
      //  PosInfo = new PositionInfo
      //  {
      //    X = tower.Pos.X,
      //    Y = tower.Pos.Y,
      //    Z = tower.Pos.Z
      //  }
      //};
      //
      //pkt.Objects.Add(info);
      //Broadcast(pkt);
    }

    // 4) HP 브로드캐스트 (Tower.OnDamaged → Room.BroadcastTowerHp 호출)
    public void BroadcastTowerHp(Tower tower)
    {
      //S_TowerHp pkt = new S_TowerHp
      //{
      //  ObjectId = tower.ObjectID,
      //  Hp = tower.Hp,
      //  MaxHp = tower.MaxHp
      //};
      //Broadcast(pkt);
    }

    // 5) 타워 파괴 처리 (누가 이겼는지, 게임 끝 패킷 등)
    public void OnTowerDestroyed(Tower tower)
    {
      // 여기서 tower.teamType 기준으로 승/패 결정
      // 예: Red 타워가 터졌으면 Blue 승리
      ETeamType loser = tower.teamType;
      ETeamType winner = (loser == ETeamType.Red) ? ETeamType.Blue : ETeamType.Red;

      // S_BattleResult 같은 패킷 만들어서 뿌리고
      //S_BattleResult pkt = new S_BattleResult
      //{
      //  WinnerTeam = winner
      //};
      //Broadcast(pkt);

      // 이후 Close()나 별도 정리 로직 호출
      // Close();
    }
    
  }
}
