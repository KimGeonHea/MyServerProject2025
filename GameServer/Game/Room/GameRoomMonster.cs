using GameServer.Game.Object.Creature;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  public partial class GameRoom : Room
  {
    public Dictionary<int/*OjbectId*/, Monster> monsters = new Dictionary<int, Monster>();
    readonly Vector3 Team0SpawnPos = new Vector3(-4f, 0f, -1);

    // 맵 아래쪽에서 위 타워로 올라가는 팀 (Team 1)
    readonly Vector3 Team1SpawnPos = new Vector3(-4f, 0f, 26);

    // ====== 타워 레퍼런스 (네가 가진 실제 타입으로 교체) ======
    public BaseObject Team0Tower;   // Team 0 의 타워 (플레이어1 타워)
    public BaseObject Team1Tower;   // Team 1 의 타워 (플레이어2 타워)

    // ====== 웨이브 파라미터 ======
    const int MONSTER_PER_WAVE_PER_TEAM = 10;     // 한 웨이브당 팀별 10마리 총 20마리
    const float WAVE_INTERVAL = 30f;              // 웨이브 간 간격 (초)
    const float SPAWN_INTERVAL_IN_WAVE = 0.7f;    // 같은 웨이브 내에서 한 마리씩 찍어내는 간격

    // ====== 웨이브 상태 ======
    float _waveTimer = 0f;
    bool _isWaveSpawning = false;
    float _spawnTimer = 0f;
    int _waveIndex = 0;
    int _spawnedTeam0 = 0;
    int _spawnedTeam1 = 0;

    // 몬스터 종류 (데이터 ID)
    const int DEFAULT_MONSTER_ID = 1001; // 네 몬스터 테이블에 있는 ID로 바꿔

    void FixedUpdateWave(float deltaTime)
    {
      // 아직 타워가 안 세팅됐으면 아무 것도 안 함
      if (Team0Tower == null || Team1Tower == null)
        return;

      // 현재 웨이브에서 몬스터 찍어내는 중
      if (_isWaveSpawning)
      {
        _spawnTimer += deltaTime;

        // 일정 간격마다 한 마리씩
        if (_spawnTimer >= SPAWN_INTERVAL_IN_WAVE)
        {
          _spawnTimer -= SPAWN_INTERVAL_IN_WAVE;

          // 팀0
          if (_spawnedTeam0 < MONSTER_PER_WAVE_PER_TEAM)
          {
            SpawnMonsterForTeam(0);
            _spawnedTeam0++;
          }

          // 팀1
          if (_spawnedTeam1 < MONSTER_PER_WAVE_PER_TEAM)
          {
            SpawnMonsterForTeam(1);
            _spawnedTeam1++;
          }

          // 둘 다 다 찍었으면 웨이브 끝
          if (_spawnedTeam0 >= MONSTER_PER_WAVE_PER_TEAM &&
              _spawnedTeam1 >= MONSTER_PER_WAVE_PER_TEAM)
          {
            _isWaveSpawning = false;
            _waveTimer = 0f;        // 다음 웨이브까지 카운트 시작
          }
        }

        return;
      }

      // 웨이브 사이 대기 구간
      _waveTimer += deltaTime;
      if (_waveTimer >= WAVE_INTERVAL)
      {
        StartNextWave();
      }
    }
    void SpawnMonsterForTeam(int team)
    {
      // 1) 몬스터 데이터 가져오기
      // TODO: 네가 실제로 쓰는 키로 바꿔줘. (예: "Slime01" or "1001")
      const int MonsterKey = 101;

      if (!DataManager.MonsterDataDict.TryGetValue(MonsterKey, out MonsterData mData))
        return; // 데이터 없으면 스폰 안 함

      // 2) 타겟 타워 / 스폰 위치 결정
      BaseObject enemyTower = (team == 0) ? Team1Tower : Team0Tower;
      Vector3 spawnPos = (team == 0) ? Team0SpawnPos : Team1SpawnPos;

      if (enemyTower == null)
        return;

      // 3) 몬스터 생성
      Monster monster = new Monster
      {
        ObjectType = EGameObjectType.Monster
        // Room, ObjectID 는 여기서 건드리지 않는다!!
      };

      // 몬스터 전용 초기화
      monster.Init(mData, team, enemyTower, spawnPos);

      // 4) 룸에 등록 (여기서 ObjectID, Room 세팅 + baseObjects에 등록)
      EnterGame(monster);      // 또는 네가 쓰는 이름이 Add라면 Add(monster);

      // GameRoom에 Monster 전용 인덱스가 필요하면,
      // GameRoom.EnterGame(BaseObject)를 오버라이드해서 거기서 monsters에 넣게 하는 게 더 깔끔함.
    }
    void StartNextWave()
    {
      _waveIndex++;
      _isWaveSpawning = true;
      _spawnTimer = 0f;
      _spawnedTeam0 = 0;
      _spawnedTeam1 = 0;

      // 필요하면 클라에 "웨이브 시작" 브로드캐스트
      // S_WaveStart s = new S_WaveStart { WaveIndex = _waveIndex };
      // Broadcast(s);
    }
  }
}
