// Copyright (c) Nicolas Musset. All rights reserved.
// Copyright (c) 2022, John Lewin
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Markdig.Extensions.Tables;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class AggTable : FlowLayoutWidget
	{

		public List<AggTableColumn> Columns { get; }

		public List<AggTableRow> Rows { get; }

		public AggTable(Table table) : base(FlowDirection.TopToBottom)
		{
			this.Columns = table.ColumnDefinitions.Select(c => new AggTableColumn(c)).ToList();
		}

		public override void OnLayout(LayoutEventArgs layoutEventArgs)
		{
			if (this.Columns?.Count > 0)
			{
				foreach (var column in this.Columns)
				{
					column.SetCellWidths();
				}
			}

			base.OnLayout(layoutEventArgs);
		}
	}
}