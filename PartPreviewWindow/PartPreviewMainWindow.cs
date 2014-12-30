/*
Copyright (c) 2014, Lars Brubaker
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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class PartPreviewMainWindow : SystemWindow
    {
        event EventHandler unregisterEvents;
        PartPreviewContent partPreviewWidget;

        public PartPreviewMainWindow(PrintItemWrapper printItem, View3DWidget.AutoRotate autoRotate3DView, View3DWidget.OpenMode openMode = View3DWidget.OpenMode.Viewing)
            : base(690, 340)
        {
            UseOpenGL = true;
            string partPreviewTitle = LocalizedString.Get("MatterControl");
            Title = string.Format("{0}: ", partPreviewTitle) + Path.GetFileName(printItem.Name);

            partPreviewWidget = new PartPreviewContent(printItem, View3DWidget.WindowMode.StandAlone, autoRotate3DView, openMode);
            partPreviewWidget.Closed += (sender, e) => 
            {
                Close(); 
            };

			this.AddChild(partPreviewWidget);

            AddHandlers();

            Width = 640;
            Height = 480;

            MinimumSize = new Vector2(400, 300);
            ShowAsSystemWindow();
        }

#if __ANDROID__
        Stopwatch totalDrawTime = new Stopwatch();
        AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();
        public override void OnDraw(Graphics2D graphics2D)
        {
            totalDrawTime.Restart();
            base.OnDraw(graphics2D);
            totalDrawTime.Stop();

            millisecondTimer.Update((int)totalDrawTime.ElapsedMilliseconds);
            millisecondTimer.DrawTopCenter(this, graphics2D);
        }
#endif

        private void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
        }

        public void ThemeChanged(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }
    }
}
