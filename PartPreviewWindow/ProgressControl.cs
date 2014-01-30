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
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.MarchingSquares;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class ProgressControl : FlowLayoutWidget
    {
        GuiWidget bar;
        public TextWidget textWidget;

        int percentComplete;
        public int PercentComplete
        {
            get { return percentComplete; }
            set
            {
                if (value != percentComplete)
                {
                    percentComplete = value;
                    Invalidate();
                }
            }
        }

        public ProgressControl(string message)
        {
            textWidget = new TextWidget(message, textColor: RGBA_Bytes.White);
            textWidget.AutoExpandBoundsToText = true;
            textWidget.Margin = new BorderDouble(5, 0);
            AddChild(textWidget);
            bar = new GuiWidget(80, 15);
            bar.VAnchor = Agg.UI.VAnchor.ParentCenter;
            AddChild(bar);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            RectangleDouble barBounds = bar.BoundsRelativeToParent;
            graphics2D.FillRectangle(barBounds.Left, barBounds.Bottom, barBounds.Left + barBounds.Width * PercentComplete / 100.0, barBounds.Top, ActiveTheme.Instance.PrimaryAccentColor);
            graphics2D.Rectangle(barBounds, RGBA_Bytes.Black);
            base.OnDraw(graphics2D);
        }
    }
}
