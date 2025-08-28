using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Google.Protobuf.Protocol;

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

  }
}
