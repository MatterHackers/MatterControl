/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools
{
	public class GridWidget : FlowLayoutWidget
	{
		public int GridWidth => Children[0].Children.Count;

		public int GridHeight => Children.Count;

		public void SetColumnWidth(int index, double width)
		{
			for (int y = 0; y < GridHeight; y++)
			{
				Children[y].Children[index].Width = width * GuiWidget.DeviceScale;
			}
		}

		public void SetRowHeight(int index, double height)
		{
			for (int x = 0; x < GridHeight; x++)
			{
				Children[index].Children[x].Height = height;
			}
		}

		public double GetColumnWidth(int index)
		{
			return Children[0].Children[index].Width;
		}

		public double GetRowHeight(int index)
		{
			return Children[index].Children[0].Height;
		}

		public GridWidget(int gridWidth, int gridHeight, double columnWidth = 60, double rowHeight = 14, ThemeConfig theme = null)
			: base(FlowDirection.TopToBottom)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				var row = new FlowLayoutWidget();
				this.AddChild(row);
				for (int x = 0; x < gridWidth; x++)
				{
					row.AddChild(new GuiWidget()
					{
						Width = columnWidth * GuiWidget.DeviceScale,
						Height = rowHeight * GuiWidget.DeviceScale,
						Border = 1,
						BorderColor = theme == null ? Color.LightGray : theme.BorderColor20,
					});
				}
			}
		}

		public GuiWidget GetCell(int x, int y)
		{
			if (x < GridWidth && y < GridHeight)
			{
				return Children[y].Children[x];
			}

			return null;
		}

		public void ExpandRowToMaxHeight(int index)
		{
			var maxHeight = GetRowHeight(index);
			for (int x = 0; x < GridWidth; x++)
			{
				var cell = GetCell(x, index).Children.FirstOrDefault();
				if (cell != null)
				{
					maxHeight = Math.Max(maxHeight, cell.Height + cell.DeviceMarginAndBorder.Height);
				}
			}

			SetRowHeight(index, maxHeight);
		}

		public void ExpandColumnToMaxWidth(int index)
		{
			var maxWidth = GetColumnWidth(index);
			for (int y = 0; y < GridHeight; y++)
			{
				var cell = GetCell(index, y).Children.FirstOrDefault();
				if (cell != null)
				{
					maxWidth = Math.Max(maxWidth, cell.Width + cell.DeviceMarginAndBorder.Width);
				}
			}

			SetColumnWidth(index, maxWidth);
		}

		public void ExpandToFitContent()
		{
			for (int x = 0; x < GridWidth; x++)
			{
				ExpandColumnToMaxWidth(x);
			}

			for (int y = 0; y < GridHeight; y++)
			{
				ExpandRowToMaxHeight(y);
			}
		}
	}
}
