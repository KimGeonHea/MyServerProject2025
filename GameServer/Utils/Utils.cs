using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    public static class Utils
    {
        public static long TickCount { get { return System.Environment.TickCount64; } }

		public static IPAddress GetLocalIP()
		{
			var ipHost = Dns.GetHostEntry(Dns.GetHostName());

			foreach (IPAddress ip in ipHost.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					return ip;
				}
			}

			return IPAddress.Loopback;
		}
    public static Dictionary<EItemSubType, EItemSlotType> SubTypeToEquipTypeMap = new Dictionary<EItemSubType, EItemSlotType>()
    {
      { EItemSubType.Mainweapon,  EItemSlotType.Mainweapon },
      { EItemSubType.Helmet,   EItemSlotType.Helmet} ,
      { EItemSubType.Armor,      EItemSlotType.Armor },
      { EItemSubType.Shoes,       EItemSlotType.Shoes },
      { EItemSubType.Ring,         EItemSlotType.Ring },
    };

    public static EItemSlotType GetEquipSlotType(EItemSubType subType)
    {
      if (SubTypeToEquipTypeMap.TryGetValue(subType, out EItemSlotType value))
        return value;

      return EItemSlotType.None;
    }
    public static StageData FindNextStage(StageData cur)
    {
      if (cur == null)
        return null;

      // 같은 타입 (싱글/엘리트/보스 등) 기준으로만 넘기고 싶으면 필터 추가
      int curOrder = cur.OrderIndex;

      StageData next = null;

      foreach (var kv in DataManager.StageDataDict) // stageDict : string -> StageData 라고 가정
      {
        StageData s = kv.Value;
        if (s.EStageType != cur.EStageType)  // 타입 다르면 스킵하고 싶으면
          continue;

        if (s.OrderIndex > curOrder)
        {
          if (next == null || s.OrderIndex < next.OrderIndex)
            next = s;
        }
      }

      return next; // 없으면 null (마지막 스테이지)
    }


  }
}
