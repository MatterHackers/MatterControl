// Copyright (c) Nicolas Musset. All rights reserved.
// Copyright (c) 2022, John Lewin
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Markdig.Extensions.Tables;

namespace Markdig.Renderers.Agg
{
	public class AggTableColumn
	{
		private TableColumnDefinition ColumnDefinition;

		public AggTableColumn(TableColumnDefinition definition)
		{
			this.ColumnDefinition = definition;
		}

		public List<AggTableCell> Cells { get; } = new List<AggTableCell>();

		public void SetCellWidths()
		{
			double cellPadding = 10;

			if (this.Cells.Count == 0)
			{
				return;
			}

			// TODO: Column/cell width theortically is:
			//
			//  Case A. Expanding to the maximum content width of cells in column to grow each
			//          cell to a minimum value.
			//
			//  Case B. Contracting when the aggregate column widths exceed the bounds
			//     of the parent container.
			//
			//  Case C. Distributing percentages across fixed bounds of the parent container
			//
			//  Other cases...

			// This block attempts to implement Case A by finding the max content width per cells in each column
			//
			// Collect max content widths from each cell in this column
			double maxCellWidth = this.Cells.Select(c => c.ContentWidth).Max() + cellPadding * 2;

			// Apply max width to cells in this column
			foreach (var cell in this.Cells)
			{
				if (cell.Width != maxCellWidth)
				{
					cell.Width = maxCellWidth;
				}
			}
		}
	}
}