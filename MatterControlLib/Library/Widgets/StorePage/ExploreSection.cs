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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class ExploreSection : FlowLayoutWidget
	{
		private List<ExploreItem> allIconViews = new List<ExploreItem>();
		private FeedSectionData content;
		private ThemeConfig theme;
		private TextButton moreButton;
		int maxStuff = 7;

		public ExploreSection(FeedSectionData content, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			VAnchor = VAnchor.Fit | VAnchor.Top;
			this.content = content;
			this.theme = theme;

			foreach (var item in content.group_items)
			{
				allIconViews.Add(new ExploreItem(item, theme)
				{
					BackgroundColor = theme.MinimalShade,
					VAnchor = VAnchor.Fit,
				});
			}

			AddContent();
		}

		public void AddContent()
		{
			int leftRightMargin = 5;
			int topBottomMargin = 5;

			// Reflow Children
			foreach (var iconView in allIconViews)
			{
				if (this.Children.Contains(iconView))
				{
					this.RemoveChild(iconView);
				}
			}
			this.CloseAllChildren();

			if (content.group_title != null)
			{
				this.AddChild(new TextWidget(content.group_title, pointSize: theme.H1PointSize, textColor: theme.TextColor, bold: true)
				{
					HAnchor = HAnchor.Left,
					Margin = new BorderDouble(leftRightMargin, 5)
				});
			}

			int i = 0;
			foreach (var iconView in allIconViews)
			{
				if (i < maxStuff)
				{
					iconView.ClearRemovedFlag();
					iconView.Margin = new BorderDouble(leftRightMargin, topBottomMargin);
					this.AddChild(iconView);
				}
				i++;
			}

			if (content.group_items.Count > maxStuff)
			{
				moreButton = new TextButton("More".Localize() + "...", theme)
				{
					VAnchor = VAnchor.Absolute,
					HAnchor = HAnchor.Right,
					BackgroundColor = theme.MinimalShade,
					Margin = new BorderDouble(right: leftRightMargin)
				};
				moreButton.Click += (s, e1) =>
				{
					// if we can go out to the site than do that
					if (content.group_link != null)
					{
						ApplicationController.Instance.LaunchBrowser(content.group_link);
					}
					else // show more items in the list
					{
						var scroll = this.Parents<ScrollableWidget>().FirstOrDefault();
						var position = scroll.ScrollPositionFromTop;

						maxStuff += 6;
						AddContent();

						scroll.ScrollPositionFromTop = position;
					}
				};
				this.AddChild(moreButton);
			}
		}
	}
}