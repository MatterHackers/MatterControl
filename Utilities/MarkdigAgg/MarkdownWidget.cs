// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using System.Net;
using Markdig.Renderers;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.VectorMath;

namespace Markdig.Agg
{
	public class MarkdownWidget : ScrollableWidget
	{
		private static readonly MarkdownPipeline DefaultPipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

		private string _markDownText = null;
		private MarkdownPipeline _pipeLine = null;
		private Uri baseUri;
		private FlowLayoutWidget contentPanel;

		public MarkdownWidget(Uri baseUri, bool scrollContent = true)
			: base(scrollContent)
		{
			this.baseUri = baseUri;

			this.HAnchor = HAnchor.Stretch;
			this.ScrollArea.HAnchor = HAnchor.Stretch;

			if (scrollContent)
			{
				this.VAnchor = VAnchor.Stretch;
				this.ScrollArea.VAnchor = VAnchor.Fit;
			}
			else
			{
				this.VAnchor = VAnchor.Fit;
				this.ScrollArea.VAnchor = VAnchor.Fit;
			}

			contentPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};

			this.AddChild(contentPanel);
		}

		public MarkdownWidget(Uri baseUri, Uri contentUri, bool scrollContent = true)
			: this(baseUri, scrollContent)
		{
			var webClient = new WebClient();
			this.Markdown = "~~Strike-through test text~~ \r\n" + webClient.DownloadString(contentUri);
		}

		/// <summary>
		/// Gets or sets the markdown to display.
		/// </summary>
		public string Markdown
		{
			get => _markDownText;
			set
			{
				if (_markDownText != value)
				{
					_markDownText = value;
					this.RefreshDocument();
					this.Width = 10;
					this.ScrollPositionFromTop = Vector2.Zero;
				}
			}
		}

		/// <summary>
		/// Gets or sets the markdown pipeline to use.
		/// </summary>
		public MarkdownPipeline Pipeline
		{
			get => _pipeLine ?? DefaultPipeline;
			set
			{
				if (_pipeLine != value)
				{
					_pipeLine = value;
				}
			}
		}

		private void RefreshDocument()
		{
			if (!string.IsNullOrEmpty(this.Markdown))
			{
				var pipeline = Pipeline;

				contentPanel.CloseAllChildren();

				// why do we check the pipeline here?
				pipeline = pipeline ?? new MarkdownPipelineBuilder().Build();

				var renderer = new AggRenderer(contentPanel)
				{
					BaseUri = baseUri
				};

				pipeline.Setup(renderer);

				var document = Markdig.Markdown.Parse(this.Markdown, pipeline);
				renderer.Render(document);
			}
		}
	}
}
