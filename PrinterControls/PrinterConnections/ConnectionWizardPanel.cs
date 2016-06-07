using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class ConnectionWizardPanel : WizardPanel
	{
		private PrinterInfo activePrinter;

		public ConnectionWizardPanel(WizardWindow wizard) 
			: base(wizard, "Cancel")
		{
			cancelButton.Click += (s, e) => PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
		}

		public PrinterInfo ActivePrinter
		{
			get
			{
				if (activePrinter == null)
				{
					var settings = ActiveSliceSettings.Instance;
					activePrinter = new PrinterInfo
					{
						AutoConnect = settings.DoAutoConnect(),
						BaudRate = settings.BaudRate(),
						ComPort = settings.ComPort(),
						DriverType = settings.DriverType(),
						Id = settings.ID,
						Name = settings.Name()
					};
				}

				return activePrinter;
			}
		}

		protected void SaveAndExit()
		{
			ActiveSliceSettings.Instance.RunInTransaction(settings =>
			{
				settings.SetAutoConnect(ActivePrinter.AutoConnect);
				settings.SetBaudRate(ActivePrinter.BaudRate);
				settings.SetComPort(ActivePrinter.ComPort);
				settings.SetDriverType(ActivePrinter.DriverType);
				settings.SetName(ActivePrinter.Name);
			});

			UiThread.RunOnIdle(wizardWindow.Close);
		}
	}
}