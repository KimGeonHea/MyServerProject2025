using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class Chat
  {
    public int playerID { get; set; } // 보낸 사람 ID
    public string playerName { get; set; }//이름
    public string messageText { get; set; } // 서버에서 저장하는 원본 텍스트
    public DateTime Timestamp { get; set; } // 보낸 시간

  }
}
