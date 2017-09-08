using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class DataViewGraph : GuiWidget
	{
		private HistoryData dataHistoryArray;
		private RGBA_Floats LineColor = RGBA_Floats.Black;

		public DataViewGraph()
		{
			dataHistoryArray = new HistoryData(10);
			DoubleBuffer = true;
		}

		public bool DynamiclyScaleRange { get; set; } = true;

		RGBA_Bytes _goalColor = RGBA_Bytes.Yellow;
		public RGBA_Bytes GoalColor
		{
			get { return _goalColor; }
			set
			{
				_goalColor = value;
				Invalidate();
			}
		}

		double _goalValue;
		public double GoalValue
		{
			get { return _goalValue; }
			set
			{
				_goalValue = value;
				Invalidate();
			}
		}

		public override RectangleDouble LocalBounds
		{
			get => base.LocalBounds; set
			{
				dataHistoryArray = new HistoryData(Math.Min(1000, Math.Max(1, (int)(value.Width))));
				base.LocalBounds = value;
			}
		}

		public double MaxValue { get; set; } = double.MinValue;
		public double MinValue { get; set; } = double.MaxValue;
		public bool ShowGoal { get; set; }
		public int TotalAdds { get; private set; }

		public void AddData(double NewData)
		{
			if (DynamiclyScaleRange)
			{
				MaxValue = System.Math.Max(MaxValue, NewData);
				MinValue = System.Math.Min(MinValue, NewData);
			}

			dataHistoryArray.Add(NewData);

			TotalAdds++;

			Invalidate();
		}

		public double GetAverageValue()
		{
			return dataHistoryArray.GetAverageValue();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var linesToDrawStorage = new PathStorage();
			double Range = (MaxValue - MinValue);

			if (ShowGoal)
			{
				var yPos = (GoalValue - MinValue) * Height / Range;
				graphics2D.Line(0, yPos, Width, yPos, GoalColor);
			}

			RGBA_Bytes backgroundGridColor = RGBA_Bytes.Gray;

			graphics2D.Line(0, 0, Width, 0, backgroundGridColor);
			graphics2D.Line(0, Height, Width, Height, backgroundGridColor);

			double pixelSkip = Height;
			for (int i = 0; i < Width / pixelSkip; i++)
			{
				double xPos = Width - ((i * pixelSkip + TotalAdds) % Width);
				int inset = (int)((i % 2) == 0 ? Height / 6 : Height / 3);
				graphics2D.Line(xPos, inset, xPos, Height - inset, new RGBA_Bytes(backgroundGridColor, 64));
			}

			for (int i = 0; i < Width - 1; i++)
			{
				if (i == 0)
				{
					linesToDrawStorage.MoveTo(i + Width - dataHistoryArray.Count, ((dataHistoryArray.GetItem(i) - MinValue) * Height / Range));
				}
				else
				{
					linesToDrawStorage.LineTo(i + Width - dataHistoryArray.Count, ((dataHistoryArray.GetItem(i) - MinValue) * Height / Range));
				}
			}

			graphics2D.Render(new Stroke(linesToDrawStorage), LineColor);

			base.OnDraw(graphics2D);
		}

		public void Reset()
		{
			dataHistoryArray.Reset();
		}

		internal class HistoryData
		{
			internal double currentDataSum;
			private int capacity;
			private List<double> data;

			internal HistoryData(int capacity)
			{
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

			internal double GetAverageValue()
			{
				return currentDataSum / data.Count;
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
				double Max = -double.MinValue;
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
				double Min = double.MaxValue;
				for (int i = 0; i < data.Count; i++)
				{
					if (data[i] < Min)
					{
						Min = data[i];
					}
				}

				return Min;
			}

			internal void Reset()
			{
				currentDataSum = 0;
				data.Clear();
			}
		};
	};
}