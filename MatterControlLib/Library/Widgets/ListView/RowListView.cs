/*
Copyright (c) 2017, John Lewin
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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class RowListView : FlowLayoutWidget, IListContentView
	{
		private ThemeConfig theme;

		public int ThumbWidth { get; } = 50;
		public int ThumbHeight { get; } = 50;

		public RowListView(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
		}

		public ListViewItemBase AddItem(ListViewItem item)
		{
			var detailsView = new RowViewItem(item, this.ThumbWidth, this.ThumbHeight, theme);
			this.AddChild(detailsView);

			return detailsView;
		}

		public void ClearItems()
		{
		}

		public void BeginReload()
		{
		}

		public void EndReload()
		{
		}
	}

	public class RowViewItem : ListViewItemBase
	{
		public RowViewItem(ListViewItem listViewItem, int thumbWidth, int thumbHeight, ThemeConfig theme)
			: base(listViewItem, thumbWidth, thumbHeight, theme)
		{
			// Set Display Attributes
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;
			this.Padding = new BorderDouble(0);
			this.Margin = new BorderDouble(6, 0, 6, 6);

			var row = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch
			};

			row.AddChild(imageWidget = new ImageWidget(thumbWidth, thumbHeight)
			{
				Name = "List Item Thumbnail",
			});

			row.AddChild(new TextWidget(listViewItem.Model.Name, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(10, 0)
			});

			this.AddChild(row);
		}

		private bool isHoverItem = false;
		public override bool IsHoverItem
		{
			get { return isHoverItem; }
			set
			{
				if (this.isHoverItem != value)
				{
					this.isHoverItem = value;

					UpdateColors();
				}
			}
		}

		public override Color BackgroundColor
		{
			get => this.IsSelected ? theme.AccentMimimalOverlay : theme.ThumbnailBackground;
			set { }
		}

		protected override async void UpdateHoverState()
		{
			if (!mouseInBounds)
			{
				IsHoverItem = false;
				return;
			}

			// Hover only occurs after mouse is in bounds for a given period of time
			await Task.Delay(500);

			if (!mouseInBounds)
			{
				IsHoverItem = false;
				return;
			}

			switch (UnderMouseState)
			{
				case UnderMouseState.NotUnderMouse:
					IsHoverItem = false;
					break;

				case UnderMouseState.FirstUnderMouse:
					IsHoverItem = true;
					break;

				case UnderMouseState.UnderMouseNotFirst:
					IsHoverItem = ContainsFirstUnderMouseRecursive();
					break;
			}
		}
	}
}