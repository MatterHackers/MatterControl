using System;
using System.Collections.Generic;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class SlideWidget : GuiWidget
    {
        Stopwatch timeHasBeenChanging = new Stopwatch();

        public SlideWidget()
            :base(0.0,0.0)
        {
            
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
            if (this.Width != TargetWidth)
            {
                SetSlidePosition();
                Invalidate();
            }
        }

        double OriginalWidth { get; set; }
        double TargetWidth { get; set; }

        public void SlideIn()
        {
            this.Visible = true;
            if (OriginalWidth == 0)
            {
                this.OriginalWidth = this.Width;
            }
            this.TargetWidth = this.OriginalWidth;
            this.Width = 0.1;
            timeHasBeenChanging.Restart();
            SetSlidePosition();
            Invalidate();
        }

        public void SlideOut()
        {
            this.Visible = true;
            if (OriginalWidth == 0)
            {
                this.OriginalWidth = this.Width;
            }
            this.TargetWidth = 0;
            timeHasBeenChanging.Restart();
            SetSlidePosition();
            Invalidate();
        }

        void SetSlidePosition()
        {
            if (TargetWidth == 0 && this.Width == 0)
            {
                this.Visible = false;
                this.Width = this.OriginalWidth;
                
            }
            else if (this.TargetWidth != this.Width)
            {
                double maxOffsetPerDraw = timeHasBeenChanging.ElapsedMilliseconds;

                double currentWidth = this.Width;
                double delta = TargetWidth - currentWidth;
                if (delta < 0)
                {
                    delta = Math.Max(-maxOffsetPerDraw, delta);
                }
                else
                {
                    delta = Math.Min(maxOffsetPerDraw, delta);
                }

                double offsetThisDraw = currentWidth + delta;
                this.Width = offsetThisDraw;
            }
        }
    }
}
