using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class WizardWindow : SystemWindow
	{
		public static Func<bool> ShouldShowAuthPanel { get; set; }
		public static Action ShowAuthDialog;
		private static WizardWindow wizardWindow = null;
		private static bool connectionWindowIsOpen = false;
		protected PrinterInfo activePrinter;

		private bool editMode = false;

		public WizardWindow(bool openToHome = false)
			: base(500 * GuiWidget.DeviceScale, 500 * GuiWidget.DeviceScale)
		{
			AlwaysOnTopOfMain = true;
			this.Title = "Setup Wizard".Localize();

			if (openToHome)
			{
				ChangeToHome();
			}
			else
			{
				// Todo - detect wifi connectivity
				bool WifiDetected = MatterControlApplication.Instance.IsNetworkConnected();
				if (!WifiDetected)
				{
					ChangeToWifiForm();
				}
				else
				{
					ChangeToSetupPrinterForm();
				}
			}

			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Padding = new BorderDouble(8);
			this.ShowAsSystemWindow();
			MinimumSize = new Vector2(350 * GuiWidget.DeviceScale, 400 * GuiWidget.DeviceScale);
		}

		public static void Show(bool openToHome = false)
		{
			if (connectionWindowIsOpen == false)
			{
				wizardWindow = new WizardWindow(openToHome);
				connectionWindowIsOpen = true;
				wizardWindow.Closed += (parentSender, e) =>
				{
					connectionWindowIsOpen = false;
					wizardWindow = null;
				};
			}
			else
			{
				if (wizardWindow != null)
				{
					wizardWindow.BringToFront();
				}
			}
		}

		public void ChangeToSetupPrinterForm()
		{
			bool showAuthPanel = ShouldShowAuthPanel?.Invoke() ?? false;
			if (showAuthPanel)
			{
				ChangeToAuthPanel();
			}
			else
			{
				ChangeToAddPrinter();
			}
		}

		public void ChangeToConnectForm(bool editMode = false)
		{
			this.editMode = editMode;
			ChangeToPanel<SetupWizardConnect>();
		}

		public void ChangeToTroubleshooting()
		{
			ChangeToPanel<SetupWizardTroubleshooting>();
		}

		public void ChangeToWifiForm(bool editMode = false)
		{
			this.editMode = editMode;
			ChangeToPanel<SetupWizardWifi>();
		}

		public void ChangeToHome()
		{
			ChangeToPanel<SetupWizardHome>();
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

		internal void ChangeToAddPrinter()
		{
			this.activePrinter = null;
			ChangeToPanel<SetupStepMakeModelName>();
		}

		internal void ChangeToPanel<T>() where T : WizardPanel, new()
		{
			var panel = new T();
			panel.WizardWindow = this;
			ChangeToStep(panel);
		}

		internal void ChangeToSetupBaudRate()
		{
			ChangeToPanel<SetupStepBaudRate>();
		}

		internal void ChangeToInstallDriver()
		{
			ChangeToPanel<SetupStepInstallDriver>();
		}

		internal void ChangeToSetupComPortOne()
		{
			ChangeToPanel<SetupStepComPortOne>();
		}

		internal void ChangeToSetupCompPortTwo()
		{
			ChangeToPanel<SetupStepComPortTwo>();
		}

		internal void ChangeToSetupComPortManual()
		{
			ChangeToPanel<SetupStepComPortManual>();
		}

		internal void ChangeToAuthPanel()
		{
			ChangeToPanel<ShowAuthPanel>();
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
			if (string.IsNullOrEmpty(PrinterConnectionAndCommunication.Instance?.ActivePrinter?.BaudRate()))
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