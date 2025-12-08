using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Server
{
	class SessionManager : Singleton<SessionManager>
	{
		int _sessionId = 0;

		Dictionary<int, ClientSession> _sessions = new Dictionary<int, ClientSession>();
		object _lock = new object();

		const int PingTimeoutSec = 600; // 예: 60초
    static readonly TimeSpan timeOut = TimeSpan.FromSeconds(PingTimeoutSec);

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

		public void CheckTimeout()
		{
			DateTime now = DateTime.UtcNow;
			List<ClientSession> toDisconnect = new List<ClientSession>();

			lock (_lock)
			{
				foreach (var kv in _sessions)
				{
					ClientSession s = kv.Value;
					if (now - s.LastPacketUtc > timeOut)
					{
						toDisconnect.Add(s);
					}
				}
			}

			foreach (ClientSession s in toDisconnect)
			{
				try
				{
					s.Disconnect();
				}
				catch (Exception e)
				{
					Console.WriteLine($"CheckTimeout Disconnect Error: {e}");
				}
			}
		}
  }
}
