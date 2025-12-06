using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Game.Room;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Server.Data;
using Server.Game;
using ServerCore;

namespace Server
{
	// 1. Recv (N개)     서빙
	// 2. GameLogic (1)  요리사
	// 3. Send (1개)     서빙
	class Program
	{
		static Listener _listener = new Listener();
		static Connector _connector = new Connector();

    //static void GameLogicTask()
    //{
    //  const int TICK_MS = 20;
    //  var stopwatch = new System.Diagnostics.Stopwatch();
    //  stopwatch.Start();
    //
    //  long lastTick = stopwatch.ElapsedMilliseconds;
    //
    //  while (true)
    //  {
    //    long now = stopwatch.ElapsedMilliseconds;
    //    long elapsed = now - lastTick;
    //
    //    if (elapsed >= TICK_MS)
    //    {
    //      float deltaTime = elapsed / 1000.0f;
    //      RoomManager.Instance.Update(deltaTime);
    //      lastTick = now;
    //    }
    //    else
    //    {
    //      Thread.Sleep(1);
    //    }
    //  }
    //}

    //static void GameLogicTask()
		//{
		//	while (true)
		//	{
    //    RoomManager.Instance.Update();
    //    Thread.Sleep(0);
		//	}
		//}

		static void GameDbTask()
		{
			while (true)
			{
				DBManager.Instance.Flush();
				Thread.Sleep(100);
			}
		}

		static void Main(string[] args)
		{
			ConfigManager.LoadConfig();
			DataManager.LoadData();

			//기존 방식//
      //IPAddress ipAddr = IPAddress.Parse(ConfigManager.Config.ip);
			//내 포트 연결//
      IPAddress ipAddr = IPAddress.Any;
      IPEndPoint endPoint = new IPEndPoint(ipAddr, ConfigManager.Config.port);
			_listener.Init(endPoint, () => { return SessionManager.Instance.Generate(); });
			
			Console.WriteLine("Listening...");

      SessionManager.Instance.StartPingChecker();

      const int DbThreadCount = 1;
      DBManager.LaunchDBThreads(DbThreadCount);
      DBManager.InitDbIds();

      // 게임 워커 수: vCPU-2 정도 추천(여유 코어 확보)
      int vcpu = Environment.ProcessorCount;
      int gameWorkers = Math.Max(1, vcpu - 2);

      // 로비 1개(30Hz), 게임 워커 N개(50Hz), 스레드당 200룸
      RoomManager.Instance.StartSchedulers(gameWorkers, lobbyHz: 30, gameHz: 50);
      Thread.CurrentThread.Name = "Main";
      Thread.Sleep(Timeout.Infinite);

      // 메인쓰레드 방식//
      //Thread.CurrentThread.Name = "GameLogic";
      //GameLogicTask();
    }
	}
}
