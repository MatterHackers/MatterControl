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
using org.mariuszgromada.math.mxparser;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace MatterHackers.MatterControl.DesignTools
{
	public class SheetData
	{
		private object locker = new object();

		public event EventHandler Recalculated;

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

		public string EvaluateExpression(string expression)
		{
			lock (locker)
			{
				if (!tabelCalculated)
				{
					BuildTableConstants();
				}

				if(expression.StartsWith("="))
				{
					expression = expression.Substring(1);
				}
				var evaluator = new Expression(expression.ToLower());
				AddConstants(evaluator);
				var value = evaluator.calculate();

				return value.ToString();
			}
		}

		public string CellId(int x, int y)
		{
			lock (locker)
			{
				return $"{(char)('A' + x)}{y + 1}";
			}
		}

		public TableCell this[int x, int y]
		{
			get
			{
				lock (locker)
				{
					return this[CellId(x, y)];
				}
			}

			set
			{
				lock (locker)
				{
					this[CellId(x, y)] = value;
				}
			}
		}

		public TableCell this[string cellId]
		{
			get
			{
				lock (locker)
				{
					if (cellId.Length == 2)
					{
						var x = cellId.Substring(0, 1).ToUpper()[0] - 'A';
						var y = cellId.Substring(1, 1)[0] - '1';
						return Rows[y].Cells[x];
					}
					else
					{
						foreach (var row in Rows)
						{
							foreach (var cell in row.Cells)
							{
								if (cellId.Equals(cell.Name, StringComparison.OrdinalIgnoreCase))
								{
									return cell;
								}
							}
						}
					}

					return null;
				}
			}

			set
			{
				lock (locker)
				{
					if (cellId.Length == 2)
					{
						var x = cellId.Substring(0, 1).ToUpper()[0] - 'A';
						var y = cellId.Substring(1, 1)[0] - '1';
						Rows[y].Cells[x] = value;
					}
					else
					{
						foreach (var row in Rows)
						{
							for (int i = 0; i < row.Cells.Count; i++)
							{
								if (cellId.Equals(row.Cells[i].Name, StringComparison.OrdinalIgnoreCase))
								{
									row.Cells[i] = value;
								}
							}
						}
					}
				}
			}
		}

		[JsonIgnore]
		public int Width => Rows.Count;

		[JsonIgnore]
		public int Height => Rows[0].Cells.Count;

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
			/// The user override id for this cell
			/// </summary>
			public string Name;

			/// <summary>
			/// The actual content typed into the cell
			/// </summary>
			public string Expression = "";

			/// <summary>
			/// How to format this data in the display
			/// </summary>
			public CellFormat Format;

			public override string ToString()
			{
				return Expression;
			}
		}

		public class RowData
		{
			public List<TableCell> Cells = new List<TableCell>();

			public RowData()
			{
			}

			public RowData(int numItems)
			{
				Cells = new List<TableCell>(numItems);
				for (int i = 0; i < numItems; i++)
				{
					Cells.Add(new TableCell());
				}
			}
		}

		public List<RowData> Rows = new List<RowData>();

		Dictionary<string, double> constants = new Dictionary<string, double>();
		private bool tabelCalculated;

		private IEnumerable<(int x, int y, TableCell cell)> EnumerateCells()
		{
			lock (locker)
			{
				for (int y = 0; y < Rows.Count; y++)
				{
					for (int x = 0; x < Rows[y].Cells.Count; x++)
					{
						yield return (x, y, this[x, y]);
					}
				}
			}
		}

		public void Recalculate()
		{
			BuildTableConstants();

			Recalculated?.Invoke(this, null);
		}

		private void BuildTableConstants()
		{
			double GetValue((int x, int y, TableCell cell) xyCell)
			{
				var expression = xyCell.cell.Expression;
				if (expression.StartsWith("="))
				{
					expression = expression.Substring(1);
				}
				if (expression.StartsWith("."))
				{
					expression = "0" + expression;
				}
				var evaluator = new Expression(expression.ToLower());
				AddConstants(evaluator);
				var value = evaluator.calculate();
				return value;
			}

			lock (locker)
			{
				constants.Clear();

				var list = EnumerateCells().ToList();
				var noSolution = new List<(int, int, TableCell)>();
				var addedConstant = false;

				// process all the cells until we can no longer solve a new value (needs to be DAG)
				do
				{
					addedConstant = false;
					foreach (var xyCell in list)
					{
						double value = GetValue(xyCell);
						if (double.IsNaN(value)
							|| double.IsInfinity(value))
						{
							noSolution.Add(xyCell);
						}
						else
						{
							constants.Add(CellId(xyCell.x, xyCell.y).ToLower(), value);
							if (!string.IsNullOrEmpty(xyCell.cell.Name))
							{
								constants.Add(xyCell.cell.Name.ToLower(), value);
							}

							addedConstant = true;
						}
					}

					list.Clear();
					list.AddRange(noSolution);
					noSolution.Clear();
				} while (list.Count > 0 && addedConstant);

				// add the rest of the cells to constants as 0's
				foreach (var xyCell in list)
				{
					constants.Add(CellId(xyCell.x, xyCell.y).ToLower(), 0);
					if (!string.IsNullOrEmpty(xyCell.cell.Name))
					{
						constants.Add(xyCell.cell.Name.ToLower(), 0);
					}
				}

				tabelCalculated = true;
			}
		}

		private void AddConstants(Expression evaluator)
		{
			lock (locker)
			{
				foreach (var kvp in constants)
				{
					evaluator.defineConstant(kvp.Key, kvp.Value);
				}
			}
		}
	}
}