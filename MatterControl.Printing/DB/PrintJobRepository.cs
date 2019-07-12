using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace MatterControl.Common.Repository
{
	public class PrintJobRepository : IRepository<PrintJob>
	{
		private PrintServerContext printServerContext;

		public PrintJobRepository(PrintServerContext printServerContext)
		{
			this.printServerContext = printServerContext;
		}

		public IEnumerable<PrintJob> List => printServerContext.PrintJobs;

		public void Add(PrintJob entity)
		{
			printServerContext.PrintJobs.Add(entity);
			printServerContext.SaveChanges();
		}

		public void Delete(PrintJob entity)
		{
			printServerContext.PrintJobs.Remove(entity);
			printServerContext.SaveChanges();
		}

		public void Update(PrintJob entity)
		{
			printServerContext.Entry(entity).State = EntityState.Modified;
			printServerContext.SaveChanges();
		}

		public PrintJob FindById(int id)
		{
			return printServerContext.PrintJobs.FirstOrDefault(r => r.Id == id);
		}
	}
}
