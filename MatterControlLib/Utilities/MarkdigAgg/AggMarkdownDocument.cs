/*
Copyright(c) 2018, Lars Brubaker, John Lewin
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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
using System.Net;
using Markdig.Renderers;
using Markdig.Renderers.Agg;
using Markdig.Syntax.Inlines;
using MatterHackers.Agg.UI;

namespace Markdig.Agg
{
	public class MarkdownDocumentLink
	{
		public Uri Uri { get; internal set; }

		public LinkInline LinkInline { get; internal set; }

		public string PageID { get; internal set; }
	}

	public class AggMarkdownDocument
	{
		private string _markDownText = null;
		private MarkdownPipeline _pipeLine = null;
		private static readonly MarkdownPipeline DefaultPipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

		public AggMarkdownDocument()
		{
		}

		public AggMarkdownDocument(Uri baseUri)
		{
			this.BaseUri = baseUri;
		}

		private string matchingText;

		public string MatchingText
		{
			get => matchingText;
			set => matchingText = value;
		}


		public Uri BaseUri { get; set; } = new Uri("https://www.matterhackers.com/");

		public List<MarkdownDocumentLink> Children { get; private set; } = new List<MarkdownDocumentLink>();

		public static AggMarkdownDocument Load(Uri uri)
		{
			var webClient = new WebClient();

			string rawText = webClient.DownloadString(uri);

			return new AggMarkdownDocument(uri)
			{
				Markdown = rawText,
			};
		}

		/// <summary>
		/// Gets or sets the Markdown to display.
		/// </summary>
		public string Markdown
		{
			get => _markDownText;
			set
			{
				if (_markDownText != value)
				{
					_markDownText = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the Markdown pipeline to use.
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

		public void Parse(GuiWidget guiWidget = null)
		{
			if (!string.IsNullOrEmpty(this.Markdown))
			{
				MarkdownPipeline pipeline;

				if (!string.IsNullOrWhiteSpace(matchingText))
				{
					var builder = new MarkdownPipelineBuilder().UseSupportedExtensions();
					builder.InlineParsers.Add(new MatchingTextParser(matchingText));

					pipeline = builder.Build();
				}
				else
				{
					pipeline = Pipeline;
				}

				var rootWidget = guiWidget ?? new GuiWidget();

				var renderer = new AggRenderer(rootWidget)
				{
					BaseUri = this.BaseUri,
					ChildLinks = new List<MarkdownDocumentLink>()
				};

				pipeline.Setup(renderer);

				var document = Markdig.Markdown.Parse(this.Markdown, pipeline);

				renderer.Render(document);

				this.Children = renderer.ChildLinks;
			}
		}
	}
}
