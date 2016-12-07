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
		TextWidget syncingDetails;
		public SyncingPrintersPage()
			: base("Close")
		{
			TextWidget syncingText = new TextWidget("Syncing Profiles...".Localize(),textColor: ActiveTheme.Instance.PrimaryTextColor);
			syncingDetails = new TextWidget("Retrieving sync information...".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize:10);
			syncingDetails.AutoExpandBoundsToText = true;
			contentRow.AddChild(syncingText);
			contentRow.AddChild(syncingDetails);
			Progress<SyncReportType> progress = new Progress<SyncReportType>(ReportProgress);

			ApplicationController.SyncPrinterProfiles("SyncingPrintersPage.ctor()", progress).ContinueWith((task) =>
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
					// only close the window if we are not switching to the setup printer form
					UiThread.RunOnIdle(WizardWindow.Close);
				}
			});
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}

		private void ReportProgress(SyncReportType report)
		{
			syncingDetails.Text = report.actionLabel;
		}
	}
}
