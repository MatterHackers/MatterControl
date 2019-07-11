using System;

namespace MatterControl.Common.Repository
{
	public class PrintJob : IEntity
	{
		public int Id { get; set; }

		public string GCodeFile { get; set; }

		public double RecoveryCount { get; set; }

		public double PercentDone { get; set; }

		public bool PrintComplete { get; set; }

		public DateTime PrintEnd { get; set; }

		public int PrinterId { get; set; }

		//public int PrintItemId { get; set; }

		public string PrintName { get; set; }

		public DateTime PrintStart { get; set; }

		public int PrintTimeMinutes
		{
			get
			{
				TimeSpan printTimeSpan = PrintEnd.Subtract(PrintStart);

				return (int)(printTimeSpan.TotalMinutes + .5);
			}
		}

		public int PrintTimeSeconds { get; set; }
	}
}
