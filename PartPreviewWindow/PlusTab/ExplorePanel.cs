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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class ExplorePanel : ScrollableWidget
	{
		private ThemeConfig theme;

		public ExplorePanel(ThemeConfig theme)
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

			WebClient client = new WebClient();
			client.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs e) =>
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

		private void AddControlsForContent(ExplorerFeed contentList)
		{
			var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};
			this.AddChild(topToBottom);
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
								ImageWidget imageWidget = new ImageWidget(image)
								{
									Margin = new BorderDouble(5),
									HAnchor = HAnchor.Center,
								};

								if (content.link != null)
								{
									imageWidget.Cursor = Cursors.Hand;
									imageWidget.Click += (s, e) =>
									{
										MatterControlApplication.Instance.LaunchBrowser(content.link);
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