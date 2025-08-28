using Server.Game;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server
{
	public static class Extensions
	{
		public static bool SaveChangesEx(this GameDbContext db)
		{
			try
			{
				db.SaveChanges();
				return true;
			}
			catch
			{
        Console.WriteLine("유저 ID가 null 혹은 빈 문자열입니다.");
        return false;
			}
		}
	}
}
