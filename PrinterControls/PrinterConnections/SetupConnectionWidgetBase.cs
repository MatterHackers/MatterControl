using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupConnectionWidgetBase : WizardPanel
	{
		private PrinterInfo activePrinter;

		public SetupConnectionWidgetBase(WizardWindow wizard) 
			: base(
				  wizard, 
				  "Cancel", 
				  new TextImageButtonFactory()
				  {
					  normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
					  hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
					  disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
					  pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
					  borderWidth = 0
				  })
		{
			linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
			linkButtonFactory.fontSize = 10;

			this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off

			cancelButton.Click += (s, e) => PrinterConnectionAndCommunication.Instance.HaltConnectionThread();

			mainContainer.Padding = new BorderDouble(3, 5, 3, 5);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			headerLabel.PointSize = 14;
			headerLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			contentRow.Padding = new BorderDouble(5);
			footerRow.Margin = new BorderDouble(0, 3);
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