// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using System.Net;
using Markdig.Renderers;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;

namespace Markdig.Wpf
{
	public class MarkdownPage : DialogPage
	{
		private static readonly MarkdownPipeline DefaultPipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

		public MarkdownPipeline MarkdownPipeline { get; set; } = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

		private string _markDownText = null;
		private MarkdownPipeline _pipeLine = null;
		private Uri uri;
		private FlowLayoutWidget contentPanel;

		public MarkdownPage()
		{
			this.WindowTitle = this.HeaderText = "Markdown Tests";

			uri = new Uri("https://raw.githubusercontent.com/lunet-io/markdig/master/readme.md");

			var webClient = new WebClient();

			var scrollableWidget = new ScrollableWidget(true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			scrollableWidget.ScrollArea.HAnchor = HAnchor.Stretch;
			scrollableWidget.ScrollArea.VAnchor = VAnchor.Fit;
			contentRow.AddChild(scrollableWidget);

			contentPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};

			scrollableWidget.AddChild(contentPanel);

			this.Markdown = webClient.DownloadString(uri);
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

				var pipeline = Pipeline ?? DefaultPipeline;

				contentPanel.CloseAllChildren();

				pipeline = pipeline ?? new MarkdownPipelineBuilder().Build();

				var renderer = new AggRenderer(contentPanel)
				{
					BaseUri = uri
				};

				pipeline.Setup(renderer);

				var document = Markdig.Markdown.Parse(this.Markdown, pipeline);
				renderer.Render(document);
			}
		}
	}
}
