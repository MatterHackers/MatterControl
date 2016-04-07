using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace Gaming.Game
{
	public class DataViewGraph : WindowWidget
	{
		private RGBA_Floats SentDataLineColor = new RGBA_Floats(200, 200, 0);
		private RGBA_Floats ReceivedDataLineColor = new RGBA_Floats(0, 200, 20);
		private RGBA_Floats BoxColor = new RGBA_Floats(10, 25, 240);

		private double valueMin;
		private double valueMax;
		private bool dynamiclyScaleRange;
		private uint graphWidth;
		private uint graphHeight;
		private Dictionary<String, HistoryData> dataHistoryArray;
		private int nextLineColorIndex;
		private PathStorage linesToDrawStorage;

		internal class HistoryData
		{
			private int capacity;
			private List<double> data;

			internal double currentDataSum;
			internal RGBA_Bytes lineColor;

			internal HistoryData(int capacity, IColorType lineColor)
			{
				this.lineColor = lineColor.GetAsRGBA_Bytes();
				this.capacity = capacity;
				data = new List<double>();
				Reset();
			}

			public int Count
			{
				get
				{
					return data.Count;
				}
			}

			internal void Add(double Value)
			{
				if (data.Count == capacity)
				{
                    currentDataSum -= data[0];
					data.RemoveAt(0);
                }
				data.Add(Value);

				currentDataSum += Value;
			}

			internal void Reset()
			{
				currentDataSum = 0;
				data.Clear();
			}

			internal double GetItem(int ItemIndex)
			{
				if (ItemIndex < data.Count)
				{
					return data[ItemIndex];
				}
				else
				{
					return 0;
				}
			}

			internal double GetMaxValue()
			{
				double Max = -9999999999;
				for (int i = 0; i < data.Count; i++)
				{
					if (data[i] > Max)
					{
						Max = data[i];
					}
				}

				return Max;
			}

			internal double GetMinValue()
			{
				double Min = 9999999999;
				for (int i = 0; i < data.Count; i++)
				{
					if (data[i] < Min)
					{
						Min = data[i];
					}
				}

				return Min;
			}

			internal double GetAverageValue()
			{
				return currentDataSum / data.Count;
			}
		};

		public DataViewGraph()
			: this(80, 50, 0, 0)
		{
			dynamiclyScaleRange = true;
		}

		public DataViewGraph(uint Width, uint Height)
			: this(Width, Height, 0, 0)
		{
			dynamiclyScaleRange = true;
		}

		public DataViewGraph(uint width, uint height, double valueMin, double valueMax)
			: base(new RectangleDouble(0,0,width + 150,height + 80))
		{
			linesToDrawStorage = new PathStorage();
			dataHistoryArray = new Dictionary<String, HistoryData>();

			graphWidth = width;
			graphHeight = height;
			this.valueMin = valueMin;
			this.valueMax = valueMax;
			if (valueMin == 0 && valueMax == 0)
			{
				this.valueMax = -999999;
				this.valueMin = 999999;
			}
			dynamiclyScaleRange = false;
		}

		public double GetAverageValue(String DataType)
		{
			HistoryData TrendLine;
			dataHistoryArray.TryGetValue(DataType, out TrendLine);
			if (TrendLine != null)
			{
				return TrendLine.GetAverageValue();
			}

			return 0;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double currentTextHeight = -20;
			double Range = (valueMax - valueMin);
			VertexSourceApplyTransform TransformedLinesToDraw;
			Stroke StrockedTransformedLinesToDraw;

			Vector2 renderOffset = new Vector2(1, Height - graphHeight - 22);
            RoundedRect backGround = new RoundedRect(renderOffset.x, renderOffset.y - 1, renderOffset.x + graphWidth, renderOffset.y - 1 + graphHeight + 2, 5);
			graphics2D.Render(backGround, new RGBA_Bytes(0, 0, 0, .5));

			// if the 0 line is within the window than draw it.
			if (valueMin < 0 && valueMax > 0)
			{
				linesToDrawStorage.remove_all();
				linesToDrawStorage.MoveTo(renderOffset.x,
					renderOffset.y + ((0 - valueMin) * graphHeight / Range));
				linesToDrawStorage.LineTo(renderOffset.x + graphWidth,
					renderOffset.y + ((0 - valueMin) * graphHeight / Range));
				StrockedTransformedLinesToDraw = new Stroke(linesToDrawStorage);
				graphics2D.Render(StrockedTransformedLinesToDraw, new RGBA_Bytes(0, 0, 0, 1));
			}

			double MaxMax = -999999999;
			double MinMin = 999999999;
			double MaxAverage = 0;
			foreach (KeyValuePair<String, HistoryData> historyKeyValue in dataHistoryArray)
			{
				HistoryData history = historyKeyValue.Value;
				linesToDrawStorage.remove_all();
				MaxMax = System.Math.Max(MaxMax, history.GetMaxValue());
				MinMin = System.Math.Min(MinMin, history.GetMinValue());
				MaxAverage = System.Math.Max(MaxAverage, history.GetAverageValue());
				for (int i = 0; i < graphWidth - 1; i++)
				{
					if (i == 0)
					{
						linesToDrawStorage.MoveTo(renderOffset.x + i,
							renderOffset.y + ((history.GetItem(i) - valueMin) * graphHeight / Range));
					}
					else
					{
						linesToDrawStorage.LineTo(renderOffset.x + i,
							renderOffset.y + ((history.GetItem(i) - valueMin) * graphHeight / Range));
					}
				}

				StrockedTransformedLinesToDraw = new Stroke(linesToDrawStorage);
				graphics2D.Render(StrockedTransformedLinesToDraw, history.lineColor);

				String Text = historyKeyValue.Key + ": Min:" + MinMin.ToString("0.0") + " Max:" + MaxMax.ToString("0.0") + " Avg:" + MaxAverage.ToString("0.0");
				graphics2D.DrawString(Text, renderOffset.x, renderOffset.y + currentTextHeight, backgroundColor: new RGBA_Bytes(RGBA_Bytes.White, 220), drawFromHintedCach: true);
				currentTextHeight -= 20;
			}

			RoundedRect BackGround2 = new RoundedRect(renderOffset.x, renderOffset.y - 1, renderOffset.x + graphWidth, renderOffset.y - 1 + graphHeight + 2, 5);
			Stroke StrockedTransformedBackGround = new Stroke(BackGround2);
			graphics2D.Render(StrockedTransformedBackGround, new RGBA_Bytes(0.0, 0, 0, 1));

			//renderer.Color = BoxColor;
			//renderer.DrawRect(m_Position.x, m_Position.y - 1, m_Width, m_Height + 2);

			base.OnDraw(graphics2D);
		}

		public void AddData(String DataType, double NewData)
		{
			if (dynamiclyScaleRange)
			{
				valueMax = System.Math.Max(valueMax, NewData);
				valueMin = System.Math.Min(valueMin, NewData);
			}

			if (!dataHistoryArray.ContainsKey(DataType))
			{
				RGBA_Bytes LineColor = new RGBA_Bytes(255, 255, 255);
				switch (nextLineColorIndex++ % 3)
				{
					case 0:
						LineColor = new RGBA_Bytes(255, 55, 55);
						break;

					case 1:
						LineColor = new RGBA_Bytes(55, 255, 55);
						break;

					case 2:
						LineColor = new RGBA_Bytes(55, 55, 255);
						break;
				}

				dataHistoryArray.Add(DataType, new HistoryData((int)graphWidth, LineColor));
			}

			dataHistoryArray[DataType].Add(NewData);
		}

		public void Reset()
		{
			valueMax = 1;
			valueMin = 99999;
			foreach (KeyValuePair<String, HistoryData> historyKeyValue in dataHistoryArray)
			{
				historyKeyValue.Value.Reset();
			}
		}
	};
}