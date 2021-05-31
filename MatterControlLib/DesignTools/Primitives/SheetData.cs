/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools
{
	public class SheetData
	{
		public SheetData()
		{
		}

		public SheetData(int width, int height)
		{
			this.Rows = new List<RowData>(height);
			for (int i = 0; i < height; i++)
			{
				Rows.Add(new RowData(width));
			}
		}

		public string this[int x, int y]
		{
			get
			{
				return Rows[y].RowItems[x].Data;
			}

			set
			{
				Rows[y].RowItems[x].Data = value;
			}
		}

		[JsonIgnore]
		public int Width => Rows.Count;

		[JsonIgnore]
		public int Height => Rows[0].RowItems.Count;

		public class CellFormat
		{
			public enum DataTypes
			{
				String,
				Number,
				Curency,
				DateTime,
			}

			public int DecimalPlaces = 10;
			public DataTypes DataType;
			public Agg.Font.Justification Justification;
			public bool Bold;
		}

		public class TableCell
		{
			/// <summary>
			/// The user override name for this cell
			/// </summary>
			public string Name;

			public string Data = "";

			public CellFormat Format;
		}

		public class RowData
		{
			public List<TableCell> RowItems = new List<TableCell>();

			public RowData()
			{
			}

			public RowData(int numItems)
			{
				RowItems = new List<TableCell>(numItems);
				for (int i = 0; i < numItems; i++)
				{
					RowItems.Add(new TableCell());
				}
			}
		}

		public List<RowData> Rows = new List<RowData>();
	}
}