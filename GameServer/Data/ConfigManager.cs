using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Server.Data
{
	[Serializable]
	public class ServerConfig
	{
		public string dataPath;
		public string ip;
		public int port;
		public string connectionString;
	}

	public class ConfigManager
	{
		public static ServerConfig Config { get; private set; }

		public static void LoadConfig(string path = "./config.json")
		{
			string text = File.ReadAllText(path);
			Config = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerConfig>(text);
		}
	}
}
