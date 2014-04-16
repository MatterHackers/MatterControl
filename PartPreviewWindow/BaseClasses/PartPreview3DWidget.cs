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
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class PartPreview3DWidget : PartPreviewWidget
    {
        protected MeshViewerWidget meshViewerWidget;

        public PartPreview3DWidget()
        {
        }

        protected void Add3DViewControls()
        {
            FlowLayoutWidget transformTypeSelector = new FlowLayoutWidget();
            transformTypeSelector.BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);
            textImageButtonFactory.FixedHeight = 20;
            textImageButtonFactory.FixedWidth = 20;
            textImageButtonFactory.AllowThemeToAdjustImage = false;

            string rotateIconPath = Path.Combine("Icons", "ViewTransformControls", "rotate.png");
            rotateViewButton = textImageButtonFactory.GenerateRadioButton("", rotateIconPath);
            rotateViewButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(rotateViewButton);
            rotateViewButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
            };

            string translateIconPath = Path.Combine("Icons", "ViewTransformControls", "translate.png");
            translateButton = textImageButtonFactory.GenerateRadioButton("", translateIconPath);
            translateButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(translateButton);
            translateButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Translation;
            };

            string scaleIconPath = Path.Combine("Icons", "ViewTransformControls", "scale.png");
            RadioButton scaleButton = textImageButtonFactory.GenerateRadioButton("", scaleIconPath);
            scaleButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(scaleButton);
            scaleButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Scale;
            };

            viewControlsSeparator = new GuiWidget(2, 32);
            viewControlsSeparator.BackgroundColor = RGBA_Bytes.White;
            viewControlsSeparator.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(viewControlsSeparator);

            string partSelectIconPath = Path.Combine("Icons", "ViewTransformControls", "partSelect.png");
            partSelectButton = textImageButtonFactory.GenerateRadioButton("", partSelectIconPath);
            partSelectButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(partSelectButton);
            partSelectButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.None;
            };

            transformTypeSelector.Margin = new BorderDouble(5);
            transformTypeSelector.HAnchor |= Agg.UI.HAnchor.ParentLeft;
            transformTypeSelector.VAnchor = Agg.UI.VAnchor.ParentTop;
            AddChild(transformTypeSelector);
            rotateViewButton.Checked = true;
        }
    }
}
