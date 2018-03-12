/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class AboutPage : DialogPage
	{
		public AboutPage()
			: base("Close".Localize())
		{
			this.WindowTitle = "About".Localize() + " " + ApplicationController.Instance.ProductName;

			var theme = ApplicationController.Instance.Theme;

			this.MinimumSize = new Vector2(480 * GuiWidget.DeviceScale, 520 * GuiWidget.DeviceScale);
			this.WindowSize = new Vector2(500 * GuiWidget.DeviceScale, 550 * GuiWidget.DeviceScale);

			contentRow.BackgroundColor = Color.Transparent;

			headerRow.Visible = false;

			var altHeadingRow = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Height = 100,
			};
			contentRow.AddChild(altHeadingRow);

			var productInfo = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Center | HAnchor.Fit,
				VAnchor = VAnchor.Center | VAnchor.Fit
			};

			var productTitle = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Center | HAnchor.Fit
			};
			productTitle.AddChild(new TextWidget("MatterControl".Localize(), textColor: theme.Colors.PrimaryTextColor, pointSize: 20) { Margin = new BorderDouble(right: 3) });
			productTitle.AddChild(new TextWidget("TM".Localize(), textColor: theme.Colors.PrimaryTextColor, pointSize: 7) { VAnchor = VAnchor.Top });

			altHeadingRow.AddChild(productInfo);
			productInfo.AddChild(productTitle);

			var spinnerPanel = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute | HAnchor.Left,
				VAnchor = VAnchor.Absolute,
				Height = 100,
				Width = 100,
			};
			altHeadingRow.AddChild(spinnerPanel);
			var spinner = new LogoSpinner(spinnerPanel, 4, 0.2, 0, rotateX: 0)

			productInfo.AddChild(
				new TextWidget("Version".Localize() + " " + VersionInfo.Instance.BuildVersion, textColor: theme.Colors.PrimaryTextColor, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Center
				});

			productInfo.AddChild(
				new TextWidget("Developed By".Localize() + ": " + "MatterHackers", textColor: theme.Colors.PrimaryTextColor, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Center
				});

			var infoRow = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Center |HAnchor.Fit,
				Margin = new BorderDouble(top: 20)
			};
			contentRow.AddChild(infoRow);

			infoRow.AddChild(
				new TextWidget("MatterControl is made possible by the team at MatterHackers and".Localize(), textColor: theme.Colors.PrimaryTextColor, pointSize: theme.FontSize10));

			var originalFontSize = theme.LinkButtonFactory.fontSize;

			theme.LinkButtonFactory.fontSize = theme.FontSize10;

			var ossLink = theme.LinkButtonFactory.Generate("other open source software".Localize());
			ossLink.Margin = new BorderDouble(left: 3, top: 3);
			ossLink.Cursor = Cursors.Hand;
			ossLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				// Show attributes
			});
			infoRow.AddChild(ossLink);

			//contentRow.AddChild(
			//	new ImageWidget(
			//		AggContext.StaticData.LoadIcon(Path.Combine("..", "Images", "mh-logo.png"), 250, 250))
			//	{
			//		HAnchor = HAnchor.Center,
			//		Margin = new BorderDouble(0, 25)
			//	});

			contentRow.AddChild(new VerticalSpacer());

			var button = new TextButton("Send Feedback", theme)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Absolute,
				BackgroundColor = theme.MinimalShade,
				Margin = new BorderDouble(bottom: 20)
			};
			button.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				ContactFormWindow.Open();
			});

			contentRow.AddChild(button);

			var siteLink = theme.LinkButtonFactory.Generate("www.matterhackers.com");
			siteLink.HAnchor = HAnchor.Center;
			siteLink.Cursor = Cursors.Hand;
			siteLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.LaunchBrowser("http://www.matterhackers.com");
			});
			contentRow.AddChild(siteLink);

			contentRow.AddChild(
				new TextWidget("Copyright © 2018 MatterHackers, Inc.", textColor: theme.Colors.PrimaryTextColor, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Center,
				});

			var clearCacheLink = theme.LinkButtonFactory.Generate("Clear Cache".Localize());
			clearCacheLink.HAnchor = HAnchor.Center;
			clearCacheLink.Cursor = Cursors.Hand;
			clearCacheLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				CacheDirectory.DeleteCacheData(0);
			});
			contentRow.AddChild(clearCacheLink);

			theme.LinkButtonFactory.fontSize = originalFontSize;
		}
	}
}