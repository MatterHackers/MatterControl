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
		private bool editMode = false;
		protected PrinterInfo activePrinter;

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
				//Todo - detect wifi connectivity
				bool WifiDetected = MatterControlApplication.Instance.IsNetworkConnected();

				if (!WifiDetected)
				{
					ChangeToWifiForm();
				}
				else if (GetPrinterRecordCount() > 0)
				{
					ChangeToSetupPrinterForm();
				}
				else
				{
					ChangeToSetupPrinterForm();
				}
			}

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Padding = new BorderDouble(8);
			this.ShowAsSystemWindow();
			MinimumSize = new Vector2(350 * GuiWidget.DeviceScale, 400 * GuiWidget.DeviceScale);
		}

		private static WizardWindow setupWizardWindow = null;
		private static bool connectionWindowIsOpen = false;

		public static Func<bool> ShouldShowAuthPanel { get; set; }

		public static Action ShowAuthDialog;

		public static void Show(bool openToHome = false)
		{
			if (connectionWindowIsOpen == false)
			{
				setupWizardWindow = new WizardWindow(openToHome);
				connectionWindowIsOpen = true;
				setupWizardWindow.Closed += (parentSender, e) =>
				{
					connectionWindowIsOpen = false;
					setupWizardWindow = null;
				};
			}
			else
			{
				if (setupWizardWindow != null)
				{
					setupWizardWindow.BringToFront();
				}
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			base.OnMouseUp(mouseEvent);
		}

		private void DoNotChangeWindow()
		{
			//Empty function used as default callback for changeToWindowCallback
		}

		public void ChangeToSetupPrinterForm()
		{
			bool showAuthPanel = ShouldShowAuthPanel?.Invoke() ?? false;
			if (showAuthPanel)
			{
				UiThread.RunOnIdle(ChangeToAuthPanel);
			}
			else
			{
				UiThread.RunOnIdle(ChangeToAddPrinter);
			}
		}

		public void ChangeToConnectForm(bool editMode = false)
		{
			this.editMode = editMode;
			UiThread.RunOnIdle(DoChangeToConnectForm);
		}

		public void DoChangeToConnectForm()
		{
			GuiWidget chooseConnectionWidget = new SetupWizardConnect(this);
			this.RemoveAllChildren();
			this.AddChild(chooseConnectionWidget);
			this.Invalidate();
		}

		public void ChangeToTroubleshooting()
		{
			UiThread.RunOnIdle(() =>
			{
				GuiWidget wizardForm = new SetupWizardTroubleshooting(this);
				this.RemoveAllChildren();
				this.AddChild(wizardForm);
				this.Invalidate();
			});
		}

		public void ChangeToWifiForm(bool editMode = false)
		{
			this.editMode = editMode;
			UiThread.RunOnIdle(DoChangeToWifiForm, null);
		}

		public void ChangeToPanel(WizardPanel panelToChangeTo)
		{
			this.RemoveAllChildren();
			this.AddChild(panelToChangeTo);
			this.Invalidate();
		}

		public void DoChangeToWifiForm(object state)
		{
			GuiWidget chooseConnectionWidget = new SetupWizardWifi(this);
			this.RemoveAllChildren();
			this.AddChild(chooseConnectionWidget);
			this.Invalidate();
		}

		public void ChangeToHome()
		{
			UiThread.RunOnIdle(DoChangeToHome, null);
		}

		public void DoChangeToHome(object state)
		{
			GuiWidget homeWidget = new SetupWizardHome(this);
			this.RemoveAllChildren();
			this.AddChild(homeWidget);
			this.Invalidate();
		}

		private int GetPrinterRecordCount()
		{
			return Datastore.Instance.RecordCount("Printer");
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
			if (string.IsNullOrEmpty(PrinterConnectionAndCommunication.Instance?.ActivePrinter?.BaudRate()))
			{
				ChangeToSetupBaudRate();
			}
			else
			{
				ChangeToSetupComPortOne();
			}
		}

		internal void ChangeToAuthPanel()
		{
			ChangeToStep(new ShowAuthPanel(this));
		}
	}
}