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
    public class PrintProgressBar : GuiWidget
    {
        double currentPercent = 0;
        RGBA_Bytes completeColor = new RGBA_Bytes(255, 255, 255);

        public PrintProgressBar()
        {
            MinimumSize = new Vector2(0, 10);
            HAnchor = HAnchor.ParentLeftRight;
            BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
            Margin = new BorderDouble(0);

            AddHandlers();
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            PrinterCommunication.Instance.WroteLine.RegisterEvent(Instance_WroteLine, ref unregisterEvents);
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            //Set background color to new theme
            this.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
            this.Invalidate();
        }

        Stopwatch timeSinceLastUpdate = new Stopwatch();
        void Instance_WroteLine(object sender, EventArgs e)
        {
            if (!timeSinceLastUpdate.IsRunning)
            {
                timeSinceLastUpdate.Start();
            }

            if (timeSinceLastUpdate.ElapsedMilliseconds > 5000)
            {
                timeSinceLastUpdate.Restart();
                currentPercent = PrinterCommunication.Instance.PercentComplete;
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
            graphics2D.FillRectangle(0, 0, Width * currentPercent / 100, Height, completeColor);            
        }
    }
}
