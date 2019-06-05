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
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class ExplorePanel : FlowLayoutWidget
	{
		string relativeUrl;
		private ThemeConfig theme;
		FlowLeftRightWithWrapping currentContentContainer;
		private object locker = new object();

		public ExplorePanel(ThemeConfig theme, string relativeUrl)
			: base(FlowDirection.TopToBottom)
		{
			this.relativeUrl = relativeUrl;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.MinimumSize = new Vector2(0, 1);
			this.Margin = new BorderDouble(30 - 11, 0);

			this.theme = theme;
		}

		public override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);

			FeedData explorerFeed = null;

			// Force layout to change to get it working
			var oldMargin = this.Margin;
			this.Margin = new BorderDouble(20);
			this.Margin = oldMargin;

			Task.Run(() =>
			{
				// Construct directly from cache
				WebCache.RetrieveText($"https://www.matterhackers.com/feeds/{relativeUrl}", (newData) =>
				{
					lock (locker)
					{
						explorerFeed = JsonConvert.DeserializeObject<FeedData>(newData);

						if (explorerFeed != null)
						{
							UiThread.RunOnIdle(() =>
							{
								this.CloseAllChildren();

									// Add controls for content
								foreach (var content in explorerFeed.Content)
								{
									AddContentItem(content);
								}

								// Force layout to change to get it working
								this.Margin = new BorderDouble(20);
								this.Margin = oldMargin;
							});
						}
					}
				});
			});
		}


		private void AddContentItem(FeedSectionData content)
		{
			switch (content.content_type)
			{
				case "headline":
					{
						break;

						// use the Golden Ratio to calculate an attractive size relative to the banner
						var image = new ImageBuffer(1520, (int)(170 / 1.618));
						var imageWidget = new ResponsiveImageWidget(image)
						{
							Margin = new BorderDouble(5),
							Cursor = Cursors.Hand
						};

						var graphics2D = image.NewGraphics2D();
						image.SetRecieveBlender(new BlenderPreMultBGRA());
						graphics2D.Clear(theme.AccentMimimalOverlay);

						// use the Golden Ratio to calculate an attractive size for the text relative to the text banner
						var pixelsPerPoint = 96.0 / 72.0;
						var goalPointSize = image.Height / pixelsPerPoint / 1.618;

						var printer = new TypeFacePrinter(content.text, goalPointSize);

						graphics2D.DrawString(content.text, image.Width/2, image.Height/2 + printer.TypeFaceStyle.EmSizeInPixels / 2, goalPointSize,
							Justification.Center, Baseline.BoundsTop,
							theme.TextColor);

						if (content.link != null)
						{
							imageWidget.Cursor = Cursors.Hand;
							imageWidget.Click += (s, e) =>
							{
								if (e.Button == MouseButtons.Left)
								{
									ApplicationController.Instance.LaunchBrowser(content.link);
								}
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
						if ((content.theme_filter == "dark" && theme.IsDarkTheme)
							|| (content.theme_filter == "light" && !theme.IsDarkTheme)
							|| (content.theme_filter == "all"))
						{
							var image = new ImageBuffer(expectedWidth, expectedHeight);
							var imageWidget = new ResponsiveImageWidget(image)
							{
								Margin = new BorderDouble(5),
								Cursor = Cursors.Hand
							};

							if (content.link != null)
							{
								imageWidget.Cursor = Cursors.Hand;
								imageWidget.Click += (s, e) =>
								{
									if (e.Button == MouseButtons.Left)
									{
										ApplicationController.Instance.LaunchBrowser(content.link);
									}
								};
							}

							imageWidget.Load += (s, e) => WebCache.RetrieveImageAsync(image, content.image_url, false, new BlenderPreMultBGRA());
							this.AddChild(imageWidget);
						}
					}
					break;

				case "article_group":
				case "product_group":
					if(currentContentContainer == null)
					{
						currentContentContainer = new FlowLeftRightWithWrapping();
						this.AddChild(currentContentContainer);
					}
					currentContentContainer.AddChild(new ExploreSection(content, theme));
					break;
			}
		}
	}
}