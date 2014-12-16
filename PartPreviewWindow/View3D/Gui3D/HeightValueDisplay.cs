﻿/*
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

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class HeightValueDisplay : GuiWidget
    {
        View3DWidget view3DWidget;
        TextWidget numberDisplay;

        static readonly int HorizontalLineLength = 30;

        public HeightValueDisplay(View3DWidget view3DWidget)
        {
            BackgroundColor = new RGBA_Bytes(RGBA_Bytes.White, 150);
            this.view3DWidget = view3DWidget;
            view3DWidget.meshViewerWidget.AddChild(this);
            numberDisplay = new TextWidget("00.00", pointSize:8);
            numberDisplay.Margin = new BorderDouble(3, 2);
            numberDisplay.AutoExpandBoundsToText = true;
            AddChild(numberDisplay);
            VAnchor = VAnchor.FitToChildren;
            HAnchor = HAnchor.FitToChildren;

            MeshViewerToDrawWith.TrackballTumbleWidget.DrawGlContent += TrackballTumbleWidget_DrawGlContent;
            MeshViewerToDrawWith.DrawAfter += new DrawEventHandler(MeshViewerToDrawWith_Draw);
        }

        MeshViewerWidget MeshViewerToDrawWith { get { return view3DWidget.meshViewerWidget; } }

        public void SetPosition()
        {
            if (MeshViewerToDrawWith.HaveSelection)
            {
                // draw the hight from the bottom to the bed
                AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.GetBoundsForSelection();

                Vector2 screenPosition = new Vector2(-100, 0);
                if (view3DWidget.DisplayAllValueData)
                {
                    screenPosition = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(new Vector3(selectedBounds.maxXYZ.x, selectedBounds.minXYZ.y, selectedBounds.minXYZ.z));
                    numberDisplay.Text = "{0:0.00}".FormatWith(selectedBounds.minXYZ.z);
                }
                else
                {
                    Vector3[] bottomPoints = new Vector3[4];
                    bottomPoints[0] = new Vector3(selectedBounds.minXYZ.x, selectedBounds.minXYZ.y, selectedBounds.minXYZ.z);
                    bottomPoints[1] = new Vector3(selectedBounds.minXYZ.x, selectedBounds.maxXYZ.y, selectedBounds.minXYZ.z);
                    bottomPoints[2] = new Vector3(selectedBounds.maxXYZ.x, selectedBounds.minXYZ.y, selectedBounds.minXYZ.z);
                    bottomPoints[3] = new Vector3(selectedBounds.maxXYZ.x, selectedBounds.maxXYZ.y, selectedBounds.minXYZ.z);
                    
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 testScreenPosition = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(bottomPoints[i]);
                        if (testScreenPosition.x > screenPosition.x)
                        {
                            startLineSelectionPos = testScreenPosition;
                            startLineGroundPos = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(bottomPoints[i] + new Vector3(0, 0, -bottomPoints[i].z));
                            screenPosition = testScreenPosition + new Vector2(HorizontalLineLength, 0);
                        }
                    }
                    numberDisplay.Text = "{0:0.00mm}".FormatWith(selectedBounds.minXYZ.z);
                }

                OriginRelativeParent = screenPosition;

            }
        }

        Vector2 startLineGroundPos;
        Vector2 startLineSelectionPos;

        void MeshViewerToDrawWith_Draw(GuiWidget drawingWidget, DrawEventArgs drawEvent)
        {
            if (Visible)
            {
                if (drawEvent != null)
                {
                    // draw the line that is on the ground
                    double yGround = (int)(startLineGroundPos.y + .5) + .5;
                    drawEvent.graphics2D.Line(startLineGroundPos.x, yGround, startLineGroundPos.x + HorizontalLineLength - 5, yGround, RGBA_Bytes.Black);
                    // and the line that is at the base of the selection
                    double ySelection = (int)(startLineSelectionPos.y + .5) + .5;
                    drawEvent.graphics2D.Line(startLineSelectionPos.x, ySelection, startLineSelectionPos.x + HorizontalLineLength - 5, ySelection, RGBA_Bytes.Black);

                    // draw the verticle line that shows the measurment
                    Vector2 pointerBottom = new Vector2(startLineGroundPos.x + HorizontalLineLength / 2, yGround);
                    Vector2 pointerTop = new Vector2(startLineSelectionPos.x + HorizontalLineLength / 2, ySelection);
                    drawEvent.graphics2D.Line(pointerBottom, pointerTop, RGBA_Bytes.Black);

                    Vector2 direction = pointerTop - pointerBottom;
                    if (direction.LengthSquared > 0)
                    {
                        PathStorage arrow = new PathStorage();
                        arrow.MoveTo(-3, -5);
                        arrow.LineTo(0, 0);
                        arrow.LineTo(3, -5);
                        double rotation = Math.Atan2(direction.y, direction.x);
                        IVertexSource correctRotation = new VertexSourceApplyTransform(arrow, Affine.NewRotation(rotation - MathHelper.Tau / 4));
                        IVertexSource inPosition = new VertexSourceApplyTransform(correctRotation, Affine.NewTranslation(pointerTop));
                        drawEvent.graphics2D.Render(inPosition, RGBA_Bytes.Black);
                    }
                }
            }
        }

        void TrackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
        {
            if (Visible)
            {
                if (view3DWidget.DisplayAllValueData)
                {
                }
                else
                {
                }
            }
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            base.OnMouseDown(mouseEvent);
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            base.OnMouseMove(mouseEvent);
        }
    }
}
