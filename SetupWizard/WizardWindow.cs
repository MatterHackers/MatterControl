﻿using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
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
		public static Action ChangeToAccountCreate;

		private static Dictionary<string, WizardWindow> allWindows = new Dictionary<string, WizardWindow>();

		private WizardWindow()
			: base(500 * GuiWidget.DeviceScale, 500 * GuiWidget.DeviceScale)
		{
			this.AlwaysOnTopOfMain = true;

			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Padding = new BorderDouble(8);
			this.MinimumSize = new Vector2(350 * GuiWidget.DeviceScale, 400 * GuiWidget.DeviceScale);

			this.ShowAsSystemWindow();
		}

		public static void Close(string uri)
		{
			WizardWindow existingWindow;

			if (allWindows.TryGetValue(uri, out existingWindow))
			{
				existingWindow.Close();
			}
		}

		public static void Show<PanelType>(string uri, string title) where PanelType : WizardPage, new()
		{
			WizardWindow wizardWindow = GetWindow(uri);
			wizardWindow.Title = title;
			wizardWindow.ChangeToPage<PanelType>();
		}

		public static void Show(string uri, string title, WizardPage wizardPage)
		{
			WizardWindow wizardWindow = GetWindow(uri);
			wizardWindow.Title = title;
			wizardWindow.ChangeToPage(wizardPage);
		}

		public static void ShowPrinterSetup()
		{
			WizardWindow wizardWindow = GetWindow("PrinterSetup");
			wizardWindow.Title = "Setup Wizard".Localize();

			// Do the printer setup logic
			// Todo - detect wifi connectivity
			bool WifiDetected = MatterControlApplication.Instance.IsNetworkConnected();
			if (!WifiDetected)
			{
				wizardWindow.ChangeToPage<SetupWizardWifi>();
			}
			else
			{
				wizardWindow.ChangeToSetupPrinterForm();
			}
		}

		public static void ShowComPortSetup()
		{
			WizardWindow wizardWindow = GetWindow("PrinterSetup");
			wizardWindow.Title = "Setup Wizard".Localize();

			wizardWindow.ChangeToPage<SetupStepComPortOne>();
		}

		public static bool IsOpen(string uri)
		{
			WizardWindow wizardWindow;

			if (allWindows.TryGetValue(uri, out wizardWindow))
			{
				return true;
			}

			return false;
		}

		private static WizardWindow GetWindow(string uri)
		{
			WizardWindow wizardWindow;

			if (allWindows.TryGetValue(uri, out wizardWindow))
			{
				wizardWindow.BringToFront();
			}
			else
			{
				wizardWindow = new WizardWindow();
				wizardWindow.Closed += (s, e) => allWindows.Remove(uri);
				allWindows[uri] = wizardWindow;
			}

			return wizardWindow;
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
			if (SetupStepInstallDriver.PrinterDrivers().Count > 0
				&& OsInformation.OperatingSystem == OSType.Windows)
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
			if (string.IsNullOrEmpty(PrinterConnectionAndCommunication.Instance?.ActivePrinter?.GetValue(SettingsKey.baud_rate)))
			{
				ChangeToPage<SetupStepBaudRate>();
			}
			else
			{
				ChangeToPage<SetupStepComPortOne>();
			}
		}

		internal void ChangeToPage(WizardPage pageToChangeTo)
		{
			pageToChangeTo.WizardWindow = this;
			UiThread.RunOnIdle(() =>
			{
				this.RemoveAllChildren();
				this.AddChild(pageToChangeTo);
				this.Invalidate();
			});
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