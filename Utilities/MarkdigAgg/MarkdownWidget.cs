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
using System.Net;
using Markdig.Renderers;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Agg
{
	public class MarkdownWidget : ScrollableWidget
	{
		private static readonly MarkdownPipeline DefaultPipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

		private string _markDownText = null;
		private FlowLayoutWidget contentPanel;

		private AggMarkdownDocument markdownDocument;

		public MarkdownWidget(Uri contentUri, bool scrollContent = true)
			: this(scrollContent)
		{
			markdownDocument.BaseUri = contentUri;

			this.Load(contentUri);
		}

		public MarkdownWidget(bool scrollContent = true)
			: base(scrollContent)
		{
			markdownDocument = new AggMarkdownDocument();

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

			var lastScroll = this.TopLeftOffset;
			this.ScrollPositionChanged += (s, e) =>
			{
				lastScroll = TopLeftOffset;
			};

			// make sure as the scrolling area changes height we maintain our current scroll position
			this.ScrollArea.BoundsChanged += (s, e) =>
			{
				TopLeftOffset = lastScroll;
			};

			contentPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};

			this.AddChild(contentPanel);
		}

		public void Load(Uri uri)
		{
			var webClient = new WebClient();
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

					// Empty self
					contentPanel.CloseAllChildren();

					this.Width = 10;
					this.ScrollPositionFromTop = Vector2.Zero;

					// Parse and reconstruct
					markdownDocument.Markdown = value;
					markdownDocument.Parse(contentPanel);
				}
			}
		}
	}
}
