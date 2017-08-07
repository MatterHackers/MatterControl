using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.AboutPage
{
	public class CheckForUpdatesPage : WizardPage
	{
		public CheckForUpdatesPage()
		: base("Close", "Check for Update")
		{
			AnchorAll();

			var theme = ApplicationController.Instance.Theme;

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			Padding = 0;

			FlowLayoutWidget currentFeedAndDropDownContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			currentFeedAndDropDownContainer.VAnchor = VAnchor.FitToChildren;
			currentFeedAndDropDownContainer.HAnchor = HAnchor.ParentLeftRight;
			currentFeedAndDropDownContainer.Margin = new BorderDouble(0, 5, 0, 0);
			currentFeedAndDropDownContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			
			UpdateControlView updateStatusWidget = new UpdateControlView(ApplicationController.Instance.Theme);

			TextWidget feedLabel = new TextWidget("Update Channel".Localize(), pointSize: 12);
			feedLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			feedLabel.VAnchor = VAnchor.ParentCenter;
			feedLabel.Margin = new BorderDouble(left: 5);

			var releaseOptionsDropList = new DropDownList("Development", maxHeight: 200);
			releaseOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

			releaseOptionsDropList.AddItem("Stable".Localize(), "release");
			releaseOptionsDropList.AddItem("Beta".Localize(), "pre-release");
			releaseOptionsDropList.AddItem("Alpha".Localize(), "development");

			var acceptableUpdateFeedTypeValues = new List<string>() { "release", "pre-release", "development" };

			string currentUpdateFeedType = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);
			if (acceptableUpdateFeedTypeValues.IndexOf(currentUpdateFeedType) == -1)
			{
				UserSettings.Instance.set(UserSettingsKey.UpdateFeedType, "release");
			}

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

			string currentBuildNo = VersionInfo.Instance.BuildVersion;
			string currentBuildInfoLabel = String.Format("Current Build : {0}", currentBuildNo);

			var currentBuildInfo = new TextWidget(currentBuildInfoLabel.Localize());
			currentBuildInfo.HAnchor = HAnchor.ParentLeftRight;
			currentBuildInfo.Margin = new BorderDouble(left: 5, bottom: 15, top: 20);
			currentBuildInfo.TextColor = ActiveTheme.Instance.PrimaryTextColor;

			FlowLayoutWidget additionalInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			additionalInfoContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			additionalInfoContainer.HAnchor = HAnchor.ParentLeftRight;
			additionalInfoContainer.Padding = new BorderDouble(left: 6, top: 6);

			string aboutUpdateChannel = "Changing your update channel will change the version of MatterControl \nthat you receive when updating:".Localize();
			var updateChannelLabel = new TextWidget(aboutUpdateChannel);
			updateChannelLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			updateChannelLabel.HAnchor = HAnchor.ParentLeftRight;
			updateChannelLabel.Margin = new BorderDouble(bottom: 20);
			additionalInfoContainer.AddChild(updateChannelLabel);

			string stableFeedInfoText = "Stable: The current release version of MatterControl (recommended).".Localize();
			var stableInfoLabel = new TextWidget(stableFeedInfoText);
			stableInfoLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			stableInfoLabel.HAnchor = HAnchor.ParentLeftRight;
			stableInfoLabel.Margin = new BorderDouble(bottom: 10);
			additionalInfoContainer.AddChild(stableInfoLabel);

			string betaFeedInfoText = "Beta: The release candidate version of MatterControl.".Localize();
			var betaInfoLabel = new TextWidget(betaFeedInfoText);
			betaInfoLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			betaInfoLabel.HAnchor = HAnchor.ParentLeftRight;
			betaInfoLabel.Margin = new BorderDouble(bottom: 10);
			additionalInfoContainer.AddChild(betaInfoLabel);

			string alphaFeedInfoText = "Alpha: The in development version of MatterControl.".Localize();
			var alphaInfoLabel = new TextWidget(alphaFeedInfoText);
			alphaInfoLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			alphaInfoLabel.HAnchor = HAnchor.ParentLeftRight;
			alphaInfoLabel.Margin = new BorderDouble(bottom: 10);
			additionalInfoContainer.AddChild(alphaInfoLabel);

			Button whatsThisLink = theme.HelpLinkFactory.Generate("What's this?".Localize());
			whatsThisLink.VAnchor = VAnchor.ParentCenter;
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

			topToBottom.AddChild(updateStatusWidget);
			topToBottom.AddChild(currentBuildInfo);
			currentFeedAndDropDownContainer.AddChild(feedLabel);
			currentFeedAndDropDownContainer.AddChild(whatsThisLink);
			currentFeedAndDropDownContainer.AddChild(new HorizontalSpacer());
			currentFeedAndDropDownContainer.AddChild(releaseOptionsDropList);
			topToBottom.AddChild(currentFeedAndDropDownContainer);

			topToBottom.AddChild(additionalInfoContainer);

			contentRow.AddChild(topToBottom);

			additionalInfoContainer.Visible = false;

			//Add buttons to buttonContainer
			//footerRow.AddChild(acceptButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
			cancelButton.Visible = true;

			footerRow.Visible = true;

			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
		}
	}
}
