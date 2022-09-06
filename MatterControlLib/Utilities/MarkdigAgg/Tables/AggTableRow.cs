// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// Copyright (c) 2022, John Lewin
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Markdig.Renderers.Agg.Inlines;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class AggTableRow : FlowLayoutWidget
	{
		public AggTableRow()
		{
			this.Margin = new BorderDouble(10, 4);
			this.VAnchor = VAnchor.Absolute;
			this.Height = 25;
		}

		public bool IsHeadingRow { get; set; }

		public List<AggTableCell> Cells { get; } = new List<AggTableCell>();
		public double RowHeight { get; private set; }

		// Override AddChild to push styles to child elements when table rows are resolved to the tree
		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (childToAdd is TextWidget textWidget)
			{
				// textWidget.TextColor = new Color("#036ac3");
				if (this.IsHeadingRow)
				{
					textWidget.Bold = true;
				}
			}
			else if (childToAdd is TextLinkX textLink)
			{
				foreach (var childTextWidget in childToAdd.Children.OfType<TextWidget>())
				{
					if (this.IsHeadingRow)
					{
						childTextWidget.Bold = true;
					}
				}
			}

			return base.AddChild(childToAdd, indexInChildrenList);
		}

		internal void CellHeightChanged(double newHeight)
		{
			double cellPadding = 2;
			double height = newHeight + 2 * cellPadding;

			//double maxChildHeight = this.Cells.Select(c => c.Height).Max();

			if (this.RowHeight != height)
			{
				foreach (var cell in this.Cells)
				{
					using (cell.LayoutLock())
					{
						cell.Height = height;
					}
				}

				using (this.LayoutLock())
				{
					this.Height = this.RowHeight = height;
				}
			}
		}
	}
}
