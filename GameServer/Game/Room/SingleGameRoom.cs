using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Server;
using Server.Data;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GameServer.Game.Room
{

  class SingleStageState
  {
    public StageData Stage;          // 이 플레이어가 지금 플레이 중인 스테이지 데이터
    public DateTime StartUtc;        // 스테이지 시작한 시각(UTC 기준)
    public bool BossKilled;          // 보스를 이미 잡았는지 여부
    public bool RewardGiven;         // 클리어 보상을 이미 지급했는지 여부
    public bool IsPaused;            // 지금 일시정지 상태인지
    public DateTime? PauseUtc;       // 일시정지를 시작한 시각(언제부터 멈춰 있었는지)
    public double PausedAccumSec;    // 지금까지 일시정지로 멈춰 있었던 시간 총합(초)
    public bool IsFinished;          // 성공/실패 포함 "이미 끝난 스테이지"인지
    public bool IsClear;             // 마지막 결과가 클리어인지

  }

  public class SingleGameRoom :Room
  {

    Dictionary<int/*playerId*/, SingleStageState> singleStages;
    Dictionary<string, StageData> stageTable;
    List<int/*playerId*/> removeList;

    public SingleGameRoom()
    {
      stageTable = new Dictionary<string, StageData>();
      stageTable = DataManager.StageDataDict;

      singleStages = new Dictionary<int, SingleStageState>();
      removeList = new List<int>();
    }

    public override void EnterGame(Player player)
    {
      if (player == null || !IsAlive)
        return;

      base.EnterGame(player);
      player.Session.ServerState = PlayerServerState.ServerStateGame; // 하나 새로 파도 됨
    }

    double GetElapsedSec(SingleStageState st, DateTime now)
    {
      double baseElapsed = (now - st.StartUtc).TotalSeconds;
      double paused = st.PausedAccumSec;

      if (st.IsPaused && st.PauseUtc.HasValue)
        paused += (now - st.PauseUtc.Value).TotalSeconds;

      double result = baseElapsed - paused;
      return result < 0 ? 0 : result;
    }

    public override void Update(float deltaTime)
    {
      base.Update(deltaTime);

      if (singleStages.Count == 0)
        return;

      DateTime now = DateTime.UtcNow;
      removeList.Clear();

      foreach (var kv in singleStages) 
      {
        int playerId = kv.Key;
        SingleStageState st = kv.Value;
        StageData stage = st.Stage;

        // 플레이어가 이미 방에서 나갔으면 상태만 정리 후보로
        if (!players.TryGetValue(playerId, out var player) || player == null)
        {
          removeList.Add(playerId);
          continue;
        }

        // 일시정지 중이면 시간/클리어/타임아웃 체크 스킵
        if (st.IsPaused)
          continue;

        double elapsed = GetElapsedSec(st, now);

        // 1) 서버 기준 클리어 조건 만족  자동 클리어 처리
        //if (IsStageCleared(st, stage, elapsed))
        //{
        //  FinishStage(player, st, isClear: true, elapsedSec: elapsed);
        //  removeList.Add(playerId);   // 완료된 스테이트 제거
        //  continue;
        //}
        //double timeout = stage.MinClearTime;
        //
        //if (elapsed >= timeout)
        //{
        //  FinishStage(player, st, isClear: false, elapsedSec: elapsed);
        //  removeList.Add(playerId);
        //  continue;
        //}
      }

      // 여기서 한 번에 제거 (딕셔너리 수정은 foreach 바깥에서만)
      for (int i = 0; i < removeList.Count; i++)
      {
        singleStages.Remove(removeList[i]);
      }
    }

    // ===== 입장 =====
    public void EnterSingleStage(Player player, string stageId)
    {
      if (player == null)
        return;

      // 1) 방에 안 들어와 있으면 먼저 EnterGame
      if (player.Room != this)
      {
        EnterGame(player);
      }

      // 2) 스테이지 데이터 찾기
      if (!stageTable.TryGetValue(stageId, out var stage))
        return;

      // 3) 에너지 체크
      if (player.Energy < stage.ConsumeEnergy)
        return;

      // 4) 에너지 차감 + 상태 기록
      player.Energy -= stage.ConsumeEnergy;

      singleStages[player.ObjectID] = new SingleStageState
      {
        Stage = stage,
        StartUtc = DateTime.UtcNow,
        BossKilled = false,
        RewardGiven = false,
        IsPaused = false,
        PauseUtc = null,
        PausedAccumSec = 0,
        IsFinished = false,
        IsClear = false,

      };

      // 5) 입장 성공 패킷
      S_EnterSingleStage pkt = new S_EnterSingleStage
      {
        StageId = stageId,
        EStageResultType = EStageResultType.Ok,
        Energy = player.Energy,
      };

      player.Session?.Send(pkt);
    }

    // ===== 보스 처치 리포트 (서버 내부에서만 호출하는 용도로 쓰는 걸 추천) =====
    public void ReportBossKilled(Player player, string stageId)
    {
      if (player == null)
        return;

      if (!singleStages.TryGetValue(player.ObjectID, out var st))
        return;
      if (st.Stage.StageId != stageId)
        return;

      st.BossKilled = true;
    }

    // ===== 클라에서 스테이지 종료 보고 (선택) =====
    public void ReportStageEnd(Player player, string stageId, bool clientIsClear)
    {
      if (player == null)
        return;

      if (!singleStages.TryGetValue(player.ObjectID, out var st))
        return;
      if (st.Stage.StageId != stageId)
        return;

      if (st.RewardGiven)
        return;

      StageData stage = st.Stage;
      double elapsed = GetElapsedSec(st, DateTime.UtcNow);

      // 서버 기준 클리어 판정 + 클라 isClear 둘 다 만족해야 최종 클리어로 인정
      bool serverClear = IsStageCleared(st, stage, elapsed);
      bool finalClear = serverClear && clientIsClear;

      FinishStage(player, st, finalClear, elapsed);
    }

    public void RevivePlayer(Player player)
    {
      if (player == null)
        return;
      // 부활 비용 처리 등은 여기서
      if(player.Diamond < 10)
      {
        // 다이아 부족
        //S_StageRevive s_StageRevive = new S_StageRevive()
        //{
        //  Dia = player.Diamond
        //};
        //player.Session?.Send(s_StageRevive);
        return;
      }

      player.Diamond -= 10;
      DBManager.SaveDiamon(player);
      
      S_StageRevive s_StageRevive = new S_StageRevive()
      {
        Dia = player.Diamond
      };
      ToggleStagePause(player);
      player.Session?.Send(s_StageRevive);
    }

    // ===== 일시정지 토글 (C_StagePause 대응) =====
    public void ToggleStagePause(Player player)
    {
      if (player == null)
        return;

      if (!singleStages.TryGetValue(player.ObjectID, out var st))
        return;
      DateTime now = DateTime.UtcNow;
      if (!st.IsPaused)
      {
        // Pause 시작
        st.IsPaused = true;
        st.PauseUtc = now;
      }
      else
      {
        //  Pause 해제  멈춰있던 시간 누적
        if (st.PauseUtc.HasValue)
        {
          st.PausedAccumSec += (now - st.PauseUtc.Value).TotalSeconds;
        }

        st.IsPaused = false;
        st.PauseUtc = null;
      }

      // 클라에 Ack (필드를 나중에 proto에 추가해도 됨)
      //S_StagePause res = new S_StagePause
      //{
      //  // bool IsPaused 같은 필드가 proto에 생기면 여기서 채우면 됨
      //};


      //player.Session?.Send(res);
    }

    // ===== 클라가 나가기(C_LeaveGame) 눌렀을 때 =====
    public override void LeaveGame(Player player)
    {
      if (player == null)
        return;

      SingleStageState st = null;

      if (singleStages.TryGetValue(player.ObjectID, out st))
      {
        double elapsed = GetElapsedSec(st, DateTime.UtcNow);

        // 아직 스테이지 결과가 안 난 상태에서
        // C_LeaveGame으로 나간 거면 => 자발적 퇴장 + 실패 처리
        if (!st.IsFinished)
        {
          FinishStage(player, st, isClear: false, elapsedSec: elapsed);
        }

        singleStages.Remove(player.ObjectID);
      }

      // 이 함수는 "클라가 C_LeaveGame 보낸 경우"에만 타니까
      // 기본 의미는 자발적 퇴장(Voluntary).
      // 단, 스테이지가 이미 끝난 상태에서 결과창 닫고 나간 거면
      // StageComplete로 표시해도 됨.
      ELeaveReason reason;

      if (st != null && st.IsFinished)
        reason = ELeaveReason.StageComplete;  // 결과창 보고 나감
      else
        reason = ELeaveReason.Voluntary;      // 도중에 그냥 나감

      NotifyLeave(player, reason, goLobby: true);

      PlayerRomve(player.ObjectID);

      var lobbyRoom = RoomManager.Instance.LobbyRoom;
      lobbyRoom?.Push(lobbyRoom.EnterGame, player);
    }

      // ===== 공통 완료 처리 (성공/실패 모두 여기로) =====
    void FinishStage(Player player, SingleStageState st, bool isClear, double elapsedSec)
    {
      if (player == null || st == null)
        return;

      // 이미 끝난 스테이지면 두 번째 호출은 무시
      if (st.IsFinished)
        return;

      st.IsFinished = true;
      st.IsClear = isClear;

      StageData stage = st.Stage;

      S_ReportStage res = new S_ReportStage
      {
        StageId = stage.StageId,
        IsClear = isClear,
      };

      // 실패면 결과만 보내고 끝
      if (!isClear)
      {
        player.Session?.Send(res);
        return;
      }

      // 이미 보상 준 상태면(이론상 오면 안 되지만 방어)
      if (st.RewardGiven)
      {
        player.Session?.Send(res);
        return;
      }

      // === 여기부터 처음 클리어일 때만 ===
      int gainedGold = 0;
      int gainedDia = 0;
      int gainedExp = 0;// stage.ClearExp;

      if (DataManager.StageRewardDict.TryGetValue(stage.StageRewardDataId, out StageRewardDataGroup group) && group.Rewards != null)
      {
        foreach (var r in group.Rewards)
        {
          switch (r.ERewardType)
          {
            case ERewardType.ErwardTypeGold:
              gainedGold += r.Count;
              break;

            case ERewardType.ErwardTypeDiamod:
              gainedDia += r.Count;
              break;
            case ERewardType.ErwardTypeExp:
              gainedExp += r.Count;
              break;
            case ERewardType.ErwardTypeObject:
              {
                if (r.ItemId <= 0 || r.Count <= 0)
                  break;

                if (DBManager.MakeAddItemDb(player, r.ItemId, r.Count,
                      out ItemDb newItemDb, out ItemDb stackItemDb, out int addStackCount))
                {
                  DBManager.ApplyAddItemDbToMemory(player, newItemDb, stackItemDb, addStackCount);
                  DBManager.SaveItemDbChanges(player, newItemDb, stackItemDb);
                }
                break;
              }
          }
        }
      }

      player.Gold += gainedGold;
      player.Diamond += gainedDia;
      player.Exp += gainedExp;

      StageData next = Server.Utils.FindNextStage(stage);
      if (next != null && next.OrderIndex > player.CurStageOrderIndex)
      {
        player.Stagename = next.StageId;
      }

      DBManager.Push(player.PlayerDbId, () =>
      {
        using var db = new GameDbContext();
        var pdb = new PlayerDb
        {
          PlayerDbId = player.PlayerDbId,
          Gold = player.Gold,
          Diamond = player.Diamond,
          Exp = player.Exp,
          StageName = player.Stagename,
        };

        db.playerDbs.Attach(pdb);
        db.Entry(pdb).Property(p => p.Gold).IsModified = true;
        db.Entry(pdb).Property(p => p.Diamond).IsModified = true;
        db.Entry(pdb).Property(p => p.Exp).IsModified = true;
        db.Entry(pdb).Property(p => p.StageName).IsModified = true;

        db.SaveChangesEx();
      });

      st.RewardGiven = true;

      res.Gold = player.Gold;
      res.Dia = player.Diamond;
      res.Exp = player.Exp;
      res.StageName = player.Stagename;

      player.Session?.Send(res);
    }
    // ===== 서버 기준 클리어 판정 =====
    bool IsStageCleared(SingleStageState st, StageData stage, double elapsed)
    {
      // 최소 시간 못 버텼으면 무조건 실패
      if (elapsed < 60 )//stage.MinClearTime)
        return false;

      // 보스 있으면 보스는 반드시 잡아야 함
      if (stage.HasBoss && !st.BossKilled)
        return false;

      return true;
    }

    // ===== 세션 끊겼을 때 (OnDisconnected → HandleDisconnect) =====
    public void HandleDisconnect(Player player)
    {
      if (player == null)
        return;

      if (singleStages.TryGetValue(player.ObjectID, out var st))
      {
        // 여기서는 굳이 FinishStage 안 하고,
        // 그냥 진행 중이던 스테이지 날려버리는 식으로만 해도 됨.
        // (진짜로 실패 처리하고 싶으면 FinishStage(false) 한 번 호출해도 되고)
        singleStages.Remove(player.ObjectID);
      }

      // 방에서 제거
      PlayerRomve(player.ObjectID);
      player.Room = null;
    }
  }
}

