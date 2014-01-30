/*
Copyright (c) 2013, Lars Brubaker
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
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.SerialPortConnecton;

namespace MatterHackers.MatterControl
{    
    public class SDCardManager : SystemWindow
    {
        static SDCardManager connectionWindow = null;
        static bool sdCardWindowIsOpen = false;
        public static void Show()
        {
            if (sdCardWindowIsOpen == false)
            {
                connectionWindow = new SDCardManager();
                sdCardWindowIsOpen = true;
                connectionWindow.Closed += (parentSender, e) =>
                {
                    sdCardWindowIsOpen = false;
                    connectionWindow = null;
                };
            }
            else
            {
                if (connectionWindow != null)
                {
                    connectionWindow.BringToFront();
                }
            }
        }

        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        
        private SDCardManager()
            : base(400, 300)
        {
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.AnchorAll();

            Title = "MatterControl - SD Card Manager";
            this.ShowAsSystemWindow();
            MinimumSize = new Vector2(Width, Height);

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            FlowLayoutWidget topButtons = new FlowLayoutWidget();

            topButtons.AddChild(textImageButtonFactory.Generate("Upload"));
            topButtons.AddChild(textImageButtonFactory.Generate("Delete"));
            topButtons.AddChild(textImageButtonFactory.Generate("New Folder"));
            topButtons.AddChild(textImageButtonFactory.Generate("Print"));
            topButtons.AddChild(textImageButtonFactory.Generate("Stop"));
            topButtons.AddChild(textImageButtonFactory.Generate("Mount"));
            topButtons.AddChild(textImageButtonFactory.Generate("Unmount"));

            topToBottom.AddChild(topButtons);

            this.AddChild(topToBottom);
        }

        event EventHandler unregisterEvents;

        public override void OnClosed(EventArgs e)
        {
            // make sure we are not holding onto this window (keeping a pointer that can't be garbage collected).
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }
    }
}
