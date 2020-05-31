// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Markdig.Agg;
using Markdig.Syntax.Inlines;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.VectorMath;

namespace Markdig.Renderers.Agg.Inlines
{
	public class TextLinkX : FlowLayoutWidget
	{
		private LinkInline linkInline;
		private string url;
		private AggRenderer aggRenderer;

		public TextLinkX(AggRenderer renderer, string url, LinkInline linkInline)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.Cursor = Cursors.Hand;
			this.linkInline = linkInline;
			this.url = url;
			this.aggRenderer = renderer;
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (linkInline.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			{
				ApplicationController.Instance.LaunchBrowser(linkInline.Url);
			}
			else
			{
				if (aggRenderer.RootWidget.Parents<MarkdownWidget>().FirstOrDefault() is MarkdownWidget markdownWidget)
				{
					markdownWidget.LoadUri(new Uri(url));
				}
			}

			base.OnClick(mouseEvent);
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (childToAdd is TextWidget textWidget)
			{
				// Underline TextWidget children of TextLink nodes
				textWidget.Underline = true;
			}

			// Allow link parent to own mouse events
			childToAdd.Selectable = false;

			base.AddChild(childToAdd, indexInChildrenList);
		}
	}

	public class ImageLinkSimpleX : FlowLayoutWidget
	{
		private static ImageBuffer icon = AggContext.StaticData.LoadIcon("internet.png", 16, 16);

		public ImageLinkSimpleX(AggRenderer renderer, string imageUrl, string linkUrl = null)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Selectable = false;

			this.ImageUrl = imageUrl;
			this.LinkUrl = linkUrl;

			this.aggRenderer = renderer;

			if (linkUrl != null)
			{
				this.Selectable = true;
			}

			imageSequence = new ImageSequence(icon);
			// var sequenceWidget = new ImageSequenceWidget(imageSequence);
			var sequenceWidget = new ResponsiveImageSequenceWidget(imageSequence)
			{
				Cursor = Cursors.Hand,
			};

			sequenceWidget.Click += SequenceWidget_Click;

			this.AddChild(sequenceWidget);
		}

		private void SequenceWidget_Click(object sender, MouseEventArgs e)
		{
			if (this.LinkUrl != null)
			{
				if (LinkUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
				{
					ApplicationController.Instance.LaunchBrowser(LinkUrl);
				}
				else
				{
					if (aggRenderer.RootWidget.Parents<MarkdownWidget>().FirstOrDefault() is MarkdownWidget markdownWidget)
					{
						markdownWidget.LoadUri(new Uri(LinkUrl));
					}
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (!hasBeenLoaded)
			{
				if (ImageUrl.StartsWith("http"))
				{
					WebCache.RetrieveImageSquenceAsync(imageSequence, ImageUrl, () =>
					{
						this.MinStretchOrFitHorizontal(20, imageSequence.Width);
						UiThread.RunOnIdle(() =>
						{
							if (aggRenderer.RootWidget.Parents<MarkdownWidget>().FirstOrDefault() is MarkdownWidget markdownWidget)
							{
								markdownWidget.Width = markdownWidget.Width + 1;
							}
						});
					});
				}

				hasBeenLoaded = true;
			}

			base.OnDraw(graphics2D);
		}


		/// <summary>
		/// Sets this control to Stretch and all direct parent FlowLayoutWidgets to Stretch, it then ensures
		/// this and all direct parent FlowLayouts have a max width of the contents of this.
		/// </summary>
		/// <param name="absoluteMinWidth">The minimum size will be set to the larger of the existing minimum size or this value.</param>
		/// <param name="absoluteMaxWidth">The maximum size will be set to this value.</param>
		private void MinStretchOrFitHorizontal(double absoluteMinWidth, double absoluteMaxWidth)
		{
			this.HAnchor = HAnchor.Stretch;

			MinimumSize = new Vector2(Math.Max(absoluteMinWidth * DeviceScale, MinimumSize.X), MinimumSize.Y);
			MaximumSize = new Vector2(absoluteMaxWidth, MaximumSize.Y);
		}

		public string ImageUrl { get; }

		private string LinkUrl { get; }

		private AggRenderer aggRenderer;
		private ImageSequence imageSequence;
		private bool hasBeenLoaded;
	}

	public class ImageLinkAdvancedX : FlowLayoutWidget
	{
		private static HttpClient client = new HttpClient();

		private static ImageBuffer icon = AggContext.StaticData.LoadIcon("internet.png", 16, 16);

		public string Url { get; }

		public ImageLinkAdvancedX(string url)
		{
			HAnchor = HAnchor.Fit;
			VAnchor = VAnchor.Fit;
			this.Url = url;

			var imageBuffer = new ImageBuffer(icon);
			var imageWidget = new ImageWidget(imageBuffer);

			this.AddChild(imageWidget);

			try
			{
				if (url.StartsWith("http"))
				{
					client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ContinueWith(task =>
					{
						var response = task.Result;

						if (response.IsSuccessStatusCode)
						{
							response.Content.ReadAsStreamAsync().ContinueWith(streamTask =>
							{
								// response.Headers.TryGetValues("", s[""] == "" ||
								if (string.Equals(Path.GetExtension(url), ".svg", StringComparison.OrdinalIgnoreCase))
								{
									// Load svg into SvgWidget, swap for ImageWidget
									try
									{
										var svgWidget = new SvgWidget(streamTask.Result, 1)
										{
											Border = 1,
											BorderColor = Color.YellowGreen
										};

										this.ReplaceChild(imageWidget, svgWidget);
									}
									catch (Exception svgEx)
									{
										Debug.WriteLine("Error loading svg: {0} :: {1}", url, svgEx.Message);
									}
								}
								else
								{
									// Load img
									if (!AggContext.ImageIO.LoadImageData(streamTask.Result, imageBuffer))
									{
										Debug.WriteLine("Error loading image: " + url);
									}
								}
							});
						}
					});
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
	}

	/// <summary>
	/// A Agg renderer for a <see cref="LinkInline"/>.
	/// </summary>
	/// <seealso cref="Markdig.Renderers.Agg.AggObjectRenderer{Markdig.Syntax.Inlines.LinkInline}" />
	public class AggLinkInlineRenderer : AggObjectRenderer<LinkInline>
	{
		/// <inheritdoc/>
		protected override void Write(AggRenderer renderer, LinkInline link)
		{
			var url = link.GetDynamicUrl != null ? link.GetDynamicUrl() ?? link.Url : link.Url;

			if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
			{
				url = "#";
			}

			if (!url.StartsWith("http"))
			{
				var pageID = url;

				url = new Uri(renderer.BaseUri, url).AbsoluteUri;

				renderer.ChildLinks.Add(new MarkdownDocumentLink()
				{
					Uri = new Uri(url),
					LinkInline = link,
					PageID = pageID
				});
			}

			if (link.IsImage)
			{
				renderer.WriteInline(new ImageLinkSimpleX(renderer, url));
			}
			else
			{
				if (link.FirstChild is LinkInline linkInLine
					&& linkInLine.IsImage)
				{
					renderer.WriteInline(new ImageLinkSimpleX(renderer, linkInLine.Url, url));
					renderer.Pop();
				}
				else
				{
					renderer.Push(new TextLinkX(renderer, url, link));
					renderer.WriteChildren(link);
					renderer.Pop();
				}
			}
		}
	}
}
