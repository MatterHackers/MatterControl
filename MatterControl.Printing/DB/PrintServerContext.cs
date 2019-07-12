using System;
using System.IO;
using MatterControl.Common.Repository;
using Microsoft.EntityFrameworkCore;

public class PrintServerContext : DbContext
{
	private static string applicationUserDataPath = EnsurePath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatterControl"));

	private static string dbPath = Path.Combine(applicationUserDataPath, "PrintServer.db");

	private static readonly string ConnectionString = "Data Source=" + dbPath;

	public DbSet<PrintJob> PrintJobs { get; set; }

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		base.OnConfiguring(optionsBuilder);

		optionsBuilder.UseSqlite(ConnectionString);
	}

	private static string EnsurePath(string fullPath)
	{
		Directory.CreateDirectory(fullPath);

		return fullPath;
	}
}