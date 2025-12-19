using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Utils
{
  public class Define
  {
    public const int HERO_TEMPLATE_HOODIE = 101;
    public const int HERO_TEMPLATE_KNIGHT = 201;
    public const int HERO_TEMPLATE_SOLDIER = 301;
    public const int HERO_TEMPLATE_SAMURAI = 401;
    public const int HERO_TEMPLATE_SNIPER = 501;
    public const int HERO_TEMPLATE_SPACEMAN = 601;
    public const int HERO_TEMPLATE_COWBOY = 701;
    public const int HERO_TEMPLATE_VETERAN = 801;
    public const int HERO_TEMPLATE_CHEMICALMAN = 901;
    public const int HERO_TEMPLATE_NINJA = 1001;
    public const int HERO_TEMPLATE_ROBOT = 1101;
    public const int HERO_TEMPLATE_BEAR = 1201;



    public const int MAX_ROOM_COUNT = 100; // 최대 방 개수
    public const int MAX_PLAYER_COUNT = 10; // 최대 플레이어 수
    public const int MAX_BULLET_COUNT = 100; // 최대 총알 수
    public const int MAX_SKILL_COUNT = 50; // 최대 스킬 수
    public static readonly TimeSpan GameTickInterval = TimeSpan.FromMilliseconds(16); // 게임 틱 간격 (60 FPS 기준)

    public const int INVENTORY_CAPACITY_CONSUMGOLD = 100; // 인벤토리 확장시 소모 골드
    public const int INVENTORY_UP_CAPACITY = 6; // 증감 
    public const int INVENTORY_MAXCAPACITY = 200; //최대
    public const int ENERGY_MAX = 60; //최대 에너지
  }
}
