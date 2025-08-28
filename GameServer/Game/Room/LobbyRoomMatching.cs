using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  public partial class LobbyRoom : Room
  {

    const int LevelBucketSize = 5;
    int MakeBucketKey(int level) => Math.Max(0, level / LevelBucketSize);

    // 대기열에 저장할 최소 정보
    struct MatchEntry
    {
      public Player P;
      public int Level;
    }

    // 레벨 버킷별 대기 큐(순서 유지)
    Dictionary<int /*bucket*/, LinkedList<MatchEntry>> levelBucket = new();
    // 빠른 취소/중복 체크:Player objectId -> (bucketKey, node)
    Dictionary<int /*P obejctId*/, (int bucket, LinkedListNode<MatchEntry> node)> levelBucketMap = new();

    public void EnqueuePlayer(Player player)
    {
      if (player == null) 
        return;
      if (levelBucketMap.ContainsKey(player.ObjectID)) 
        return; // 중복 방지

      var level = player.playerStatInfo.Level;


      int b = MakeBucketKey(level);
      if (levelBucket.TryGetValue(b, out var list) == false)
        list = levelBucket[b] = new LinkedList<MatchEntry>();

      var node = list.AddLast(new MatchEntry
      {
        P = player,
        Level = level
      });
      levelBucketMap[player.ObjectID] = (b, node);

      // (선택) ACK: p.Session?.Send(new S_MatchEnqueueOk());
      CompleteMatchByLevel(); // 같은 스레드에서 즉시 시도
    }

    public void CompleteMatchByLevel()
    {
      foreach (var b in levelBucket.Keys.ToList())
      {
        if (!levelBucket.TryGetValue(b, out var list)) 
          continue;

        while (list.Count >= 2)
        {
          var n1 = list.First; list.RemoveFirst();
          var n2 = list.First; list.RemoveFirst();

          levelBucketMap.Remove(n1.Value.P.ObjectID);
          levelBucketMap.Remove(n2.Value.P.ObjectID);
          if (list.Count == 0) levelBucket.Remove(b);

          // 로비에서 제거(링크 해제)
          base.Remove(n1.Value.P.ObjectID);
          base.Remove(n2.Value.P.ObjectID);

          // 게임룸 생성
          RoomManager.Instance.Create1vs1Room(n1.Value.P, n2.Value.P);
        }
      }
    }
    // 대기 취소
    public void CancelMatch(Player player)
    {
      if (player == null) 
        return;

      if (levelBucketMap.TryGetValue(player.ObjectID, out var info) == false)
      {
        // (선택) 이미 매칭됨/대기 아님
        return;
      }
      (int b, LinkedListNode<MatchEntry> node) = info;
      
      if (levelBucket.TryGetValue(b, out var list))
      {
        list.Remove(node);
        if (list.Count == 0) levelBucket.Remove(b);
      }
      levelBucketMap.Remove(player.ObjectID);

      // (선택) 취소 ACK
      // p.Session?.Send(new S_MatchCancelOk());
    }


    public override void Remove(int objectId)
    {
      if (levelBucketMap.TryGetValue(objectId, out var info))
      {
        var (b, node) = info;
        levelBucket[b].Remove(node);
        if (levelBucket[b].Count == 0) levelBucket.Remove(b);
        levelBucketMap.Remove(objectId);
      }
      base.Remove(objectId); // players 딕셔너리에서 제거 + 링크 해제
    }
  }
}
