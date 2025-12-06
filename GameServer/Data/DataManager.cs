using GameServer.Game;
using GameServer.Game.Object;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Server.Data
{
	public interface ILoader<Key, Value>
	{
		Dictionary<Key, Value> MakeDict();
	}

	public class DataManager
	{
    //public static Dictionary<int, Data.ItemData> ItemDict { get; private set; } = new Dictionary<int, ItemData>();
    public static Dictionary<int, HeroData> HeroDataDict { get; private set; } = new Dictionary<int, HeroData>();
    public static Dictionary<int, ItemData> ItemDataDict { get; private set; } = new Dictionary<int, ItemData>();

    public static Dictionary<int, HeroSkillData> HeroSkilldataDict { get; private set; } = new Dictionary<int, HeroSkillData>();
    public static Dictionary<string, StageData> StageDataDict { get; private set; } = new Dictionary<string, StageData>();

    public static Dictionary<int, MonsterData> MonsterDataDict { get; private set; } = new Dictionary<int, MonsterData>();

    public static Dictionary<string, StageRewardDataGroup> StageRewardDict = new Dictionary<string, StageRewardDataGroup>();
    public static List<Obstacle> Obstacles { get; private set; } = new List<Obstacle>();
    public static Grid ObstacleGrid { get; private set; } = new(2.0f); // 셀 크기 설정
    public static void LoadData()
		{
      HeroDataDict = LoadJson<HeroLoader, int, HeroData>("HeroData").MakeDict();
      ItemDataDict = LoadJson<ItemLoader, int, ItemData>("ItemData").MakeDict();
      HeroSkilldataDict = LoadJson<HeroSkillLoader, int, HeroSkillData>("HeroSkillData").MakeDict();
      MonsterDataDict = LoadJson<MonsterServerDataLoader,int, MonsterData>("MonsterData").MakeDict();
      StageDataDict = LoadJson<StageLoader, string, StageData>("StageData").MakeDict();
      StageRewardDict = LoadJson<RewardDataLoader, string, StageRewardDataGroup>("StageRewardData").MakeDict();

      ItemBox.BuildPools(DataManager.ItemDataDict.Values);
      //LoadMap();
      LoadMapGrid();


    }


    static Loader LoadJson<Loader, Key, Value>(string path) where Loader : ILoader<Key, Value>
		{
			string text = File.ReadAllText($"{ConfigManager.Config.dataPath}/JsonData/{path}.json");
      return Newtonsoft.Json.JsonConvert.DeserializeObject<Loader>(text);
		}




		static Loader LoadJson<Loader>(string path)
		{
			string text = File.ReadAllText($"{ConfigManager.Config.dataPath}/{path}.json");
			return Newtonsoft.Json.JsonConvert.DeserializeObject<Loader>(text);
		}

    static void LoadMap()
		{
      string path = @"D:\M2\Server\ServerData\map1.txt";
      string[] lines = File.ReadAllLines(path);
      //Obstacles = new List<Obstacle>();

      foreach (string line in lines)
      {
        // 예: Bush,3.00,5.00,0.00,1.00,2.00,1.00
        string[] tokens = line.Split(',');

        if (tokens.Length != 7)
          continue;

        Obstacle obs = new Obstacle();
        obs.Type = tokens[0];
        obs.Center = new Vector3(
            float.Parse(tokens[1]),
            float.Parse(tokens[2]),
            float.Parse(tokens[3])
        );
        obs.Size = new Vector3(
            float.Parse(tokens[4]),
            float.Parse(tokens[5]),
            float.Parse(tokens[6])
        );

        Obstacles.Add(obs);
      }
    }

    static void LoadMapGrid()
    {
      string path = @"D:\M2\Server\ServerData\map1.txt";
      string[] lines = File.ReadAllLines(path);

      Obstacles = new List<Obstacle>();
      ObstacleGrid = new Grid(cellSize: 2.0f); // 셀 크기는 맵 구조에 따라 조정

      foreach (string line in lines)
      {
        // 예: Bush,3.00,5.00,0.00,1.00,2.00,1.00
        string[] tokens = line.Split(',');

        if (tokens.Length != 7)
          continue;

        Obstacle obs = new Obstacle();
        obs.Type = tokens[0];
        obs.Center = new Vector3(
            float.Parse(tokens[1]),
            float.Parse(tokens[2]),
            float.Parse(tokens[3])
        );
        obs.Size = new Vector3(
            float.Parse(tokens[4]),
            float.Parse(tokens[5]),
            float.Parse(tokens[6])
        );

        Obstacles.Add(obs);
        ObstacleGrid.AddObstacle(obs); // 추가
      }
    }

  }
}
