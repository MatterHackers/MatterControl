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

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class ExploreItem : FlowLayoutWidget
	{
		private ExplorerFeedItem item;

		public ExploreItem(ExplorerFeedItem item)
		{
			int spacing = 10;

			this.HAnchor = HAnchor.Absolute;
			this.Width = 250 * GuiWidget.DeviceScale;
			//this.Border = spacing;
			this.Padding = spacing;
			this.item = item;

			if (item.icon != null)
			{
				ImageBuffer image = new ImageBuffer((int)(64 * GuiWidget.DeviceScale), (int)(64 * GuiWidget.DeviceScale));

				var imageWidget = new ImageWidget(image)
				{
					Selectable = false,
					VAnchor = VAnchor.Top,
					Margin = new BorderDouble(right: spacing)
				};

				imageWidget.Load += (s, e) => ApplicationController.Instance.DownloadToImageAsync(image, item.icon, true, new BlenderPreMultBGRA());
				this.AddChild(imageWidget);
			}

			var wrappedText = new WrappedTextWidget(item.title, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Selectable = false,
				VAnchor = VAnchor.Center | VAnchor.Fit,
				Margin = 3
			};
			this.AddChild(wrappedText);
			wrappedText.Load += (s, e) =>
			{
				wrappedText.VAnchor = VAnchor.Top | VAnchor.Fit;
			};

			this.Cursor = Cursors.Hand;
		}

		public override void OnClick(MouseEventArgs mouseEvent)
		{
			if (item.url != null)
			{
				ApplicationController.Instance.LaunchBrowser("http://www.matterhackers.com/" + item.url);
			}
			else if (item.link != null)
			{
				ApplicationController.Instance.LaunchBrowser(item.link);
			}

			base.OnClick(mouseEvent);
		}
	}
}