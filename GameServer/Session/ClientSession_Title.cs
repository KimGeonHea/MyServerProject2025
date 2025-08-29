﻿using GameServer;
using GameServer.Game;
using GameServer.Game.Room;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Server.Data;
using Server.Game;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Server
{
  public partial class ClientSession : PacketSession
  {
    public void HandleEnter(C_EnterGame enterGamePacekt)
    {
     
    }
    

    public void HandleLogin(C_LoginRes loginPacket)
    {
      //보안 체크
      if (ServerState != PlayerServerState.ServerStateLogin)
        return;
      //악의적으로 여러번 보낸다면
      //쌩뚱맞은 타이밍에 그냥 이 패킷을 보낸다면?      
      lock(_lock)
      {
        if (player != null)
          return;
        PlayerDb playerdb = DBManager.LoadPlayerDb(loginPacket);
        if (playerdb != null)
        {
          player = Player.MakePlayer(this , playerdb);
          //player.Init(playerdb);
          AccountDbId = playerdb.AccountDbId;

          S_LoginReq loginReq = new S_LoginReq()
          {

            PlayerInfo = new PlayerStatInfo()
            {
              PlayerDbId = playerdb.PlayerDbId,
              Level = playerdb.Level,
              Exp = playerdb.Exp,
              TotalExp = playerdb.TotalExp,
              Gold = playerdb.Gold,
              Daimond = playerdb.Diamond,
              Energy = playerdb.Energy,
              StageName = playerdb.StageName,
              PlayerName =playerdb.PlayerName,
              InventoryCapacity = playerdb.InventoryCapacity,
              LastEnergyGivenTime = playerdb.LastEnergyGivenTime.ToString(),
              LastDailyRewardTime = playerdb.LastDailyRewardTime.ToString(),
              DailyweekReward = WeeaklyRewardFlagTostring(playerdb.WeeklyRewardFlags),
            },
          };

          foreach (HeroDb herodb in playerdb.Heros)
          {
            HeroStatInfo herostatinfo = Hero.MakieHeroStatInfo(herodb);
            loginReq.Heros.Add(herostatinfo);
          }
          foreach (ItemDb itemdb in playerdb.Items)
          {
            ItemInfo iteminfo = Item.MakeItemInfo(itemdb);
            loginReq.Items.Add(iteminfo);
          }

          Send(loginReq);
          LobbyRoom room = LobbyRoom.Instance;
          if (room == null)
            return;

          room.Push(room.EnterGame, player);


          //멀티쓰레드방식 TODO AWS 사용시 성능을 어느정도로 해야할지//
          //LobbyJobManager.Push(player.PlayerDbId, () =>
          //{
          //  room.EnterGmae(player);
          //});

        }
        else
        {
          PlayerDb createPlayerDb = DBManager.CreatePlayerDb(loginPacket);
          {
            //플레이어 생성 
            //플레이어 세션 연결
            //플레이어 이니셜 라이즈 (디비 연결)
            player = Player.MakePlayer(this, createPlayerDb);
            //player.Init(createPlayerDb);
            AccountDbId = player.PlayerDbId;

            S_LoginReq loginReq = new S_LoginReq()
            {
              PlayerInfo = new PlayerStatInfo()
              {
                PlayerDbId = createPlayerDb.PlayerDbId,
                Level = createPlayerDb.Level,
                Exp = createPlayerDb.Exp,
                TotalExp = createPlayerDb.TotalExp,
                Gold = createPlayerDb.Gold,
                Daimond = createPlayerDb.Diamond,
                Energy = createPlayerDb.Energy,
                StageName = createPlayerDb.StageName,
                PlayerName = createPlayerDb.PlayerName,
                InventoryCapacity = createPlayerDb.InventoryCapacity,
                LastEnergyGivenTime = createPlayerDb.LastEnergyGivenTime.ToString(),
                LastDailyRewardTime = createPlayerDb.LastDailyRewardTime.ToString(),
                DailyweekReward = WeeaklyRewardFlagTostring(createPlayerDb.WeeklyRewardFlags),
              }
            };

            foreach (HeroDb herodb in createPlayerDb.Heros)
            {
              HeroStatInfo herostatinfo = Hero.MakieHeroStatInfo(herodb);
              loginReq.Heros.Add(herostatinfo);
            }

            foreach (ItemDb itemdb in createPlayerDb.Items)
            {
              ItemInfo iteminfo = Item.MakeItemInfo(itemdb);
              loginReq.Items.Add(iteminfo);
            }

            Send(loginReq);


            LobbyRoom room = LobbyRoom.Instance;
            if (room == null)
              return;

            room.Push(room.EnterGame, player);

            //멀티쓰레드방식//
            //LobbyJobManager.Push(player.PlayerDbId, () =>
            //{
            //  room.EnterGmae(player);
            //});
          }
        }
      }
    }
    //새로운 히어로 생성//

    public string WeeaklyRewardFlagTostring(int flags)
    {
      char[] result = new char[7];
      for (int i = 0; i < 7; i++)
        result[i] = (flags & (1 << i)) != 0 ? '1' : '0';
      return new string(result);
    }
 
    public List<ItemDb> CreateTest()
    {
      var t = DBManager.CreateTestDb(player.PlayerDbId);
      for (int i = 0; i < t.Count; i++)
      {
        Item item = new Item();
        item.Init(t[i]);
        player.inventory.Add(item);
      }
      
      return DBManager.CreateTestDb(player.PlayerDbId);
    }
  }
}
