using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections.Generic;

public enum MsgId
{
	S_Connected = 1,
	C_Test = 2,
	S_LoginReq = 3,
	C_LoginRes = 4,
	S_AddHero = 5,
	C_EnterGame = 6,
	S_EnterGame = 7,
	C_EnterQeue = 8,
	C_LeaveGame = 9,
	S_LeaveGame = 10,
	S_AddItem = 11,
	S_ChangeItemSlot = 12,
	C_EquipItem = 13,
	C_UnEquipItem = 14,
	C_UseItem = 15,
	S_UseItemBox = 16,
	C_ItemClick = 17,
	S_ItemClick = 18,
	C_Chat = 19,
	S_Chat = 20,
	C_ChatList = 21,
	S_ChatList = 22,
	C_InvenCapaticy = 23,
	S_InvenCapaticy = 24,
	C_ItemList = 25,
	S_ItemList = 26,
	C_DeleteItem = 27,
	S_DeleteItem = 28,
	C_HeroList = 29,
	S_HeroList = 30,
	C_SelectHero = 31,
	S_SelctHero = 32,
	C_HeroMove = 33,
	S_HeroMove = 34,
	C_HeroShot = 35,
	S_HeroShot = 36,
	C_HeroSkill = 37,
	S_HeroSkill = 38,
	S_HeroShotMove = 39,
	S_HeroSkillMove = 40,
	S_HeroChangeHp = 41,
	S_Despawn = 42,
	C_EnterMultyGame = 43,
	S_EnterMultyGame = 44,
	C_MatchCancel = 45,
	C_DailyReward = 46,
	S_DailyReward = 47,
	C_DailyRewardOpen = 48,
	S_DailyRewardOpen = 49,
	C_RewardItem = 50,
	S_RewardItem = 51,
}

class PacketManager
{
	#region Singleton
	static PacketManager _instance = new PacketManager();
	public static PacketManager Instance { get { return _instance; } }
	#endregion

	PacketManager()
	{
		Register();
	}

	Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>> _onRecv = new Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>>();
	Dictionary<ushort, Action<PacketSession, IMessage>> _handler = new Dictionary<ushort, Action<PacketSession, IMessage>>();
		
	public Action<PacketSession, IMessage, ushort> CustomHandler { get; set; }

	public void Register()
	{		
		_onRecv.Add((ushort)MsgId.C_Test, MakePacket<C_Test>);
		_handler.Add((ushort)MsgId.C_Test, PacketHandler.C_TestHandler);		
		_onRecv.Add((ushort)MsgId.C_LoginRes, MakePacket<C_LoginRes>);
		_handler.Add((ushort)MsgId.C_LoginRes, PacketHandler.C_LoginResHandler);		
		_onRecv.Add((ushort)MsgId.C_EnterGame, MakePacket<C_EnterGame>);
		_handler.Add((ushort)MsgId.C_EnterGame, PacketHandler.C_EnterGameHandler);		
		_onRecv.Add((ushort)MsgId.C_EnterQeue, MakePacket<C_EnterQeue>);
		_handler.Add((ushort)MsgId.C_EnterQeue, PacketHandler.C_EnterQeueHandler);		
		_onRecv.Add((ushort)MsgId.C_LeaveGame, MakePacket<C_LeaveGame>);
		_handler.Add((ushort)MsgId.C_LeaveGame, PacketHandler.C_LeaveGameHandler);		
		_onRecv.Add((ushort)MsgId.C_EquipItem, MakePacket<C_EquipItem>);
		_handler.Add((ushort)MsgId.C_EquipItem, PacketHandler.C_EquipItemHandler);		
		_onRecv.Add((ushort)MsgId.C_UnEquipItem, MakePacket<C_UnEquipItem>);
		_handler.Add((ushort)MsgId.C_UnEquipItem, PacketHandler.C_UnEquipItemHandler);		
		_onRecv.Add((ushort)MsgId.C_UseItem, MakePacket<C_UseItem>);
		_handler.Add((ushort)MsgId.C_UseItem, PacketHandler.C_UseItemHandler);		
		_onRecv.Add((ushort)MsgId.C_ItemClick, MakePacket<C_ItemClick>);
		_handler.Add((ushort)MsgId.C_ItemClick, PacketHandler.C_ItemClickHandler);		
		_onRecv.Add((ushort)MsgId.C_Chat, MakePacket<C_Chat>);
		_handler.Add((ushort)MsgId.C_Chat, PacketHandler.C_ChatHandler);		
		_onRecv.Add((ushort)MsgId.C_ChatList, MakePacket<C_ChatList>);
		_handler.Add((ushort)MsgId.C_ChatList, PacketHandler.C_ChatListHandler);		
		_onRecv.Add((ushort)MsgId.C_InvenCapaticy, MakePacket<C_InvenCapaticy>);
		_handler.Add((ushort)MsgId.C_InvenCapaticy, PacketHandler.C_InvenCapaticyHandler);		
		_onRecv.Add((ushort)MsgId.C_ItemList, MakePacket<C_ItemList>);
		_handler.Add((ushort)MsgId.C_ItemList, PacketHandler.C_ItemListHandler);		
		_onRecv.Add((ushort)MsgId.C_DeleteItem, MakePacket<C_DeleteItem>);
		_handler.Add((ushort)MsgId.C_DeleteItem, PacketHandler.C_DeleteItemHandler);		
		_onRecv.Add((ushort)MsgId.C_HeroList, MakePacket<C_HeroList>);
		_handler.Add((ushort)MsgId.C_HeroList, PacketHandler.C_HeroListHandler);		
		_onRecv.Add((ushort)MsgId.C_SelectHero, MakePacket<C_SelectHero>);
		_handler.Add((ushort)MsgId.C_SelectHero, PacketHandler.C_SelectHeroHandler);		
		_onRecv.Add((ushort)MsgId.C_HeroMove, MakePacket<C_HeroMove>);
		_handler.Add((ushort)MsgId.C_HeroMove, PacketHandler.C_HeroMoveHandler);		
		_onRecv.Add((ushort)MsgId.C_HeroShot, MakePacket<C_HeroShot>);
		_handler.Add((ushort)MsgId.C_HeroShot, PacketHandler.C_HeroShotHandler);		
		_onRecv.Add((ushort)MsgId.C_HeroSkill, MakePacket<C_HeroSkill>);
		_handler.Add((ushort)MsgId.C_HeroSkill, PacketHandler.C_HeroSkillHandler);		
		_onRecv.Add((ushort)MsgId.C_EnterMultyGame, MakePacket<C_EnterMultyGame>);
		_handler.Add((ushort)MsgId.C_EnterMultyGame, PacketHandler.C_EnterMultyGameHandler);		
		_onRecv.Add((ushort)MsgId.C_MatchCancel, MakePacket<C_MatchCancel>);
		_handler.Add((ushort)MsgId.C_MatchCancel, PacketHandler.C_MatchCancelHandler);		
		_onRecv.Add((ushort)MsgId.C_DailyReward, MakePacket<C_DailyReward>);
		_handler.Add((ushort)MsgId.C_DailyReward, PacketHandler.C_DailyRewardHandler);		
		_onRecv.Add((ushort)MsgId.C_DailyRewardOpen, MakePacket<C_DailyRewardOpen>);
		_handler.Add((ushort)MsgId.C_DailyRewardOpen, PacketHandler.C_DailyRewardOpenHandler);		
		_onRecv.Add((ushort)MsgId.C_RewardItem, MakePacket<C_RewardItem>);
		_handler.Add((ushort)MsgId.C_RewardItem, PacketHandler.C_RewardItemHandler);
	}

	public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer)
	{
		ushort count = 0;

		ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
		count += 2;
		ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
		count += 2;

		Action<PacketSession, ArraySegment<byte>, ushort> action = null;
		if (_onRecv.TryGetValue(id, out action))
			action.Invoke(session, buffer, id);
	}

	void MakePacket<T>(PacketSession session, ArraySegment<byte> buffer, ushort id) where T : IMessage, new()
	{
		T pkt = new T();
		pkt.MergeFrom(buffer.Array, buffer.Offset + 4, buffer.Count - 4);

		if (CustomHandler != null)
		{
			CustomHandler.Invoke(session, pkt, id);
		}
		else
		{
			Action<PacketSession, IMessage> action = null;
			if (_handler.TryGetValue(id, out action))
				action.Invoke(session, pkt);
		}
	}

	public Action<PacketSession, IMessage> GetPacketHandler(ushort id)
	{
		Action<PacketSession, IMessage> action = null;
		if (_handler.TryGetValue(id, out action))
			return action;
		return null;
	}
}