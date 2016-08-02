using MatterHackers.Localizations;
using MatterHackers.Agg.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.SetupWizard
{
	public class SyncingPrintersPage: WizardPage
	{
		public SyncingPrintersPage()
		{
			TextWidget syncingText = new TextWidget("Syncing Profiles...".Localize(),textColor: ActiveTheme.Instance.PrimaryTextColor);
			contentRow.AddChild(syncingText);

			ApplicationController.SyncPrinterProfiles().ContinueWith((task) =>
			{
				if (!ProfileManager.Instance.ActiveProfiles.Any())
				{
					// Switch to setup wizard if no profiles exist
					WizardWindow.ChangeToSetupPrinterForm();
				}
				else if (ProfileManager.Instance.ActiveProfiles.Count() == 1)
				{
					//Set as active printer
					ActiveSliceSettings.SwitchToProfile(ProfileManager.Instance.ActiveProfiles.First().ID);
				}
				UiThread.RunOnIdle(WizardWindow.Close);
			});
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

		}

	}
}
