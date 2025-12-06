using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
	class SessionManager : Singleton<SessionManager>
	{
		int _sessionId = 0;

		Dictionary<int, ClientSession> _sessions = new Dictionary<int, ClientSession>();
		object _lock = new object();

		const int PingTimeoutSec = 600; // 예: 60초


		public List<ClientSession> GetSessions()
		{
			List<ClientSession> sessions = new List<ClientSession>();

			lock (_lock)
			{
				sessions = _sessions.Values.ToList();
			}

			return sessions;
		}

		public ClientSession Generate()
		{
			lock (_lock)
			{
				int sessionId = ++_sessionId;

				ClientSession session = new ClientSession();
				session.SessionId = sessionId;
				_sessions.Add(sessionId, session);

				Console.WriteLine($"Connected : {sessionId}");

				return session;
			}
		}

		public ClientSession Find(int id)
		{
			lock (_lock)
			{
				_sessions.TryGetValue(id, out ClientSession session);
				return session;
			}
		}

		public void Remove(ClientSession session)
		{
			lock (_lock)
			{
				_sessions.Remove(session.SessionId);
			}
		}

    public void StartPingChecker()
    {
      Thread t = new Thread(PingLoop);
      t.IsBackground = true;
      t.Name = "PingChecker";
      t.Start();
    }

    void PingLoop()
    {
      List<ClientSession> toDisconnect = new List<ClientSession>();
      while (true)
      {
        DateTime now = DateTime.UtcNow;
        toDisconnect.Clear();

        lock (_lock)
        {
          foreach (ClientSession s in _sessions.Values)
          {
            double idleSec = (now - s.LastPacketUtc).TotalSeconds;
            if (idleSec > PingTimeoutSec)
            {
              toDisconnect.Add(s);
            }
          }
        }

        // 잠금 밖에서 실제 소켓 끊기
        foreach (ClientSession s in toDisconnect)
        {
          try
          {
            s.Disconnect(); 
          }
          catch (Exception e)
          {
            Console.WriteLine($"PingLoop Disconnect Error: {e}");
          }
        }

        Thread.Sleep(1000); // 1초마다 체크
      }
    }
  }
}
