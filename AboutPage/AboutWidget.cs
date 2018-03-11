/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	public class AboutWidget : GuiWidget
	{
		public AboutWidget()
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Top;

			this.Padding = new BorderDouble(5);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			FlowLayoutWidget customInfoTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			customInfoTopToBottom.Name = "AboutPageCustomInfo";
			customInfoTopToBottom.HAnchor = HAnchor.Stretch;
			customInfoTopToBottom.VAnchor = VAnchor.MaxFitOrStretch;
			customInfoTopToBottom.Padding = new BorderDouble(5, 10, 5, 0);

			if (UserSettings.Instance.IsTouchScreen)
			{
				customInfoTopToBottom.AddChild(new UpdateControlView(ApplicationController.Instance.Theme));
			}

			//AddMatterHackersInfo(customInfoTopToBottom);
			customInfoTopToBottom.AddChild(new GuiWidget(1, 10));

			string aboutHtmlFile = Path.Combine("OEMSettings", "AboutPage.html");
			string htmlContent = AggContext.StaticData.ReadAllText(aboutHtmlFile);

#if false // test
			{
				SystemWindow releaseNotes = new SystemWindow(640, 480);
				string releaseNotesFile = Path.Combine("OEMSettings", "ReleaseNotes.html");
				string releaseNotesContent = AggContext.StaticData.ReadAllText(releaseNotesFile);
				HtmlWidget content = new HtmlWidget(releaseNotesContent, Color.Black);
				content.AddChild(new GuiWidget(HAnchor.AbsolutePosition, VAnchor.Stretch));
				content.VAnchor |= VAnchor.Top;
				content.BackgroundColor = Color.White;
				releaseNotes.AddChild(content);
				releaseNotes.BackgroundColor = Color.Cyan;
				UiThread.RunOnIdle((state) =>
				{
					releaseNotes.ShowAsSystemWindow();
				}, 1);
			}
#endif

			HtmlWidget htmlWidget = new HtmlWidget(htmlContent, ActiveTheme.Instance.PrimaryTextColor);

			customInfoTopToBottom.AddChild(htmlWidget);

			this.AddChild(customInfoTopToBottom);
		}

		public string CreateCenteredButton(string content)
		{
			throw new NotImplementedException();
		}

		public string CreateLinkButton(string content)
		{
			throw new NotImplementedException();
		}

		public string DoToUpper(string content)
		{
			throw new NotImplementedException();
		}

		public string DoTranslate(string content)
		{
			throw new NotImplementedException();
		}

		public string GetBuildString(string content)
		{
			return VersionInfo.Instance.BuildVersion;
		}

		public string GetVersionString(string content)
		{
			return VersionInfo.Instance.ReleaseVersion;
		}
	}
}