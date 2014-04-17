/*
Copyright (c) 2014, Kevin Pope
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ActionBar
{
    class TemperatureWidgetBase : GuiWidget
    {
        TextWidget indicatorTextWidget;
        RGBA_Bytes borderColor = new RGBA_Bytes(255, 255, 255);
        int borderWidth = 2;

        public string IndicatorValue
        {
            get
            {
                return indicatorTextWidget.Text;
            }
            set
            {
                if (indicatorTextWidget.Text != value)
                {
                    indicatorTextWidget.Text = value;
                }
            }
        }

        event EventHandler unregisterEvents;
        public TemperatureWidgetBase(string textValue)
            : base(52, 52)
        {
            this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 200);
            indicatorTextWidget = new TextWidget(textValue, pointSize: 11);
            indicatorTextWidget.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
            indicatorTextWidget.HAnchor = HAnchor.ParentCenter;
            indicatorTextWidget.VAnchor = VAnchor.ParentCenter;
            indicatorTextWidget.AutoExpandBoundsToText = true;
            this.Margin = new BorderDouble(0, 2);
            this.AddChild(indicatorTextWidget);
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
            this.indicatorTextWidget.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
            this.Invalidate();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);

            RectangleDouble Bounds = LocalBounds;
            RoundedRect borderRect = new RoundedRect(this.LocalBounds, 0);
            Stroke strokeRect = new Stroke(borderRect, borderWidth);
            graphics2D.Render(strokeRect, borderColor);
        }
    }
}
