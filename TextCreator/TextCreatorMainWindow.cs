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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.Plugins.TextCreator
{
    public class TextCreatorMainWindow : SystemWindow
    {
        View3DTextCreator part3DView;

        public TextCreatorMainWindow()
            : base(690, 340)
        {
            Title = "MatterControl: Text Creator";

            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            double buildHeight = ActiveSliceSettings.Instance.BuildHeight;

            part3DView = new View3DTextCreator(
                new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight), 
                ActiveSliceSettings.Instance.BedCenter,
                ActiveSliceSettings.Instance.BedShape);

#if __ANDROID__
			this.AddChild(new SoftKeyboardContentOffset(part3DView, SoftKeyboardContentOffset.AndroidKeyboardOffset));
#else
            this.AddChild(part3DView);
#endif

			this.AnchorAll();

            part3DView.Closed += (sender, e) => 
			{
				Close(); 
			};

            Width = 640;
            Height = 480;

            ShowAsSystemWindow();
            MinimumSize = new Vector2(400, 300);
        }
    }
}
