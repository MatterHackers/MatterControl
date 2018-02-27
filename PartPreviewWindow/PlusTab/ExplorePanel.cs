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

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class ExplorePanel : ScrollableWidget
	{
		private ThemeConfig theme;
		private FlowLayoutWidget topToBottom;
		private TextWidget headingA;
		private Toolbar toolBarA;

		public ExplorePanel(PartPreviewContent partPreviewContent, SimpleTabs simpleTabs, ThemeConfig theme)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.BackgroundColor = theme.TabBodyBackground;
			this.MinimumSize = new Vector2(0, 200);
			this.AnchorAll();
			this.AutoScroll = true;
			this.ScrollArea.Padding = new BorderDouble(3);
			this.ScrollArea.HAnchor = HAnchor.Stretch;
			this.theme = theme;

			topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};
			this.AddChild(topToBottom);

			var columnA = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(20, 10),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			topToBottom.AddChild(columnA);

			columnA.AddChild(headingA = new TextWidget("Create".Localize(), pointSize: theme.H1PointSize, textColor: ActiveTheme.Instance.PrimaryTextColor, bold: true)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(20, 5)
			});

			columnA.AddChild(toolBarA = new Toolbar()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			});

			var createPart = new TextButton("Create Part".Localize(), theme)
			{
				Margin = theme.ButtonSpacing
			};
			createPart.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					BedConfig bed;
					simpleTabs.RemoveTab(simpleTabs.ActiveTab);
					partPreviewContent.CreatePartTab(
						"New Part",
						bed = new BedConfig(),
						theme);

					bed.LoadContent(
						new EditContext()
						{
							ContentStore = ApplicationController.Instance.Library.PlatingHistory,
							SourceItem = BedConfig.NewPlatingItem()
						}).ConfigureAwait(false);
				});
			};
			toolBarA.AddChild(createPart);

			var createPrinter = new TextButton("Create Printer".Localize(), theme)
			{
				Name = "Create Printer",
				Margin = theme.ButtonSpacing
			};
			createPrinter.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					simpleTabs.RemoveTab(simpleTabs.ActiveTab);

					if (ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPrinting
					|| ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPaused)
					{
						StyledMessageBox.ShowMessageBox("Please wait until the print has finished and try again.".Localize(), "Can't add printers while printing".Localize());
					}
					else
					{
						DialogWindow.Show(PrinterSetup.GetBestStartPage(PrinterSetup.StartPageOptions.ShowMakeModel));
					}
				});
			};
			toolBarA.AddChild(createPrinter);

			var importButton = new TextButton("Import Printer".Localize(), theme)
			{
				Margin = theme.ButtonSpacing
			};
			importButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					AggContext.FileDialogs.OpenFileDialog(
						new OpenFileDialogParams(
							"settings files|*.ini;*.printer;*.slice"),
							(result) =>
							{
								if (!string.IsNullOrEmpty(result.FileName)
									&& File.Exists(result.FileName))
								{
									simpleTabs.RemoveTab(simpleTabs.ActiveTab);
									ImportSettingsPage.ImportFromExisting(result.FileName);
								}
							});
				});
			};
			toolBarA.AddChild(importButton);

			toolBarA.AddChild(new VerticalLine(50) { Margin = new BorderDouble(12, 0) });

			toolBarA.AddChild(new TextWidget("Open Existing".Localize() + ":", textColor: theme.Colors.PrimaryTextColor, pointSize: theme.DefaultFontSize)
			{
				VAnchor = VAnchor.Center
			});

			var printerSelector = new PrinterSelector(theme)
			{
				Margin = new BorderDouble(left: 15)
			};
			toolBarA.AddChild(printerSelector);

			toolBarA.AddChild(new VerticalLine(50) { Margin = new BorderDouble(12, 0) });

			var redeemDesignCode = new TextButton("Redeem Design Code".Localize(), theme)
			{
				Name = "Redeem Design Code Button",
				Margin = theme.ButtonSpacing
			};
			redeemDesignCode.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					simpleTabs.RemoveTab(simpleTabs.ActiveTab);
					// Implementation already does RunOnIdle
					ApplicationController.Instance.RedeemDesignCode?.Invoke();
				});
			};
			toolBarA.AddChild(redeemDesignCode);

			var redeemShareCode = new TextButton("Enter Share Code".Localize(), theme)
			{
				Name = "Enter Share Code Button",
				Margin = theme.ButtonSpacing
			};
			redeemShareCode.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					simpleTabs.RemoveTab(simpleTabs.ActiveTab);

					// Implementation already does RunOnIdle
					ApplicationController.Instance.EnterShareCode?.Invoke();
				});
			};
			toolBarA.AddChild(redeemShareCode);

			WebClient client = new WebClient();
			client.DownloadDataCompleted += (s, e) =>
			{
				try // if we get a bad result we can get a target invocation exception. In that case just don't show anything
				{
					// scale the loaded image to the size of the target image
					byte[] raw = e.Result;
					Stream stream = new MemoryStream(raw);
					var jsonContent = new StreamReader(stream).ReadToEnd();
					var content = JsonConvert.DeserializeObject<ExplorerFeed>(jsonContent);

					// add a bunch of content
					AddControlsForContent(content);

					UiThread.RunOnIdle(() =>
					{
						// Force layout to change to get it working
						var oldMargin = this.Margin;
						this.Margin = new BorderDouble(20);
						this.Margin = oldMargin;
					});
				}
				catch
				{
				}
			};

			try
			{
				var url = "http://www.matterhackers.com/feeds/explore?sk=2lhddgi3q67xoqa53pchpeddl6w1uf";
				client.DownloadDataAsync(new Uri(url));
			}
			catch
			{
			}
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			int direction = (mouseEvent.WheelDelta > 0) ? -1 : 1;
			this.ScrollPosition += new Vector2(0, (ExploreItem.IconSize + (ExploreItem.ItemSpacing * 2)) * direction);
		}

		private void AddControlsForContent(ExplorerFeed contentList)
		{
			foreach (var content in contentList.Content)
			{
				switch (content.content_type)
				{
					case "article_group":
					case "product_group":
						topToBottom.AddChild(new ExploreSection(content, theme));
						break;
				}
			}
		}
	}

	#region json expand classes

	public class ExploreFeedContent
	{
		public string content_type;
		public List<ExplorerFeedItem> group_items;
		public string group_link;
		public string group_subtitle;
		public string group_title;
		public string icon_url;
		public string image_url;
		public string link;
		public string theme_filter;
	}

	public class ExplorerFeed
	{
		public List<ExploreFeedContent> Content;
		public string Status;
	}

	public class ExplorerFeedItem
	{
		public string author;
		public string category;
		public string date_published;
		public string description;
		public string hero;
		public string icon;
		public string link;
		public string title;
		public string url;
	}

	#endregion json expand classes
}