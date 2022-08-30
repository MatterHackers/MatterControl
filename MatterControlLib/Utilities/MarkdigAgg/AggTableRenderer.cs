// Copyright (c) Nicolas Musset. All rights reserved.
// Copyright (c) 2022, John Lewin
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using Markdig.Extensions.Tables;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;

namespace Markdig.Renderers.Agg
{
    public class AggTableRenderer : AggObjectRenderer<Table>
    {
        protected override void Write(AggRenderer renderer, Table table)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            if (table == null) throw new ArgumentNullException(nameof(table));

            var aggTable = new FlowLayoutWidget(FlowDirection.TopToBottom)
            {
                HAnchor = HAnchor.Fit,
                VAnchor = VAnchor.Fit,
                Margin = new BorderDouble(top: 12),
            };

            // TODO: Use Markdig parser data to drive column/cell widths
            //foreach (var tableColumnDefinition in table.ColumnDefinitions)
            //  Width = (tableColumnDefinition?.Width ?? 0) != 0 ? tableColumnDefinition.Width : <or auto>

            renderer.Push(aggTable);

            foreach (var rowObj in table)
            {
                var row = (TableRow)rowObj;

                var aggRow = new AggTableRow()
                {
                    IsHeadingRow = row.IsHeader,
                };

                renderer.Push(aggRow);

                if (row.IsHeader)
                {
                    // Update to desired header row styling and/or moving into AggTableRow for consistency
                    aggRow.BackgroundColor = MatterHackers.MatterControl.AppContext.Theme.TabBarBackground;
                }

                for (var i = 0; i < row.Count; i++)
                {
                    var cellObj = row[i];
                    var cell = (TableCell)cellObj;

                    // Fixed width cells just to get something initially on screen
                    var aggCellBox = new GuiWidget()
                    {
                        Width = 200,
                        Height = 25,
                    };

                    // TODO: Cell Width - implement next, might be easy to track and perform in AggTableRow
                    /*  (Spec)
                     *  If any line of the markdown source is longer than the column width (see --columns), then the
                     *  table will take up the full text width and the cell contents will wrap, with the relative cell
                     *  widths determined by the number of dashes in the line separating the table header from the table
                     *  body. (For example ---|- would make the first column 3/4 and the second column 1/4 of the full
                     *  text width.) On the other hand, if no lines are wider than column width, then cell contents will
                     *  not be wrapped, and the cells will be sized to their contents.
                    */

                    // Cell box above enforces boundaries, use flow for layout
                    var aggCellFlow = new FlowLayoutWidget()
                    {
                        HAnchor = HAnchor.Stretch,
                    };

                    if (table.ColumnDefinitions.Count > 0)
                    {
                        // TODO: Ideally we'd be driving column width from metadata rather than hard-coded
                        // See example below from WPF implementation
                        //
                        // Grab the column definition, or fall back to a default
                        var columnIndex = cell.ColumnIndex < 0 || cell.ColumnIndex >= table.ColumnDefinitions.Count
                            ? i
                            : cell.ColumnIndex;
                        columnIndex = columnIndex >= table.ColumnDefinitions.Count ? table.ColumnDefinitions.Count - 1 : columnIndex;

                        // TODO: revise alignment via Agg types that produce aligned text
                        var columnDefinition = table.ColumnDefinitions[columnIndex];
                        var alignment = columnDefinition.Alignment;
                        if (alignment.HasValue)
                        {
                            switch (alignment)
                            {
                                case TableColumnAlign.Center:
                                    aggCellFlow.HAnchor |= HAnchor.Center;
                                    break;
                                case TableColumnAlign.Right:
                                    aggCellFlow.HAnchor |= HAnchor.Right;
                                    break;
                                case TableColumnAlign.Left:
                                    aggCellFlow.HAnchor |= HAnchor.Left;
                                    break;
                            }
                        }
                    }

                    renderer.Push(aggCellBox);
                    renderer.Push(aggCellFlow);
                    renderer.Write(cell);
                    renderer.Pop();
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