/*
Copyright (c) 2023, Lars Brubaker
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class DataViewGraph : GuiWidget
	{
		private HistoryData dataHistoryArray;
		private ColorF LineColor = ColorF.Black;

		public DataViewGraph()
		{
			dataHistoryArray = new HistoryData(10);
			DoubleBuffer = true;
		}

		public bool DynamicallyScaleRange { get; set; } = true;

		private Color _goalColor = Color.Yellow;

		public Color GoalColor
		{
			get
			{
				return _goalColor;
			}

			set
			{
				_goalColor = value;
				Invalidate();
			}
		}

		private double _goalValue;

		public double GoalValue
		{
			get
			{
				return _goalValue;
			}

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
				dataHistoryArray = new HistoryData(Math.Min(1000, Math.Max(1, (int)value.Width)));
				base.LocalBounds = value;
			}
		}

		public double MaxValue { get; set; } = double.MinValue;

		public double MinValue { get; set; } = double.MaxValue;

		public bool ShowGoal { get; set; }

		public int TotalAdds { get; private set; }

		public void AddData(double newData)
		{
			if (DynamicallyScaleRange)
			{
				MaxValue = System.Math.Max(MaxValue, newData);
				MinValue = System.Math.Min(MinValue, newData);
			}

			dataHistoryArray.Add(newData);

			TotalAdds++;

			if (this.ActuallyVisibleOnScreen())
			{
				Invalidate();
			}
		}

		public double GetAverageValue()
		{
			return dataHistoryArray.GetAverageValue();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var linesToDrawStorage = new VertexStorage();
			double range = MaxValue - MinValue;

			if (ShowGoal)
			{
				var yPos = (GoalValue - MinValue) * Height / range;
				graphics2D.Line(0, yPos, Width, yPos, GoalColor);
			}

			Color backgroundGridColor = Color.Gray;

			double pixelSkip = Height;
			for (int i = 0; i < Width / pixelSkip; i++)
			{
				double xPos = Width - ((i * pixelSkip + TotalAdds) % Width);
				int inset = (int)((i % 2) == 0 ? Height / 6 : Height / 3);
				graphics2D.Line(xPos, inset, xPos, Height - inset, new Color(backgroundGridColor, 120));
			}

			for (int i = 0; i < Width - 1; i++)
			{
				if (i == 0)
				{
					linesToDrawStorage.MoveTo(i + Width - dataHistoryArray.Count, (dataHistoryArray.GetItem(i) - MinValue) * Height / range);
				}
				else
				{
					linesToDrawStorage.LineTo(i + Width - dataHistoryArray.Count, (dataHistoryArray.GetItem(i) - MinValue) * Height / range);
				}
			}

			graphics2D.Render(new Stroke(linesToDrawStorage), LineColor);

			base.OnDraw(graphics2D);
		}

		public void Reset()
		{
			dataHistoryArray.Reset();
		}

        public void AddData(List<int> salesData)
        {
            foreach (var data in salesData)
            {
                AddData(data);
            }
        }

        internal class HistoryData
		{
			internal double currentDataSum;
			private readonly int capacity;
			private readonly List<double> data;

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

			internal void Add(double value)
			{
				if (data.Count == capacity)
				{
					currentDataSum -= data[0];
					data.RemoveAt(0);
				}

				data.Add(value);

				currentDataSum += value;
			}

			internal double GetAverageValue()
			{
				return currentDataSum / data.Count;
			}

			internal double GetItem(int itemIndex)
			{
				if (itemIndex < data.Count)
				{
					return data[itemIndex];
				}
				else
				{
					return 0;
				}
			}

			internal double GetMaxValue()
			{
				double max = -double.MinValue;
				for (int i = 0; i < data.Count; i++)
				{
					if (data[i] > max)
					{
						max = data[i];
					}
				}

				return max;
			}

			internal double GetMinValue()
			{
				double min = double.MaxValue;
				for (int i = 0; i < data.Count; i++)
				{
					if (data[i] < min)
					{
						min = data[i];
					}
				}

				return min;
			}

			internal void Reset()
			{
				currentDataSum = 0;
				data.Clear();
			}
		}
	}
}