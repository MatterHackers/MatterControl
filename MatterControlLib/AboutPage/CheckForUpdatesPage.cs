/*
Copyright (c) 2018, Lars Brubaker, John Lewin, Greg Diaz
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class CheckForUpdatesPage : DialogPage
	{
		public CheckForUpdatesPage()
		: base("Close".Localize())
		{
			this.WindowTitle = this.HeaderText = "Check for Update".Localize();
			this.Padding = 0;
			this.AnchorAll();

			// Clear padding so UpdateControlView toolbar appears like toolbar
			contentRow.Padding = 0;

			// Update Status Widget
			contentRow.AddChild(
				new UpdateControlView(theme));

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
				TextColor = theme.TextColor
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
				TextColor = theme.TextColor,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 5)
			};
			currentFeedAndDropDownContainer.AddChild(feedLabel);

			FlowLayoutWidget additionalInfoContainer = null;

			var whatsThisLink = new LinkLabel("What's this?".Localize(), theme)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 6),
			};
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

			var releaseOptionsDropList = new MHDropDownList("Development", theme, maxHeight: 200)
			{
				HAnchor = HAnchor.Fit,
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

				UpdateControlData.Instance.CheckForUpdate();
			};
			currentFeedAndDropDownContainer.AddChild(releaseOptionsDropList);

			additionalInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				BackgroundColor = theme.MinimalShade,
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(left: 6, top: 6),
				Visible = false
			};
			additionalInfoContainer.AddChild(
				new WrappedTextWidget("Changing your update channel will change the version of MatterControl that you receive when updating".Localize() + ":")
				{
					TextColor = theme.TextColor,
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(bottom: 20)
				});

			additionalInfoContainer.AddChild(
				new WrappedTextWidget("Stable: The current release version of MatterControl (recommended)".Localize())
				{
					TextColor = theme.TextColor,
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(bottom: 10)
				});

			additionalInfoContainer.AddChild(
				new WrappedTextWidget("Beta: The release candidate version of MatterControl".Localize())
				{
					TextColor = theme.TextColor,
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(bottom: 10)
				});

			additionalInfoContainer.AddChild(
				new WrappedTextWidget("Alpha: The in development version of MatterControl".Localize())
				{
					TextColor = theme.TextColor,
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(bottom: 10)
				});
			contentPanel.AddChild(additionalInfoContainer);
		}
	}
}
