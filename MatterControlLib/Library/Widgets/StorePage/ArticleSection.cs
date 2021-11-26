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
using System.Globalization;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
    public class ArticleSection : FlowLeftRightWithWrapping
	{
		private List<ArticleItem> allIconViews = new List<ArticleItem>();
		private FeedSectionData content;
		private ThemeConfig theme;
		int maxStuff = 20;

		public ArticleSection(FeedSectionData content, ThemeConfig theme)
		{
			Proportional = true;
			VAnchor = VAnchor.Fit | VAnchor.Top;
			this.content = content;
			this.theme = theme;

			var cultureInfo = new CultureInfo("en-US");

			foreach (var item in content.group_items.OrderByDescending(i => DateTime.Parse(i.date_published, cultureInfo)))
			{
				allIconViews.Add(new ArticleItem(item, theme)
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

			// Remove items and Children (this happens if the feed is different than the inital cach after being retrieved)
			foreach (var iconView in allIconViews)
			{
				if (this.Children.Contains(iconView))
				{
					this.RemoveChild(iconView);
				}
			}
			this.CloseChildren();

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
				var moreButton = new TextButton("More".Localize() + "...", theme)
				{
					BackgroundColor = theme.MinimalShade,
					Margin = new BorderDouble(right: leftRightMargin),
				};
				moreButton.Click += (s, e1) =>
				{
					// if we can go out to the site than do that
					if (content.group_link != null)
					{
						ApplicationController.LaunchBrowser(content.group_link);
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