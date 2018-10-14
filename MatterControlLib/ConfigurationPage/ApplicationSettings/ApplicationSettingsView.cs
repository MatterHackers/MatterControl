/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class ApplicationSettingsWidget : FlowLayoutWidget, IIgnoredPopupChild
	{
		public static Action<DialogWindow> OpenPrintNotification = null;

		private ThemeConfig theme;

		public ApplicationSettingsWidget(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.BackgroundColor = theme.Colors.PrimaryBackgroundColor;
			this.theme = theme;

			AddMenuItem("Help".Localize(), ApplicationController.Instance.ShowApplicationHelp);

			AddMenuItem("Forums".Localize(), () => ApplicationController.Instance.LaunchBrowser("https://forums.matterhackers.com/category/20/mattercontrol"));
			AddMenuItem("Wiki".Localize(), () => ApplicationController.Instance.LaunchBrowser("http://wiki.mattercontrol.com"));
			AddMenuItem("Guides and Articles".Localize(), () => ApplicationController.Instance.LaunchBrowser("http://www.matterhackers.com/topic/mattercontrol"));
			AddMenuItem("Release Notes".Localize(), () => ApplicationController.Instance.LaunchBrowser("http://wiki.mattercontrol.com/Release_Notes"));
			AddMenuItem("Report a Bug".Localize(), () => ApplicationController.Instance.LaunchBrowser("https://github.com/MatterHackers/MatterControl/issues"));
			AddMenuItem("Settings".Localize(), () => DialogWindow.Show<ApplicationSettingsPage>(), "Settings MenuItem");

			var updateMatterControl = new SettingsItem("Check For Update".Localize(), theme);
			updateMatterControl.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					UpdateControlData.Instance.CheckForUpdate();
					DialogWindow.Show<CheckForUpdatesPage>();
				});
			};
			this.AddSettingsRow(updateMatterControl);

			var aboutMatterControl = new SettingsItem("About".Localize() + " MatterControl", theme);
			if (IntPtr.Size == 8)
			{
				// Push right
				aboutMatterControl.AddChild(new HorizontalSpacer());

				// Add x64 adornment
				var blueBox = new FlowLayoutWidget()
				{
					Margin = new BorderDouble(10, 0),
					Padding = new BorderDouble(2),
					Border = new BorderDouble(1),
					BorderColor = theme.Colors.PrimaryAccentColor,
					VAnchor = VAnchor.Center | VAnchor.Fit,
				};
				blueBox.AddChild(new TextWidget("64", pointSize: 8, textColor: theme.Colors.PrimaryAccentColor));

				aboutMatterControl.AddChild(blueBox);
			}
			aboutMatterControl.Click += (s, e) =>
			{
				ApplicationController.Instance.ShowAboutPage();
			};
			this.AddSettingsRow(aboutMatterControl);
		}

		public bool KeepMenuOpen => false;

		private void AddMenuItem(string title, Action callback, string widgetName = null)
		{
			var newItem = new SettingsItem(title, theme);
			newItem.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					callback?.Invoke();
				});
			};

			if (widgetName != null)
			{
				newItem.Name = widgetName;
			}

			this.AddSettingsRow(newItem);
		}

		private void AddSettingsRow(GuiWidget widget)
		{
			this.AddChild(widget);
			widget.Padding = widget.Padding.Clone(right: 10);
		}
	}
}