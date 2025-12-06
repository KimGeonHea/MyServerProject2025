using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections.Generic;

public enum MsgId
{
	S_Connected = 1,
	C_Test = 2,
	C_Ping = 3,
	S_Ping = 4,
	S_LoginReq = 5,
	C_LoginRes = 6,
	S_AddHero = 7,
	C_EnterGame = 8,
	S_EnterGame = 9,
	C_EnterQeue = 10,
	C_LeaveGame = 11,
	S_LeaveGame = 12,
	S_AddItem = 13,
	S_ChangeItemSlot = 14,
	C_EquipItem = 15,
	C_UnEquipItem = 16,
	C_UseItem = 17,
	S_UseItemBox = 18,
	C_EnterSingleStage = 19,
	S_EnterSingleStage = 20,
	C_ReportStage = 21,
	S_ReportStage = 22,
	S_Wallet = 23,
	C_StagePause = 24,
	S_StagePause = 25,
	C_StageRevive = 26,
	S_StageRevive = 27,
	C_ItemClick = 28,
	S_ItemClick = 29,
	C_Chat = 30,
	S_Chat = 31,
	C_ChatList = 32,
	S_ChatList = 33,
	C_InvenCapaticy = 34,
	S_InvenCapaticy = 35,
	C_ItemList = 36,
	S_ItemList = 37,
	C_DeleteItem = 38,
	S_DeleteItem = 39,
	C_HeroList = 40,
	S_HeroList = 41,
	C_SelectHero = 42,
	S_SelctHero = 43,
	C_HeroMove = 44,
	S_HeroMove = 45,
	C_HeroShot = 46,
	S_HeroShot = 47,
	C_HeroSkill = 48,
	S_HeroSkill = 49,
	S_HeroShotMove = 50,
	S_HeroSkillMove = 51,
	S_HeroChangeHp = 52,
	S_Despawn = 53,
	C_EnterMultyGame = 54,
	S_EnterMultyGame = 55,
	C_MatchCancel = 56,
	C_DailyReward = 57,
	S_SpawnMonster = 58,
	S_MonsterState = 59,
	S_MonsterMove = 60,
	S_MonsterHp = 61,
	S_MonsterDie = 62,
	S_DailyReward = 63,
	C_DailyRewardOpen = 64,
	S_DailyRewardOpen = 65,
	C_RewardItem = 66,
	S_RewardItem = 67,
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
		_onRecv.Add((ushort)MsgId.C_Ping, MakePacket<C_Ping>);
		_handler.Add((ushort)MsgId.C_Ping, PacketHandler.C_PingHandler);		
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
		_onRecv.Add((ushort)MsgId.C_EnterSingleStage, MakePacket<C_EnterSingleStage>);
		_handler.Add((ushort)MsgId.C_EnterSingleStage, PacketHandler.C_EnterSingleStageHandler);		
		_onRecv.Add((ushort)MsgId.C_ReportStage, MakePacket<C_ReportStage>);
		_handler.Add((ushort)MsgId.C_ReportStage, PacketHandler.C_ReportStageHandler);		
		_onRecv.Add((ushort)MsgId.C_StagePause, MakePacket<C_StagePause>);
		_handler.Add((ushort)MsgId.C_StagePause, PacketHandler.C_StagePauseHandler);		
		_onRecv.Add((ushort)MsgId.C_StageRevive, MakePacket<C_StageRevive>);
		_handler.Add((ushort)MsgId.C_StageRevive, PacketHandler.C_StageReviveHandler);		
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