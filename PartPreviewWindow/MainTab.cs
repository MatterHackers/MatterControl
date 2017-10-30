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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class MainTab : ThreeViewTab
	{
		private class TabPill : FlowLayoutWidget
		{
			private TextWidget label;

			public TabPill(string tabTitle, RGBA_Bytes textColor, string imageUrl = null)
			{
				var imageWidget = new ImageWidget(new ImageBuffer(16, 16))
				{
					Margin = new BorderDouble(right: 6),
					VAnchor = VAnchor.Center
				};
				this.AddChild(imageWidget);

				label = new TextWidget(tabTitle)
				{
					TextColor = textColor,
					VAnchor = VAnchor.Center
				};
				this.AddChild(label);

				if (!string.IsNullOrEmpty(imageUrl))
				{
					// Attempt to load image
					try
					{
						// TODO: Must use caching
						ApplicationController.Instance.DownloadToImageAsync(imageWidget.Image, imageUrl, false);
					}
					catch { }
				}
			}

			public RGBA_Bytes TextColor
			{
				get =>  label.TextColor;
				set => label.TextColor = value;
			}

			public override string Text
			{
				get => label.Text;
				set => label.Text = value;
			}
		}

		public MainTab(string tabTitle, string tabName, TabPage tabPage, string tabImageUrl = null)
		: this(
			new TabPill(tabTitle, new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 140), tabImageUrl),
			new TabPill(tabTitle, ActiveTheme.Instance.PrimaryTextColor, tabImageUrl),
			new TabPill(tabTitle, ActiveTheme.Instance.PrimaryTextColor, tabImageUrl),
			tabName,
			tabPage)
		{
		}

		public MainTab(GuiWidget normalWidget, GuiWidget hoverWidget, GuiWidget pressedWidget, string tabName, TabPage tabPage)
			: base(tabName, normalWidget, hoverWidget, pressedWidget, tabPage)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit | VAnchor.Bottom;
		}

		public int BorderWidth { get; set; } = 1;
		public int borderRadius { get; set; } = 4;

		private RGBA_Bytes activeTabColor =  ApplicationController.Instance.Theme.SlightShade;
		private RGBA_Bytes inactiveTabColor = ApplicationController.Instance.Theme.PrimaryTabFillColor;

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble borderRectangle = LocalBounds;
			borderRectangle.ExpandToInclude(new Vector2(0, -15));

			if (BorderWidth > 0)
			{
				var r = new RoundedRect(borderRectangle, this.borderRadius);
				r.normalize_radius();

				graphics2D.Render(
					r,
					selectedWidget.Visible ? activeTabColor : inactiveTabColor);
			}

			base.OnDraw(graphics2D);
		}
	}
}
