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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public class AboutPage : DialogPage
	{
		public AboutPage()
			: base("Close".Localize())
		{
			this.WindowTitle = "About".Localize() + " " + ApplicationController.Instance.ProductName;
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
			productTitle.AddChild(new TextWidget("MatterControl".Localize(), textColor: theme.TextColor, pointSize: 20) { Margin = new BorderDouble(right: 3) });
			productTitle.AddChild(new TextWidget("TM".Localize(), textColor: theme.TextColor, pointSize: 7) { VAnchor = VAnchor.Top });

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
			var accentColor = theme.PrimaryAccentColor;

			var spinner = new LogoSpinner(spinnerPanel, 4, 0.2, 0, rotateX: 0)
			{
				/*
				MeshColor = new Color(175, 175, 175, 255),
				AmbientColor = new float[]
				{
					accentColor.Red0To1,
					accentColor.Green0To1,
					accentColor.Blue0To1,
					0
				}*/
			};

			productInfo.AddChild(
				new TextWidget("Version".Localize() + " " + VersionInfo.Instance.BuildVersion, textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Center
				});

			productInfo.AddChild(
				new TextWidget("Developed By".Localize() + ": " + "MatterHackers", textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Center
				});

			contentRow.AddChild(
				new WrappedTextWidget(
					"MatterControl is made possible by the team at MatterHackers and other open source software".Localize() + ":",
					pointSize: theme.DefaultFontSize,
					textColor: theme.TextColor)
				{
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(0, 15)
				});

			var licensePanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(bottom: 15)
			};

			var data = JsonConvert.DeserializeObject<List<LibraryLicense>>(AggContext.StaticData.ReadAllText(Path.Combine("License", "license.json")));

			var linkIcon = AggContext.StaticData.LoadIcon("fa-link_16.png", 16, 16, theme.InvertIcons);

			SectionWidget section = null;

			foreach (var item in data.OrderBy(i => i.Name))
			{
				var linkButton = new IconButton(linkIcon, theme);
				linkButton.Click += (s, e) => UiThread.RunOnIdle(() =>
				{
					ApplicationController.Instance.LaunchBrowser(item.Url);
				});

				section = new SectionWidget(item.Title ?? item.Name, new LazyLicenseText(item.Name, theme), theme, linkButton, expanded: false)
				{
					HAnchor = HAnchor.Stretch
				};
				licensePanel.AddChild(section);
			}

			// Apply a bottom border to the last time for balance
			if (section != null)
			{
				section.Border = section.Border.Clone(bottom: 1);
			}

			var scrollable = new ScrollableWidget(autoScroll: true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(bottom: 10),
			};

			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
			scrollable.AddChild(licensePanel);
			contentRow.AddChild( scrollable);

			var feedbackButton = new TextButton("Send Feedback", theme)
			{
				BackgroundColor = theme.MinimalShade,
				HAnchor = HAnchor.Absolute,
			};
			feedbackButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				this.DialogWindow.ChangeToPage<ContactFormPage>();
			});

			this.AddPageAction(feedbackButton, highlightFirstAction: false);

			contentRow.AddChild(
				new TextWidget("Copyright © 2018 MatterHackers, Inc.", textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Center,
				});

			var siteLink = new LinkLabel("www.matterhackers.com", theme)
			{
				HAnchor = HAnchor.Center,
				TextColor = theme.TextColor
			};
			siteLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.LaunchBrowser("http://www.matterhackers.com");
			});
			contentRow.AddChild(siteLink);
		}

		private class LazyLicenseText : GuiWidget
		{
			private string sourceName;
			private ThemeConfig theme;

			public LazyLicenseText(string sourceName, ThemeConfig theme)
			{
				this.sourceName = sourceName;
				this.theme = theme;

				this.HAnchor = HAnchor.Stretch;
				this.VAnchor = VAnchor.Fit;
				this.MinimumSize = new Vector2(0, 10);
			}

			public override void OnLoad(EventArgs args)
			{
				string filePath = Path.Combine("License", $"{sourceName}.txt");
				if (AggContext.StaticData.FileExists(filePath))
				{
					string content = AggContext.StaticData.ReadAllText(filePath);

					this.AddChild(new WrappedTextWidget(content, theme.DefaultFontSize, textColor: theme.TextColor)
					{
						HAnchor = HAnchor.Stretch
					});
				}

				base.OnLoad(args);
			}
		}

		private class LibraryLicense
		{
			public string Url { get; set; }
			public string Name { get; set; }
			public string Title { get; set; }
		}
	}
}