using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.SetupWizard
{
	class PrinterProfileHistoryPage : WizardPage
	{
		List<RadioButton> radioButtonList = new List<RadioButton>();
		Dictionary<string, string> printerProfileData = new Dictionary<string, string>();
		ScrollableWidget scrollWindow;

		public PrinterProfileHistoryPage()
		{
			scrollWindow = new ScrollableWidget()
			{
				AutoScroll = true,
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
			};
			scrollWindow.ScrollArea.HAnchor = HAnchor.ParentLeftRight;
			contentRow.FlowDirection = FlowDirection.TopToBottom;
			contentRow.AddChild(scrollWindow);

			var revertButton = textImageButtonFactory.Generate("Revert");
			footerRow.AddChild(revertButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
			revertButton.Click += (s, e) =>
			{
				var checkedButton = radioButtonList.Where(r => r.Checked).FirstOrDefault();
				if(checkedButton != null)
				{
					string profileToken = printerProfileData[checkedButton.Text];
					ProfileManager.Instance.LoadProfileFromMCWS(profileToken);
					//Call get profile
				}
			};

			LoadHistoryItems();
		}

		private async void LoadHistoryItems()
		{
			var printer = ProfileManager.Instance.ActiveProfiles.FirstOrDefault();

			var results = await ApplicationController.GetProfileHistory(printer.DeviceToken);
			printerProfileData = results;
			FlowLayoutWidget radioButtonLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);

			foreach(var printerProfile in results)
			{
				var profileVersionButton = new RadioButton(printerProfile.Key.ToString(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				profileVersionButton.Checked = false;
				radioButtonLayout.AddChild(profileVersionButton);
				radioButtonList.Add(profileVersionButton);
				// show them
			}
			scrollWindow.AddChild(radioButtonLayout);
			//remove loading profile text/icon
		}

	}
}
