using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MatterHackers.MatterControl
{
	public class SlidePanel : GuiWidget
	{
		public List<GuiWidget> panels = new List<GuiWidget>();

		private int currentPanelIndex = -1;
		private int desiredPanelIndex = 0;
		private Stopwatch timeHasBeenChanging = new Stopwatch();

		public SlidePanel(int count)
		{
			this.AnchorAll();

			for (int i = 0; i < count; i++)
			{
				GuiWidget newPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
				panels.Add(newPanel);
				AddChild(newPanel);
			}
		}

		public int PanelIndex
		{
			get
			{
				return currentPanelIndex;
			}

			set
			{
				if (currentPanelIndex != value)
				{
					desiredPanelIndex = value;
					timeHasBeenChanging.Restart();
					SetSlidePosition();
				}
			}
		}

		public void SetPanelIndexImmediate(int index)
		{
			desiredPanelIndex = index;
			SetSlidePosition();
		}

		public GuiWidget GetPanel(int index)
		{
			return panels[index];
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			for (int i = 0; i < panels.Count; i++)
			{
				panels[i].LocalBounds = LocalBounds;
			}
			SetSlidePosition();
			base.OnBoundsChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			if (currentPanelIndex != desiredPanelIndex)
			{
				SetSlidePosition();
				Invalidate();
			}
		}

		private void SetSlidePosition()
		{
			if (currentPanelIndex != desiredPanelIndex)
			{
				// set this based on the time that has elapsed and it should give us a nice result (we can even ease in ease out)
				double maxOffsetPerDraw = timeHasBeenChanging.ElapsedMilliseconds;

				double desiredOffset = desiredPanelIndex * -Width;
				double currentOffset = panels[0].OriginRelativeParent.X;
				double delta = desiredOffset - currentOffset;
				if (delta < 0)
				{
					delta = Math.Max(-maxOffsetPerDraw, delta);
				}
				else
				{
					delta = Math.Min(maxOffsetPerDraw, delta);
				}

				double offsetThisDraw = currentOffset + delta;

				for (int i = 0; i < panels.Count; i++)
				{
					panels[i].OriginRelativeParent = new Vector2(offsetThisDraw, 0);
					offsetThisDraw += Width;
				}

				if (currentOffset + delta == desiredOffset)
				{
					currentPanelIndex = desiredPanelIndex;
				}
			}
			else
			{
				double desiredOffset = desiredPanelIndex * -Width;
				for (int i = 0; i < panels.Count; i++)
				{
					panels[i].OriginRelativeParent = new Vector2(desiredOffset, 0);
					desiredOffset += Width;
				}
			}
		}
	}
}