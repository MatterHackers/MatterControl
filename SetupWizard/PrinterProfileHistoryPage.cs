using MatterHackers.Agg;
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
			revertButton.Click += async (s, e) =>
			{
				var checkedButton = radioButtonList.Where(r => r.Checked).FirstOrDefault();
				if (checkedButton != null)
				{
					string profileToken = printerProfileData[checkedButton.Text];

					var activeProfile = ProfileManager.Instance.ActiveProfile;

					// Download the specified json profile
					await ApplicationController.GetPrinterProfile(activeProfile, profileToken);

					// TODO: handle errors...

					// Update the active instance to the newly downloaded item
					var jsonProfile = ProfileManager.LoadProfile(activeProfile.ID, false);
					ActiveSliceSettings.RefreshActiveInstance(jsonProfile);
					UiThread.RunOnIdle(WizardWindow.Close);
				}
			};

			LoadHistoryItems();
		}

		private async void LoadHistoryItems()
		{
			TextWidget loadingText = new TextWidget("Retrieving History from Web...");
			loadingText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			scrollWindow.AddChild(loadingText);
			

			var results = await ApplicationController.GetProfileHistory(ProfileManager.Instance.ActiveProfile.DeviceToken);
			printerProfileData = results;
			if(printerProfileData != null)
			{
				FlowLayoutWidget radioButtonLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
				loadingText.Visible= false;

				foreach(var printerProfile in results)
				{
					var profileVersionButton = new RadioButton(printerProfile.Key.ToString(), textColor: ActiveTheme.Instance.PrimaryTextColor);
					profileVersionButton.Checked = false;
					radioButtonLayout.AddChild(profileVersionButton);
					radioButtonList.Add(profileVersionButton);
					// show them
				}
				scrollWindow.AddChild(radioButtonLayout);
			}
			else
			{
				loadingText.Text = "Failed To Download History!";
				loadingText.TextColor = RGBA_Bytes.Red; //CHANGE TO ERROR COLOR
			}
			
			//remove loading profile text/icon
		}

	}
}
