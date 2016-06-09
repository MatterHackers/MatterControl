using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class WizardWindow : SystemWindow
	{
		public static Func<bool> ShouldShowAuthPanel { get; set; }
		public static Action ShowAuthDialog;
		private static WizardWindow wizardWindow = null;

		private static Dictionary<string, WizardWindow> allWindows = new Dictionary<string, WizardWindow>();

		private WizardWindow()
			: base(500 * GuiWidget.DeviceScale, 500 * GuiWidget.DeviceScale)
		{
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Padding = new BorderDouble(8);
			this.ShowAsSystemWindow();
			this.MinimumSize = new Vector2(350 * GuiWidget.DeviceScale, 400 * GuiWidget.DeviceScale);
		}

		private WizardWindow(bool openToHome = false)
			: base(500 * GuiWidget.DeviceScale, 500 * GuiWidget.DeviceScale)
		{
			AlwaysOnTopOfMain = true;
			this.Title = "Setup Wizard".Localize();

			// Todo - detect wifi connectivity
			bool WifiDetected = MatterControlApplication.Instance.IsNetworkConnected();
			if (!WifiDetected)
			{
				ChangeToPage<SetupWizardWifi>();
			}
			else
			{
				ChangeToSetupPrinterForm();
			}

			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Padding = new BorderDouble(8);
			this.ShowAsSystemWindow();
			this.MinimumSize = new Vector2(350 * GuiWidget.DeviceScale, 400 * GuiWidget.DeviceScale);
		}

		public static void Show<PanelType>(string uri, string title) where PanelType : WizardPage, new()
		{
			WizardWindow existingWindow;

			if (allWindows.TryGetValue(uri, out existingWindow))
			{
				existingWindow.BringToFront();
			}
			else
			{
				existingWindow = new WizardWindow();
				existingWindow.Closed += (s, e) => allWindows.Remove(uri);
				allWindows[uri] = existingWindow;
			}

			existingWindow.Title = title;
			existingWindow.ChangeToPage<PanelType>();
		}

		public static void Show(bool openToHome = false)
		{
			if (wizardWindow == null)
			{
				wizardWindow = new WizardWindow(openToHome);
				wizardWindow.Closed += (s, e) => wizardWindow = null;
			}
			else
			{
				wizardWindow.BringToFront();
			}
		}

		public override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
		}

		public void ChangeToSetupPrinterForm()
		{
			bool showAuthPanel = ShouldShowAuthPanel?.Invoke() ?? false;
			if (showAuthPanel)
			{
				ChangeToPage<ShowAuthPanel>();
			}
			else
			{
				ChangeToPage<SetupStepMakeModelName>();
			}
		}

		internal void ChangeToInstallDriverOrComPortOne()
		{
			if (ActiveSliceSettings.Instance.PrinterDrivers().Count > 0)
			{
				ChangeToPage<SetupStepInstallDriver>();
			}
			else
			{
				ChangeToPage<SetupStepComPortOne>();
			}
		}

		internal void ChangeToSetupBaudOrComPortOne()
		{
			if (string.IsNullOrEmpty(PrinterConnectionAndCommunication.Instance?.ActivePrinter?.BaudRate()))
			{
				ChangeToPage<SetupStepBaudRate>();
			}
			else
			{
				ChangeToPage<SetupStepComPortOne>();
			}
		}

		internal void ChangeToPage<PanelType>() where PanelType : WizardPage, new()
		{
			UiThread.RunOnIdle(() =>
			{
				this.RemoveAllChildren();
				this.AddChild(new PanelType() { WizardWindow = this });
				this.Invalidate();
			});
		}
	}
}