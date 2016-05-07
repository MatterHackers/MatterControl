using System;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using MatterHackers.Agg.PlatformAbstract;
using System.IO;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class ConnectionWizard : SystemWindow
	{
		protected PrinterInfo activePrinter;
		private bool editMode = false;

		public ConnectionWizard()
			: base(350 * GuiWidget.DeviceScale, 500 * GuiWidget.DeviceScale)
		{
			AlwaysOnTopOfMain = true;
			string connectToPrinterTitle = LocalizedString.Get("MatterControl");
			string connectToPrinterTitleEnd = LocalizedString.Get("Connect to Printer");
			Title = string.Format("{0} - {1}", connectToPrinterTitle, connectToPrinterTitleEnd);
			Name = "Printer Connection Window";

			ChangeToAddPrinter();

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			this.ShowAsSystemWindow();
			MinimumSize = new Vector2(350 * GuiWidget.DeviceScale, 400 * GuiWidget.DeviceScale);
		}

		private static ConnectionWizard connectionWindow = null;

		public static void Show()
		{
			if (connectionWindow != null)
			{
				connectionWindow.BringToFront();
			}
			else
			{ 
				connectionWindow = new ConnectionWizard();
				connectionWindow.Closed += (s, e) => connectionWindow = null;
			}
		}

		internal void ChangeToAddPrinter()
		{
			this.activePrinter = null;
			ChangeToStep(new SetupStepMakeModelName(this));
		}

		private void ChangeToStep(GuiWidget nextStep)
		{
			UiThread.RunOnIdle(() =>
			{
				this.RemoveAllChildren();
				this.AddChild(nextStep);
				this.Invalidate();
			});
		}

		private int GetPrinterRecordCount()
		{
			return Datastore.Instance.RecordCount("Printer");
		}

		internal void ChangeToSetupBaudRate()
		{
			ChangeToStep(new SetupStepBaudRate(this));
		}

		internal void ChangeToInstallDriver()
		{
			ChangeToStep(new SetupStepInstallDriver(this));
		}

		internal void ChangeToSetupComPortOne()
		{
			ChangeToStep(new SetupStepComPortOne(this));
		}

		internal void ChangeToSetupCompPortTwo()
		{
			ChangeToStep(new SetupStepComPortTwo(this));
		}

		internal void ChangeToSetupComPortManual()
		{
			ChangeToStep(new SetupStepComPortManual(this));
		}

		internal void ChangeToInstallDriverOrComPortOne()
		{
			if (ActiveSliceSettings.Instance.PrinterDrivers().Count > 0)
			{
				ChangeToInstallDriver();
			}
			else
			{
				ChangeToSetupComPortOne();
			}
		}

		internal void ChangeToSetupBaudOrComPortOne()
		{
			if (string.IsNullOrEmpty(activePrinter.BaudRate))
			{
				ChangeToSetupBaudRate();
			}
			else
			{
				ChangeToSetupComPortOne();
			}
		}
	}
}