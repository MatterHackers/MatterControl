using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.AboutPage
{
	public class CheckForUpdatesPage : DialogPage
	{
		public CheckForUpdatesPage()
		: base("Close".Localize())
		{
			var theme = ApplicationController.Instance.Theme;

			this.WindowTitle = this.HeaderText = "Check for Update".Localize();
			this.Padding = 0;
			this.AnchorAll();

			// Clear padding so UpdateControlView toolbar appears like toolbar 
			contentRow.Padding = 0;

			// Update Status Widget
			contentRow.AddChild(
				new UpdateControlView(ApplicationController.Instance.Theme));

			var contentPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = 8
			};
			contentRow.AddChild(contentPanel);

			var currentBuildInfo = new TextWidget("Current Build".Localize() + $" : {VersionInfo.Instance.BuildVersion}")
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(left: 5, bottom: 15, top: 20),
				TextColor = theme.Colors.PrimaryTextColor
			};
			contentPanel.AddChild(currentBuildInfo);

			var currentFeedAndDropDownContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
			};
			contentPanel.AddChild(currentFeedAndDropDownContainer);

			var feedLabel = new TextWidget("Update Channel".Localize(), pointSize: 12)
			{
				TextColor = theme.Colors.PrimaryTextColor,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 5)
			};
			currentFeedAndDropDownContainer.AddChild(feedLabel);

			FlowLayoutWidget additionalInfoContainer = null;

			Button whatsThisLink = theme.HelpLinkFactory.Generate("What's this?".Localize());
			whatsThisLink.VAnchor = VAnchor.Center;
			whatsThisLink.Margin = new BorderDouble(left: 6);
			whatsThisLink.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					if (!additionalInfoContainer.Visible)
					{
						additionalInfoContainer.Visible = true;
					}
					else
					{
						additionalInfoContainer.Visible = false;
					}
				});
			};
			currentFeedAndDropDownContainer.AddChild(whatsThisLink);

			currentFeedAndDropDownContainer.AddChild(new HorizontalSpacer());


			var acceptableUpdateFeedTypeValues = new List<string>() { "release", "pre-release", "development" };

			string currentUpdateFeedType = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);
			if (acceptableUpdateFeedTypeValues.IndexOf(currentUpdateFeedType) == -1)
			{
				UserSettings.Instance.set(UserSettingsKey.UpdateFeedType, "release");
			}

			var releaseOptionsDropList = new DropDownList("Development", theme.Colors.PrimaryTextColor, maxHeight: 200)
			{
				HAnchor = HAnchor.Fit
			};
			releaseOptionsDropList.AddItem("Stable".Localize(), "release");
			releaseOptionsDropList.AddItem("Beta".Localize(), "pre-release");
			releaseOptionsDropList.AddItem("Alpha".Localize(), "development");
			releaseOptionsDropList.SelectedValue = currentUpdateFeedType;
			releaseOptionsDropList.SelectionChanged += (s, e) =>
			{
				string releaseCode = releaseOptionsDropList.SelectedValue;
				if (releaseCode != UserSettings.Instance.get(UserSettingsKey.UpdateFeedType))
				{
					UserSettings.Instance.set(UserSettingsKey.UpdateFeedType, releaseCode);
				}

				UpdateControlData.Instance.CheckForUpdateUserRequested();
			};
			currentFeedAndDropDownContainer.AddChild(releaseOptionsDropList);

			additionalInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor,
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(left: 6, top: 6),
				Visible = false
			};
			additionalInfoContainer.AddChild(
				new WrappedTextWidget("Changing your update channel will change the version of MatterControl that you receive when updating".Localize() + ":")
				{
					TextColor = theme.Colors.PrimaryTextColor,
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(bottom: 20)
				});

			additionalInfoContainer.AddChild(
				new WrappedTextWidget("Stable: The current release version of MatterControl (recommended)".Localize())
				{
					TextColor = theme.Colors.PrimaryTextColor,
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(bottom: 10)
				});

			additionalInfoContainer.AddChild(
				new WrappedTextWidget("Beta: The release candidate version of MatterControl".Localize())
				{
					TextColor = theme.Colors.PrimaryTextColor,
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(bottom: 10)
				});

			additionalInfoContainer.AddChild(
				new WrappedTextWidget("Alpha: The in development version of MatterControl".Localize())
				{
					TextColor = theme.Colors.PrimaryTextColor,
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(bottom: 10)
				});
			contentPanel.AddChild(additionalInfoContainer);
		}
	}
}
