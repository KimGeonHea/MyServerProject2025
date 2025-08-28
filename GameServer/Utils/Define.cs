using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Utils
{
  public class Define
  {
    public const int HOODIE_TEMPLATE_ID = 101; // 후디 캐릭터 템플릿 ID
    public const int SOLDIER_TEMPLATE_ID = 301; // 후디 캐릭터 템플릿 ID
    public const int ROBOT_TEMPLATE_ID = 101; // 후디 캐릭터 템플릿 ID
    public const int BERAR_TEMPLATE_ID = 101; // 후디 캐릭터 템플릿 ID
    //public const int HOODIE_TEMPLATE_ID = 101; // 후디 캐릭터 템플릿 ID
    //public const int HOODIE_TEMPLATE_ID = 101; // 후디 캐릭터 템플릿 ID
    //public const int HOODIE_TEMPLATE_ID = 101; // 후디 캐릭터 템플릿 ID
    //public const int HOODIE_TEMPLATE_ID = 101; // 후디 캐릭터 템플릿 ID
    public const int MAX_ROOM_COUNT = 100; // 최대 방 개수
    public const int MAX_PLAYER_COUNT = 10; // 최대 플레이어 수
    public const int MAX_BULLET_COUNT = 100; // 최대 총알 수
    public const int MAX_SKILL_COUNT = 50; // 최대 스킬 수
    public static readonly TimeSpan GameTickInterval = TimeSpan.FromMilliseconds(16); // 게임 틱 간격 (60 FPS 기준)
  }
}
