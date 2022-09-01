// Copyright (c) Nicolas Musset. All rights reserved.
// Copyright (c) 2022, John Lewin
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using Markdig.Extensions.Tables;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class AggTableRenderer : AggObjectRenderer<Table>
	{
		protected override void Write(AggRenderer renderer, Table mdTable)
		{
			if (renderer == null) throw new ArgumentNullException(nameof(renderer));
			if (mdTable == null) throw new ArgumentNullException(nameof(mdTable));

			var aggTable = new AggTable(mdTable)
			{
				Margin = new BorderDouble(top: 12),
			};

			renderer.Push(aggTable);

			foreach (var rowObj in mdTable)
			{
				var mdRow = (TableRow)rowObj;

				var aggRow = new AggTableRow()
				{
					IsHeadingRow = mdRow.IsHeader,
				};

				renderer.Push(aggRow);

				if (mdRow.IsHeader)
				{
					// Update to desired header row styling and/or move into AggTableRow for consistency
					aggRow.BackgroundColor = MatterHackers.MatterControl.AppContext.Theme.TabBarBackground;
				}

				for (var i = 0; i < mdRow.Count; i++)
				{
					var mdCell = (TableCell)mdRow[i];

					var aggCell = new AggTableCell();
					aggRow.Cells.Add(aggCell);

					if (mdTable.ColumnDefinitions.Count > 0)
					{
						// Grab the column definition, or fall back to a default
						var columnIndex = mdCell.ColumnIndex < 0 || mdCell.ColumnIndex >= mdTable.ColumnDefinitions.Count
							? i
							: mdCell.ColumnIndex;
						columnIndex = columnIndex >= mdTable.ColumnDefinitions.Count ? mdTable.ColumnDefinitions.Count - 1 : columnIndex;

						aggTable.Columns[columnIndex].Cells.Add(aggCell);

						if (mdTable.ColumnDefinitions[columnIndex].Alignment.HasValue)
						{
							switch (mdTable.ColumnDefinitions[columnIndex].Alignment)
							{
								case TableColumnAlign.Center:
									aggCell.FlowHAnchor |= HAnchor.Center;
									break;
								case TableColumnAlign.Right:
									aggCell.FlowHAnchor |= HAnchor.Right;
									break;
								case TableColumnAlign.Left:
									aggCell.FlowHAnchor |= HAnchor.Left;
									break;
							}
						}
					}

					renderer.Push(aggCell);
					renderer.Write(mdCell);
					renderer.Pop();
				}

				// Pop row
				renderer.Pop();
			}

			// Pop table
			renderer.Pop();
		}
	}
}