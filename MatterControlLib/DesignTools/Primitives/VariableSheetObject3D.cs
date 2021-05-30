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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools
{
	public class SheetData
	{
		public SheetData(int width, int height)
		{
			this.Table = new List<List<RowData>>(height);
			for (int i = 0; i < height; i++)
			{
				Table.Add(new List<RowData>(width));
				for (int j = 0; j < width; j++)
				{
					Table[i].Add(new RowData());
				}
			}
		}

		[JsonIgnore]
		public int Width => Table.Count;

		[JsonIgnore]
		public int Height => Table[0].Count;

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

		public class CellData
		{
			/// <summary>
			/// The user override name for this cell
			/// </summary>
			public string Name;

			public string Data;

			public CellFormat Format;
		}

		public class RowData
		{
			public List<CellData> RowItems;
		}

		public List<List<RowData>> Table;
	}

	public class VariableSheetObject3D : Object3D
	{
		public SheetData SheetData { get; set; } = new SheetData(5, 5);

		public override Mesh Mesh
		{
			get
			{
				if (!this.Children.Where(i => i.VisibleMeshes().Count() > 0).Any())
				{
					// add the amf content
					using (Stream measureAmfStream = StaticData.Instance.OpenStream(Path.Combine("Stls", "description_tool.amf")))
					{
						Children.Modify((list) =>
						{
							list.Clear();
							list.Add(AmfDocument.Load(measureAmfStream, CancellationToken.None));
						});
					}
				}

				return base.Mesh;
			}

			set => base.Mesh = value;
		}

		public static async Task<VariableSheetObject3D> Create()
		{
			var item = new VariableSheetObject3D();
			await item.Rebuild();
			return item;
		}

		public static T FindTableAndValue<T>(IObject3D owner, string expresion)
		{
			if (typeof(T) == typeof(double))
			{
				// this way we can use the common pattern without error
				return (T)(object)5.5;
			}

			throw new NotImplementedException();
		}
	}
}