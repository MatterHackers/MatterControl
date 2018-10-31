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
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class StoreTabPage : ScrollableWidget
	{
		public StoreTabPage(ThemeConfig theme)
		{
			this.AutoScroll = true;
			this.ScrollArea.Padding = new BorderDouble(3);
			this.ScrollArea.HAnchor = HAnchor.Stretch;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.MinimumSize = new Vector2(0, 200);
			this.BackgroundColor = theme.TabBodyBackground;

			this.Name = "StoreTab";

			var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};
			this.AddChild(topToBottom);

			if (OemSettings.Instance.ShowShopButton)
			{
				topToBottom.AddChild(new ExplorePanel(theme, "banners?sk=ii2gffs6e89c2cdd9er21v", "BannerFeed.json"));
			}

			if (OemSettings.Instance.ShowShopButton)
			{
				// actual feed
				topToBottom.AddChild(new ExplorePanel(theme, "explore?sk=2lhddgi3q67xoqa53pchpeddl6w1uf", "ExploreFeed.json"));
			}
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.WheelDelta != 0)
			{
				int direction = (mouseEvent.WheelDelta > 0) ? -1 : 1;
				this.ScrollPosition += new Vector2(0, (ExploreItem.IconSize + (ExploreItem.ItemSpacing * 2)) * direction);
				mouseEvent.WheelDelta = 0;
			}
		}
	}
}
