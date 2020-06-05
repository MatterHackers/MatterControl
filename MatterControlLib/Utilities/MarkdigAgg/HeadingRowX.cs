// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Renderers.Agg.Inlines;
using Markdig.Syntax;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class HeadingRowX : FlowLeftRightWithWrapping
	{
		public HeadingRowX()
		{
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;
			this.Margin = new BorderDouble(3, 4, 0, 12);
			this.RowPadding = new BorderDouble(0, 3);
		}

		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (childToAdd is TextWidget textWidget)
			{
				// textWidget.TextColor = new Color("#036ac3");
				textWidget.PointSize = 14;
			}
			else if (childToAdd is TextLinkX textLink)
			{
				foreach (var child in childToAdd.Children)
				{
					if (child is TextWidget childTextWidget)
					{
						childTextWidget.PointSize = 14;
					}
				}
			}

			return base.AddChild(childToAdd, indexInChildrenList);
		}
	}
}
