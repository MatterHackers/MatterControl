// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// Copyright (c) 2022, John Lewin
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System.Linq;
using Markdig.Renderers.Agg.Inlines;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class TableHeadingRow: FlowLayoutWidget
	{
		public TableHeadingRow()
		{
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;
			this.Margin = new BorderDouble(3, 4, 0, 12);
			// Likely needs to be implemented here as well
			//this.RowPadding = new BorderDouble(0, 3);
		}

		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (childToAdd is TextWidget textWidget)
			{
				// textWidget.TextColor = new Color("#036ac3");
				textWidget.Bold = true;
			}
			else if (childToAdd is TextLinkX textLink)
			{
				foreach (var childTextWidget in childToAdd.Children.OfType<TextWidget>())
				{
                    childTextWidget.Bold = true;
				}
			}

			return base.AddChild(childToAdd, indexInChildrenList);
		}
	}
}
