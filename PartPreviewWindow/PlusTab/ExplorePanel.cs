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
using System.Net.Http;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class ExplorePanel : FlowLayoutWidget
	{
		string sk;
		string staticFile;
		private ThemeConfig theme;

		public ExplorePanel(ThemeConfig theme, string sk, string staticFile)
			: base(FlowDirection.TopToBottom)
		{
			this.sk = sk;
			this.staticFile = staticFile;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.MinimumSize = new Vector2(0, 1);

			this.theme = theme;
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
				staticFile,
				"MatterHackers",
				async () =>
				{
					try
					{
						var client = new HttpClient();
						string json = await client.GetStringAsync($"http://www.matterhackers.com/feeds/{sk}");

						return JsonConvert.DeserializeObject<ExplorerFeed>(json);
					}
					catch(Exception ex)
					{
						Trace.WriteLine("Error collecting or loading feed: " + ex.Message);
					}

					return null;
				},
				Path.Combine("OEMSettings", staticFile));
		}

		private void AddControlsForContent(ExplorerFeed contentList)
		{
			foreach (var content in contentList.Content)
			{
				AddContentItem(content);
			}
		}

		private void AddContentItem(ExploreFeedContent content)
		{
			switch (content.content_type)
			{
				case "headline":
					{
						// use the Golden Ratio to calculate an atractive size relative to the banner
						ImageBuffer image = new ImageBuffer(1520, (int)(170 / 1.618));
						ResponsiveImageWidget imageWidget = new ResponsiveImageWidget(image)
						{
							Margin = new BorderDouble(5),
						};

						var graphics2D = image.NewGraphics2D();

						// make text 105 (if possible)
						graphics2D.Clear(theme.Colors.PrimaryAccentColor);

						// use the Golden Ratio to calculate an atractive size for the text relative to the text banner
						var pixelsPerPoint = 96.0 / 72.0;
						var goalPointSize = image.Height / pixelsPerPoint / 1.618;

						var printer = new TypeFacePrinter(content.text, goalPointSize);

						graphics2D.DrawString(content.text, image.Width/2, image.Height/2 + printer.TypeFaceStyle.EmSizeInPixels / 2, goalPointSize, 
							Agg.Font.Justification.Center, Baseline.BoundsTop,
							Color.White);

						if (content.link != null)
						{
							imageWidget.Cursor = Cursors.Hand;
							imageWidget.Click += (s, e) =>
							{
								ApplicationController.Instance.LaunchBrowser(content.link);
							};
						}

						this.AddChild(imageWidget);
					}
					break;

				case "banner_rotate":
					// TODO: make this make a carousel rather than add the first item and rotate between all the items
					var rand = new Random();
					AddContentItem(content.banner_list[rand.Next(content.banner_list.Count)]);
					break;

				case "banner_image":
					{
						// Our banners seem to end with something like "=w1520-h170"
						// if present use that to get the right width and height
						int expectedWidth = 1520;
						GCodeFile.GetFirstNumberAfter("=w", content.image_url, ref expectedWidth);
						int expectedHeight = 170;
						GCodeFile.GetFirstNumberAfter("-h", content.image_url, ref expectedHeight);
						if ((content.theme_filter == "dark" && ActiveTheme.Instance.IsDarkTheme)
							|| (content.theme_filter == "light" && !ActiveTheme.Instance.IsDarkTheme)
							|| (content.theme_filter == "all"))
						{
							ImageBuffer image = new ImageBuffer(expectedWidth, expectedHeight);
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
							this.AddChild(imageWidget);
						}
					}
					break;

				case "article_group":
				case "product_group":
					this.AddChild(new ExploreSection(content, theme));
					break;
			}
		}
	}

	#region json expand classes

	public class ExploreFeedContent
	{
		public string content_type;
		public List<ExplorerFeedItem> group_items;
		public List<ExploreFeedContent> banner_list;
		public string group_link;
		public string group_subtitle;
		public string group_title;
		public string icon_url;
		public string image_url;
		public string link;
		public string text;
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