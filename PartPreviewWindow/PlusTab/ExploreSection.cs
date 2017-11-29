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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class ExploreSection : FlowLayoutWidget
	{
		private List<ExploreItem> allIconViews = new List<ExploreItem>();
		private int cellIndex = 0;
		private int columnCount = 1;
		private ExploreFeedContent content;
		private int lastReflowWidth = -1;
		private int leftRightMargin;
		private FlowLayoutWidget rowButtonContainer = null;

		public ExploreSection(ExploreFeedContent content, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.content = content;
			this.HAnchor = HAnchor.Stretch;

			foreach (var item in content.group_items)
			{
				allIconViews.Add(new ExploreItem(item)
				{
					BackgroundColor = theme.SlightShade,
					VAnchor = VAnchor.Top | VAnchor.Fit,
				});
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			int topBottomMargin = 5;

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
					}
					this.CloseAllChildren();

					if (content.group_title != null)
					{
						this.AddChild(new TextWidget(content.group_title, pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor)
						{
							HAnchor = HAnchor.Left,
							Margin = 5
						});
					}

					foreach (var iconView in allIconViews)
					{
						iconView.ClearRemovedFlag();
						iconView.Margin = new BorderDouble(leftRightMargin, topBottomMargin);
						AddColumnAndChild(iconView);
					}
				}
				else
				{
					foreach (var iconView in allIconViews)
					{
						iconView.Margin = new BorderDouble(leftRightMargin, topBottomMargin);
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
			int itemWidth = (int)allIconViews[0].Width;

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
			//
			// TODO: Replace short term hack with new solution
			//leftRightMargin = (int)(remainingSpace > 0 ? spacePerColumn / 2 : 0);
			leftRightMargin = Math.Max(8, (int)(remainingSpace > 0 ? spacePerColumn / 2 : 0));

			// put in padding to get the "other" side of the outside icons
			this.Padding = new BorderDouble(leftRightMargin, 0);

			return newColumnCount;
		}
	}
}