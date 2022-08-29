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

                GuiWidget aggRow = row.IsHeader ? new TableHeadingRow() : new FlowLayoutWidget()
                {
                    HAnchor = HAnchor.Fit,
                    VAnchor = VAnchor.Absolute,
                    Height = 25,
                };

                renderer.Push(aggRow);

                if (row.IsHeader)
                {
                    // Update to desired header row styling
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

                        // TODO: implement alignment into Agg types that won't throw when set
                        var alignment = table.ColumnDefinitions[columnIndex].Alignment;
                        if (alignment.HasValue)
                        {
                            switch (alignment)
                            {
                                //case TableColumnAlign.Center:
                                //    aggCell.HAnchor |= HAnchor.Center;
                                //    break;
                                //case TableColumnAlign.Right:
                                //    aggCell.HAnchor |= HAnchor.Right;
                                //    break;
                                //case TableColumnAlign.Left:
                                //    aggCell.HAnchor |= HAnchor.Left;
                                //    break;
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