using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace Gaming.Game
{
	public class DataViewGraph
	{
		private RGBA_Floats SentDataLineColor = new RGBA_Floats(200, 200, 0);
		private RGBA_Floats ReceivedDataLineColor = new RGBA_Floats(0, 200, 20);
		private RGBA_Floats BoxColor = new RGBA_Floats(10, 25, 240);

		private double m_DataViewMinY;
		private double m_DataViewMaxY;
		private bool m_DynamiclyScaleYRange;
		private Vector2 m_Position;
		private uint m_Width;
		private uint m_Height;
		private Dictionary<String, HistoryData> m_DataHistoryArray;
		private int m_ColorIndex;
		private PathStorage m_LinesToDraw;

		internal class HistoryData
		{
			private int m_Capacity;
			private List<double> m_Data;

			internal double m_TotalValue;
			internal RGBA_Bytes m_Color;

			internal HistoryData(int Capacity, IColorType Color)
			{
				m_Color = Color.GetAsRGBA_Bytes();
				m_Capacity = Capacity;
				m_Data = new List<double>();
				Reset();
			}

			public int Count
			{
				get
				{
					return m_Data.Count;
				}
			}

			internal void Add(double Value)
			{
				if (m_Data.Count == m_Capacity)
				{
                    m_TotalValue -= m_Data[0];
					m_Data.RemoveAt(0);
                }
				m_Data.Add(Value);

				m_TotalValue += Value;
			}

			internal void Reset()
			{
				m_TotalValue = 0;
				m_Data.Clear();
			}

			internal double GetItem(int ItemIndex)
			{
				if (ItemIndex < m_Data.Count)
				{
					return m_Data[ItemIndex];
				}
				else
				{
					return 0;
				}
			}

			internal double GetMaxValue()
			{
				double Max = -9999999999;
				for (int i = 0; i < m_Data.Count; i++)
				{
					if (m_Data[i] > Max)
					{
						Max = m_Data[i];
					}
				}

				return Max;
			}

			internal double GetMinValue()
			{
				double Min = 9999999999;
				for (int i = 0; i < m_Data.Count; i++)
				{
					if (m_Data[i] < Min)
					{
						Min = m_Data[i];
					}
				}

				return Min;
			}

			internal double GetAverageValue()
			{
				return m_TotalValue / m_Data.Count;
			}
		};

		public DataViewGraph(Vector2 RenderPosition)
			: this(RenderPosition, 80, 50, 0, 0)
		{
			m_DynamiclyScaleYRange = true;
		}

		public DataViewGraph(Vector2 RenderPosition, uint Width, uint Height)
			: this(RenderPosition, Width, Height, 0, 0)
		{
			m_DynamiclyScaleYRange = true;
		}

		public DataViewGraph(Vector2 RenderPosition, uint Width, uint Height, double StartMin, double StartMax)
		{
			m_LinesToDraw = new PathStorage();
			m_DataHistoryArray = new Dictionary<String, HistoryData>();

			m_Width = Width;
			m_Height = Height;
			m_DataViewMinY = StartMin;
			m_DataViewMaxY = StartMax;
			if (StartMin == 0 && StartMax == 0)
			{
				m_DataViewMaxY = -999999;
				m_DataViewMinY = 999999;
			}
			m_Position = RenderPosition;
			m_DynamiclyScaleYRange = false;
		}

		public double GetAverageValue(String DataType)
		{
			HistoryData TrendLine;
			m_DataHistoryArray.TryGetValue(DataType, out TrendLine);
			if (TrendLine != null)
			{
				return TrendLine.GetAverageValue();
			}

			return 0;
		}

		public void Draw(MatterHackers.Agg.Transform.ITransform Position, Graphics2D renderer)
		{
			double TextHeight = m_Position.y - 20;
			double Range = (m_DataViewMaxY - m_DataViewMinY);
			VertexSourceApplyTransform TransformedLinesToDraw;
			Stroke StrockedTransformedLinesToDraw;

			RoundedRect BackGround = new RoundedRect(m_Position.x, m_Position.y - 1, m_Position.x + m_Width, m_Position.y - 1 + m_Height + 2, 5);
			VertexSourceApplyTransform TransformedBackGround = new VertexSourceApplyTransform(BackGround, Position);
			renderer.Render(TransformedBackGround, new RGBA_Bytes(0, 0, 0, .5));

			// if the 0 line is within the window than draw it.
			if (m_DataViewMinY < 0 && m_DataViewMaxY > 0)
			{
				m_LinesToDraw.remove_all();
				m_LinesToDraw.MoveTo(m_Position.x,
					m_Position.y + ((0 - m_DataViewMinY) * m_Height / Range));
				m_LinesToDraw.LineTo(m_Position.x + m_Width,
					m_Position.y + ((0 - m_DataViewMinY) * m_Height / Range));
				TransformedLinesToDraw = new VertexSourceApplyTransform(m_LinesToDraw, Position);
				StrockedTransformedLinesToDraw = new Stroke(TransformedLinesToDraw);
				renderer.Render(StrockedTransformedLinesToDraw, new RGBA_Bytes(0, 0, 0, 1));
			}

			double MaxMax = -999999999;
			double MinMin = 999999999;
			double MaxAverage = 0;
			foreach (KeyValuePair<String, HistoryData> historyKeyValue in m_DataHistoryArray)
			{
				HistoryData history = historyKeyValue.Value;
				m_LinesToDraw.remove_all();
				MaxMax = System.Math.Max(MaxMax, history.GetMaxValue());
				MinMin = System.Math.Min(MinMin, history.GetMinValue());
				MaxAverage = System.Math.Max(MaxAverage, history.GetAverageValue());
				for (int i = 0; i < m_Width - 1; i++)
				{
					if (i == 0)
					{
						m_LinesToDraw.MoveTo(m_Position.x + i,
							m_Position.y + ((history.GetItem(i) - m_DataViewMinY) * m_Height / Range));
					}
					else
					{
						m_LinesToDraw.LineTo(m_Position.x + i,
							m_Position.y + ((history.GetItem(i) - m_DataViewMinY) * m_Height / Range));
					}
				}

				TransformedLinesToDraw = new VertexSourceApplyTransform(m_LinesToDraw, Position);
				StrockedTransformedLinesToDraw = new Stroke(TransformedLinesToDraw);
				renderer.Render(StrockedTransformedLinesToDraw, history.m_Color);

				String Text = historyKeyValue.Key + ": Min:" + MinMin.ToString("0.0") + " Max:" + MaxMax.ToString("0.0") + " Avg:" + MaxAverage.ToString("0.0");
				renderer.DrawString(Text, m_Position.x, TextHeight, backgroundColor: new RGBA_Bytes(RGBA_Bytes.White, 220));
				TextHeight -= 20;
			}

			RoundedRect BackGround2 = new RoundedRect(m_Position.x, m_Position.y - 1, m_Position.x + m_Width, m_Position.y - 1 + m_Height + 2, 5);
			VertexSourceApplyTransform TransformedBackGround2 = new VertexSourceApplyTransform(BackGround2, Position);
			Stroke StrockedTransformedBackGround = new Stroke(TransformedBackGround2);
			renderer.Render(StrockedTransformedBackGround, new RGBA_Bytes(0.0, 0, 0, 1));

			//renderer.Color = BoxColor;
			//renderer.DrawRect(m_Position.x, m_Position.y - 1, m_Width, m_Height + 2);
		}

		public void AddData(String DataType, double NewData)
		{
			if (m_DynamiclyScaleYRange)
			{
				m_DataViewMaxY = System.Math.Max(m_DataViewMaxY, NewData);
				m_DataViewMinY = System.Math.Min(m_DataViewMinY, NewData);
			}

			if (!m_DataHistoryArray.ContainsKey(DataType))
			{
				RGBA_Bytes LineColor = new RGBA_Bytes(255, 255, 255);
				switch (m_ColorIndex++ % 3)
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

				m_DataHistoryArray.Add(DataType, new HistoryData((int)m_Width, LineColor));
			}

			m_DataHistoryArray[DataType].Add(NewData);
		}

		public void Reset()
		{
			m_DataViewMaxY = 1;
			m_DataViewMinY = 99999;
			foreach (KeyValuePair<String, HistoryData> historyKeyValue in m_DataHistoryArray)
			{
				historyKeyValue.Value.Reset();
			}
		}
	};
}