using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;


namespace MatterHackers.MatterControl
{
    public class SlidePanel : GuiWidget
    {
        public List<GuiWidget> pannels = new List<GuiWidget>();

        int currentPannelIndex = -1;
        int desiredPannelIndex = 0;
        Stopwatch timeHasBeenChanging = new Stopwatch();

        public SlidePanel(int count)
        {
            this.AnchorAll();

            for (int i = 0; i < count; i++)
            {
                GuiWidget newPannel = new FlowLayoutWidget(FlowDirection.TopToBottom);
                pannels.Add(newPannel);
                AddChild(newPannel);
            }
        }

        public int PannelIndex
        {
            get
            {
                return currentPannelIndex;
            }

            set
            {
                if (currentPannelIndex != value)
                {
                    desiredPannelIndex = value;
                    timeHasBeenChanging.Restart();
                    SetSlidePosition();
                }
            }
        }

        public void SetPannelIndexImediate(int index)
        {
            desiredPannelIndex = index;
            SetSlidePosition();
        }

        public GuiWidget GetPannel(int index)
        {
            return pannels[index];
        }

        public override void OnBoundsChanged(EventArgs e)
        {
            for (int i = 0; i < pannels.Count; i++)
            {
                pannels[i].LocalBounds = LocalBounds;
            }
            SetSlidePosition();
            base.OnBoundsChanged(e);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
            if (currentPannelIndex != desiredPannelIndex)
            {
                SetSlidePosition();
                Invalidate();
            }
        }

        void SetSlidePosition()
        {
            if (currentPannelIndex != desiredPannelIndex)
            {
                // set this based on the time that has elapsed and it should give us a nice result (we can even ease in ease out)
                double maxOffsetPerDraw = timeHasBeenChanging.ElapsedMilliseconds;

                double desiredOffset = desiredPannelIndex * -Width;
                double currentOffset = pannels[0].OriginRelativeParent.x;
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

                for (int i = 0; i < pannels.Count; i++)
                {
                    pannels[i].OriginRelativeParent = new Vector2(offsetThisDraw, 0);
                    offsetThisDraw += Width;
                }

                if (currentOffset + delta == desiredOffset)
                {
                    currentPannelIndex = desiredPannelIndex;
                }
            }
            else
            {
                double desiredOffset = desiredPannelIndex * -Width;
                for (int i = 0; i < pannels.Count; i++)
                {
                    pannels[i].OriginRelativeParent = new Vector2(desiredOffset, 0);
                    desiredOffset += Width;
                }
            }
        }
    }
}
