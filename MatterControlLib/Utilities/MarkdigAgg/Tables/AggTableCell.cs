// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// Copyright (c) 2022, John Lewin
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	// Parent container to restrict bounds
	public class AggTableCell : GuiWidget
	{
		public AggTableCell()
		{
			// TODO: drive from column once width calculation is performed
			Width = 300;

			Height = 25;

			// Use event rather than OnLayout as it only seems to produce the desired effect
			this.Layout += AggTableCell_Layout;
		}

		// TODO: Investigate. Without this solution, child content is wrapped and clipped, leaving only the last text block visible
		private void AggTableCell_Layout(object sender, EventArgs e)
		{
			Console.WriteLine(Parent?.Name);
			if (this.Children.Count > 0 && this.Children.First() is FlowLeftRightWithWrapping wrappedChild
				&& wrappedChild.Height != this.Height)
			{
				//using (this.LayoutLock())
				{
					//// Set height to ensure bounds grow to content after reflow
					//this.Height = wrappedChild.Height;

					if (this.Parent is AggTableRow parentRow)
					{
						parentRow.CellHeightChanged(wrappedChild.Height);
					}
				}

				this.ContentWidth = wrappedChild.ContentWidth;
			}
		}

		public double ContentWidth { get; private set; }

		// TODO: Use to align child content when bounds are less than current
		public HAnchor FlowHAnchor { get; set; }
	}
}
