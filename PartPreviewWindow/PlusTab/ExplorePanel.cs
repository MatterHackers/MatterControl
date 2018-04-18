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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class ExplorePanel : ScrollableWidget
	{
		private ThemeConfig theme;
		private FlowLayoutWidget topToBottom;

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

			var lastProfileID = ProfileManager.Instance.LastProfileID;
			var lastProfile = ProfileManager.Instance[lastProfileID];
			if (lastProfile != null)
			{
				topToBottom.AddChild(
					new PrinterBar(partPreviewContent, lastProfile, theme));
			}
			else
			{
				// TODO: implement panel for case of having no printer selected
				//var explorerBar = new ExplorerBar("testing", theme);
				//topToBottom.AddChild(explorerBar);
			}

			topToBottom.AddChild(new PartsBar(partPreviewContent, theme)
			{
				Margin = new BorderDouble(30, 15)
			});
		}

		public async override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);

			try
			{
				var explorerFeed = await LoadExploreFeed();

				UiThread.RunOnIdle(() =>
				{
					// Add controls for content
					AddControlsForContent(explorerFeed);

					// Force layout to change to get it working
					var oldMargin = this.Margin;
					this.Margin = new BorderDouble(20);
					this.Margin = oldMargin;
				});
			}
			catch
			{
			}
		}

		public async Task<ExplorerFeed> LoadExploreFeed()
		{
			return await ApplicationController.LoadCacheableAsync<ExplorerFeed>(
				"explore-feed.json",
				"MatterHackers",
				async () =>
				{
					try
					{
						var client = new HttpClient();
						string json = await client.GetStringAsync("http://www.matterhackers.com/feeds/explore?sk=2lhddgi3q67xoqa53pchpeddl6w1uf");

						return JsonConvert.DeserializeObject<ExplorerFeed>(json);
					}
					catch(Exception ex)
					{
						Trace.WriteLine("Error collecting or loading feed: " + ex.Message);
					}

					return null;
				},
				Path.Combine("OEMSettings", "ExploreFeed.json"));
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
					case "banner_image":
						{
							if ((content.theme_filter == "dark" && ActiveTheme.Instance.IsDarkTheme)
								|| (content.theme_filter == "light" && !ActiveTheme.Instance.IsDarkTheme))
							{
								ImageBuffer image = new ImageBuffer(640, 480);
								ResponsiveImageWidget imageWidget = new ResponsiveImageWidget(image)
								{
									Margin = new BorderDouble(5),
								};

								if (content.link != null)
								{
									imageWidget.Cursor = Cursors.Hand;
									imageWidget.Click += (s, e) =>
									{
										ApplicationController.Instance.LaunchBrowser(content.link);
									};
								}

								imageWidget.Load += (s, e) => ApplicationController.Instance.DownloadToImageAsync(image, content.image_url, false, new BlenderPreMultBGRA());
								topToBottom.AddChild(imageWidget);
							}
						}
						break;

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