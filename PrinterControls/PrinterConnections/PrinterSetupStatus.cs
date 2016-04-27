using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl
{
	//Wraps the printer record. Includes temporary information that we don't need in the DB.
	public class PrinterSetupStatus
	{
		public PrinterInfo ActivePrinter;
		
		public Type PreviousSetupWidget;
		public Type NextSetupWidget;

		private List<CustomCommands> printerCustomCommands;

		public PrinterSetupStatus(PrinterInfo printer = null)
		{
			if (printer == null)
			{
				this.ActivePrinter = new PrinterInfo();
				this.ActivePrinter.Make = null;
				this.ActivePrinter.Model = null;
				this.ActivePrinter.Name = "Default Printer ({0})".FormatWith(ExistingPrinterCount() + 1);
				this.ActivePrinter.BaudRate = null;
				this.ActivePrinter.ComPort = null;
			}
			else
			{
				this.ActivePrinter = printer;
			}
		}

		public int ExistingPrinterCount()
		{
			return Datastore.Instance.RecordCount("Printer");
		}

		public void Save()
		{
			//Ordering matters - need to get Id for printer prior to loading slice presets
			this.ActivePrinter.AutoConnectFlag = true;

			// TODO: Review printerID int requirement
			int printerID;
			int.TryParse(ActivePrinter.Id, out printerID);

			foreach (CustomCommands customCommand in printerCustomCommands)
			{
				customCommand.PrinterId = printerID;
				customCommand.Commit();
			}
		}
	}
}