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

			this.WindowSize = new Vector2(500 * GuiWidget.DeviceScale, 550 * GuiWidget.DeviceScale);

			headerRow.CloseAllChildren();

			headerRow.HAnchor = HAnchor.Center | HAnchor.Fit;
			headerRow.AddChild(new TextWidget("MatterControl".Localize(), pointSize: 20) { Margin = new BorderDouble(right: 3) });
			headerRow.AddChild(new TextWidget("TM".Localize(), pointSize: 7) { VAnchor = VAnchor.Top });

			contentRow.AddChild(
				new TextWidget("Version".Localize() + " " + VersionInfo.Instance.BuildVersion, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Center
				});

			contentRow.AddChild(
				new TextWidget("Developed By".Localize() + ": " + "MatterHackers", pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Center
				});

			contentRow.AddChild(
				new ImageWidget(
					AggContext.StaticData.LoadIcon(Path.Combine("..", "Images", "mh-logo.png"), 250, 250))
				{
					HAnchor = HAnchor.Center,
					Margin = new BorderDouble(0, 25)
				});

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
				new TextWidget("Copyright © 2018 MatterHackers, Inc.", pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Center,
				});

			var clearCacheLink = theme.LinkButtonFactory.Generate("Clear Cache".Localize());
			clearCacheLink.HAnchor = HAnchor.Center;
			clearCacheLink.Cursor = Cursors.Hand;
			clearCacheLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				AboutWidget.DeleteCacheData(0);
			});
			contentRow.AddChild(clearCacheLink);
		}
	}
}