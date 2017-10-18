using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public static class PrinterSetup
	{
		public static Func<bool> ShouldShowAuthPanel { get; set; }
		public static Action ShowAuthDialog;
		public static Action ChangeToAccountCreate;

		public enum StartPageOptions { Default, SkipWifiSetup, ShowMakeModel }

		public static WizardPage GetBestStartPage(StartPageOptions options = StartPageOptions.Default)
		{
			// Do the printer setup logic
			bool WifiDetected = MatterControlApplication.Instance.IsNetworkConnected();
			if (!WifiDetected 
				&& options != StartPageOptions.SkipWifiSetup)
			{
				return new SetupWizardWifi();
			}
			else if (ShouldShowAuthPanel?.Invoke() == true
				&& options != StartPageOptions.ShowMakeModel)
			{
				return new ShowAuthPanel();
			}
			else
			{
				return new SetupStepMakeModelName();
			}
		}
	}
}
