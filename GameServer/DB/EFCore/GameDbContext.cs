using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game
{
	public class GameDbContext : DbContext
	{
		public DbSet<PlayerDb> playerDbs { get; set; }
		public DbSet<HeroDb> Heroes { get; set; }
		public DbSet<ItemDb> Items { get; set; }

		static readonly ILoggerFactory _logger = LoggerFactory.Create(builder => { builder.AddConsole(); });

		public GameDbContext()
		{

		}

		protected override void OnConfiguring(DbContextOptionsBuilder options)
		{
			ConfigManager.LoadConfig();

			options
				.UseLoggerFactory(_logger)
				.UseSqlServer(ConfigManager.Config.connectionString);
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			 //AccountDbId에 인덱스 걸어준다
			builder.Entity<PlayerDb>()
				.HasIndex(t => t.AccountDbId)
				.IsUnique();

			//builder.Entity<PlayerDb>()
			//	.HasIndex(t => t.AccountDbId);
			//
      //builder.Entity<PlayerDb>()
			//	.Property(nameof(PlayerDb.CreateDate))
			//	.HasDefaultValueSql("CURRENT_TIMESTAMP");
			
			//builder.Entity<ItemDb>()
			//	.HasIndex(i => i.AccountDbId);
			//
			//builder.Entity<ItemDb>()
			//	.HasIndex(t => t.OwnerDbId);			
		}
	}
}
