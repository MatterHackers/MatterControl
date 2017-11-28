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

	public class ExploreItem : FlowLayoutWidget
	{
		public ExploreItem(ExplorerFeedItem item)
		{
			var content = new FlowLayoutWidget()
			{
				Border = new BorderDouble(2),
				BorderColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Absolute,
				Width = 220 * GuiWidget.DeviceScale,
				Margin = new BorderDouble(5),
			};
			this.AddChild(content);

			if (item.icon != null)
			{
				ImageBuffer image = new ImageBuffer((int)(64 * GuiWidget.DeviceScale), (int)(64 * GuiWidget.DeviceScale));
				ImageWidget imageWidget = new ImageWidget(image)
				{
					Selectable = false,
					VAnchor = VAnchor.Top,
					Margin = new BorderDouble(3)
				};

				imageWidget.Load += (s, e) => ApplicationController.Instance.DownloadToImageAsync(image, item.icon, true, new BlenderPreMultBGRA());
				content.AddChild(imageWidget);
			}

			var wrappedText = new WrappedTextWidget(item.title)
			{
				Selectable = false,
				VAnchor = VAnchor.Center | VAnchor.Fit,
				Margin = new BorderDouble(3)
			};
			content.AddChild(wrappedText);
			wrappedText.Load += (s, e) =>
			{
				wrappedText.VAnchor = VAnchor.Top | VAnchor.Fit;
			};

			if (item.url != null)
			{
				content.Cursor = Cursors.Hand;
				content.Click += (s, e) =>
				{
					MatterControlApplication.Instance.LaunchBrowser("http://www.matterhackers.com/" + item.url);
				};
			}
			else if(item.reference != null)
			{
				content.Cursor = Cursors.Hand;
				content.Click += (s, e) =>
				{
					MatterControlApplication.Instance.LaunchBrowser(item.reference);
				};
			}
		}
	}

	public class ExplorePanel : ScrollableWidget
	{
		public ExplorePanel()
		{
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Stretch;
			BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;

			this.MinimumSize = new Vector2(0, 200);
			this.AnchorAll();
			this.AutoScroll = true;
			this.ScrollArea.Padding = new BorderDouble(3);
			this.ScrollArea.HAnchor = HAnchor.Stretch;

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
						topToBottom.AddChild(new ExploreSection(content));
						break;
				}
			}
		}
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
		public string title;
		public string url;
		public string reference;
	}

	public class ExploreSection : FlowLayoutWidget
	{
		private List<ExploreItem> allIconViews = new List<ExploreItem>();
		private int cellIndex = 0;
		private int columnCount = 1;
		private ExploreFeedContent content;
		private int lastReflowWidth = -1;
		private int leftRightMargin;
		private FlowLayoutWidget rowButtonContainer = null;

		public ExploreSection(ExploreFeedContent content)
			: base(FlowDirection.TopToBottom)
		{
			this.content = content;
			this.HAnchor = HAnchor.Stretch;

			foreach (var item in content.group_items)
			{
				allIconViews.Add(new ExploreItem(item));
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			int currentWidth = (int)this.Size.X;
			if (lastReflowWidth != currentWidth)
			{
				lastReflowWidth = currentWidth;

				int newColumnCount = RecomputeFlowValues();
				if (newColumnCount != columnCount)
				{
					columnCount = newColumnCount;

					// Reflow Children
					foreach (var iconView in allIconViews)
					{
						iconView.Parent?.RemoveChild(iconView);
						iconView.Margin = new BorderDouble(leftRightMargin, 0);
					}

					this.CloseAllChildren();

					if (content.group_title != null)
					{
						this.AddChild(new TextWidget(content.group_title, pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor)
						{
							HAnchor = HAnchor.Left,
							Margin = new BorderDouble(5)
						});
					}

					foreach (var iconView in allIconViews)
					{
						iconView.ClearRemovedFlag();
						AddColumnAndChild(iconView);
					}
				}
				else
				{
					foreach (var iconView in allIconViews)
					{
						iconView.Margin = new BorderDouble(leftRightMargin, 0);
					}
				}
			}

			base.OnBoundsChanged(e);
		}

		private void AddColumnAndChild(ExploreItem iconView)
		{
			if (rowButtonContainer == null)
			{
				rowButtonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
				{
					HAnchor = HAnchor.Stretch,
					Padding = 0
				};
				this.AddChild(rowButtonContainer);
			}

			rowButtonContainer.AddChild(iconView);

			if (cellIndex++ >= columnCount - 1)
			{
				rowButtonContainer = null;
				cellIndex = 0;
			}
		}

		private int RecomputeFlowValues()
		{
			int padding = 4;
			int itemWidth = (int)allIconViews[0].Width + (padding * 2);

			int newColumnCount = (int)Math.Floor(this.LocalBounds.Width / itemWidth);
			int remainingSpace = (int)this.LocalBounds.Width - columnCount * itemWidth;

			// Reset position before reflow
			cellIndex = 0;
			rowButtonContainer = null;

			// There should always be at least one visible column
			if (newColumnCount < 1)
			{
				newColumnCount = 1;
			}

			// Only center items if extra space exists

			// we find the space we want between each column and the sides
			double spacePerColumn = (remainingSpace > 0) ? remainingSpace / (newColumnCount + 1) : 0;

			// set the margin to be 1/2 the space (it will happen on each side of each icon)
			leftRightMargin = (int)(remainingSpace > 0 ? spacePerColumn / 2 : 0);

			// put in padding to get the "other" side of the outside icons
			this.Padding = new BorderDouble(leftRightMargin, 0);

			return newColumnCount;
		}
	}
}