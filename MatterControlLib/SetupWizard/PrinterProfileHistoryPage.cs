using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.SetupWizard
{
	class PrinterProfileHistoryPage : DialogPage
	{
		List<RadioButton> radioButtonList = new List<RadioButton>();
		Dictionary<string, string> printerProfileData = new Dictionary<string, string>();
		List<string> orderedProfiles = new List<string>();
		private PrinterConfig printer;
		ScrollableWidget scrollWindow;

		public PrinterProfileHistoryPage(PrinterConfig printer)
		{
			this.WindowTitle = "Restore Settings".Localize();
			this.HeaderText = "Restore Settings".Localize();
			this.printer = printer;

			scrollWindow = new ScrollableWidget()
			{
				AutoScroll = true,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			scrollWindow.ScrollArea.HAnchor = HAnchor.Stretch;
			contentRow.FlowDirection = FlowDirection.TopToBottom;
			contentRow.AddChild(scrollWindow);

			var revertButton = theme.CreateDialogButton("Restore".Localize());
			revertButton.Click += async (s, e) =>
			{
				int index = radioButtonList.IndexOf(radioButtonList.Where(r => r.Checked).FirstOrDefault());

				if (index != -1)
				{
					string profileToken = printerProfileData[orderedProfiles[index]];

					var profile = ProfileManager.Instance[printer.Settings.ID];

					// Download the specified json profile
					var printerSettings = await ApplicationController.GetPrinterProfileAsync(profile, profileToken);
					if (printerSettings != null)
					{
						// Persist downloaded profile
						printerSettings.Save();

						// Update/switch printer instance to new settings
						printer.SwapToSettings(printerSettings);
					}

					UiThread.RunOnIdle(DialogWindow.Close);
				}
			};
			this.AddPageAction(revertButton);

			LoadHistoryItems();
		}

		private async void LoadHistoryItems()
		{
			TextWidget loadingText = new TextWidget("Retrieving History from Web...");
			loadingText.TextColor = theme.TextColor;
			scrollWindow.AddChild(loadingText);

			var profile = ProfileManager.Instance[printer.Settings.ID];

			var results = await ApplicationController.GetProfileHistory?.Invoke(profile.DeviceToken);
			printerProfileData = results;
			if(printerProfileData != null)
			{
				loadingText.Visible= false;

				List<DateTime> sourceTimes = new List<DateTime>();
				foreach (var printerProfile in results.OrderByDescending(d => d.Key))
				{
					// AppEngine results are current in the form of: "2016-07-21 00:43:30.965830"
					sourceTimes.Add(Convert.ToDateTime(printerProfile.Key).ToLocalTime());
				}

				var groupedTimes = RelativeTime.GroupTimes(DateTime.Now, sourceTimes);

				FlowLayoutWidget topToBottomStuff = new FlowLayoutWidget(FlowDirection.TopToBottom);
				scrollWindow.AddChild(topToBottomStuff);
				foreach (var group in groupedTimes)
				{
					// add in the group header
					topToBottomStuff.AddChild(new TextWidget(RelativeTime.BlockDescriptions[group.Key], textColor: theme.TextColor)
					{
						Margin = new BorderDouble(0, 0, 0, 5),
					});

					foreach (var time in group.Value)
					{
						// add in the radio buttons
						var profileVersionButton = new RadioButton(time.Value, textColor: theme.TextColor)
						{
							Margin = new BorderDouble(5, 0),
						};
						profileVersionButton.Checked = false;
						radioButtonList.Add(profileVersionButton);
						topToBottomStuff.AddChild(profileVersionButton);
					}
				}

				foreach(var printerProfile in results)
				{
					orderedProfiles.Add(printerProfile.Key.ToString());
				}
			}
			else
			{
				loadingText.Text = "Failed To Download History!";
				loadingText.TextColor = Color.Red;
			}

			//remove loading profile text/icon
		}

	}
}
