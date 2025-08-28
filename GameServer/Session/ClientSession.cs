using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ServerCore;
using System.Net;
using Google.Protobuf;
using Server.Game;
using Server.Data;
using GameServer;
using GameServer.Game.Room;
using GameServer.Game;
using System.Runtime.Serialization;
using Google.Protobuf.Protocol;

namespace Server
{
	public partial class ClientSession : PacketSession
	{
		public long AccountDbId { get; set; }
		public int SessionId { get; set; }

    public Player player { get; set; }

		public PlayerServerState ServerState = PlayerServerState.ServerStateLogin;

    object _lock = new object();

		#region Network
		// 예약만 하고 보내지는 않는다
		public void Send(IMessage packet)
		{
			Send(new ArraySegment<byte>(MakeSendBuffer(packet)));
		}

		public static byte[] MakeSendBuffer(IMessage packet)
		{
			MsgId msgId = (MsgId)Enum.Parse(typeof(MsgId), packet.Descriptor.Name);
			ushort size = (ushort)packet.CalculateSize();
			byte[] sendBuffer = new byte[size + 4];
			Array.Copy(BitConverter.GetBytes((ushort)(size + 4)), 0, sendBuffer, 0, sizeof(ushort));
			Array.Copy(BitConverter.GetBytes((ushort)msgId), 0, sendBuffer, 2, sizeof(ushort));
			Array.Copy(packet.ToByteArray(), 0, sendBuffer, 4, size);
			return sendBuffer;
		}

		public override void OnConnected(EndPoint endPoint)
		{
			Console.WriteLine($"OnConnected : {endPoint}");
		}

		public override void OnRecvPacket(ArraySegment<byte> buffer)
		{
			PacketManager.Instance.OnRecvPacket(this, buffer);
		}

		public override void OnDisconnected(EndPoint endPoint)
		{
      var p = player;
      var r = p?.Room;


      if (r is LobbyRoom lobby)
      {
        lobby.Push(() =>
        {
          // (a) 매칭 대기열/상태에서 제거
          lobby.CancelMatch(p);
          // (b) 로비 players 목록에서 제거
          lobby.Remove(p.ObjectID);
          p.Room = null;
        });
      }
      else if (r is GameRoom gr)
      {
        gr.Push(() =>
        {
          // (a) 자기 자신에게 S_LeaveGame 보낼 필요 없음(세션 없음)
          //     => 곧바로 Room에서 제거
          gr.Remove(p.ObjectID);
          p.Room = null;

          // (b) 룸이 비었으면 룸 삭제 예약
          if (gr.players.Count == 0)
            RoomManager.Instance.Remove(gr.GameRoomId);
        });
      }

      SessionManager.Instance.Remove(this);

			Console.WriteLine($"OnDisconnected : {endPoint}");
		}
    public override void OnSend(int numOfBytes)
		{
			//Console.WriteLine($"Transferred bytes: {numOfBytes}");
		}
		#endregion
	}
}
