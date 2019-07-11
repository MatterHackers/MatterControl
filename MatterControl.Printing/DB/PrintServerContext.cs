using MatterControl.Common.Repository;
using Microsoft.EntityFrameworkCore;

public class PrintServerContext : DbContext
{
	private static readonly string ConnectionString = "Data Source=PrintServer.db";

	public DbSet<PrintJob> PrintJobs { get; set; }

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		base.OnConfiguring(optionsBuilder);

		optionsBuilder.UseSqlite(ConnectionString);
	}
}