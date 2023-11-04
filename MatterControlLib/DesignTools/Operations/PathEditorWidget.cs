/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System;
using static MatterHackers.MatterControl.PartPreviewWindow.GCode2DWidget;

namespace MatterHackers.MatterControl.DesignTools
{
    public class PathEditorWidget : GuiWidget
    {
        private Vector2 lastMousePosition = new Vector2(0, 0);
        private Vector2 mouseDownPosition = new Vector2(0, 0);
        private Action<Vector2, double> scaleChanged;
        private ThemeConfig theme;

        private Vector2 unscaledRenderOffset = new Vector2(0, 0);
        private Action vertexChanged;

        private VertexStorage vertexStorage;

        public PathEditorWidget(VertexStorage vertexStorage,
            UndoBuffer undoBuffer,
            ThemeConfig theme,
            Action vertexChanged,
            Vector2 unscaledRenderOffset = default(Vector2),
            double layerScale = 1,
            Action<Vector2, double> scaleChanged = null)
        {
            HAnchor = HAnchor.Stretch;
            BackgroundOutlineWidth = 1;
            BackgroundColor = theme.BackgroundColor;
            BorderColor = theme.TextColor;
            Margin = 1;

            this.unscaledRenderOffset = unscaledRenderOffset;
            this.layerScale = layerScale;

            var topToBottom = this;

            this.scaleChanged = scaleChanged;

            SizeChanged += (s, e) =>
            {
                Height = Width / 2;
            };

            this.vertexChanged = vertexChanged;
            this.theme = theme;
            this.vertexStorage = vertexStorage;

            var toolBar = new FlowLayoutWidget()
            {
                HAnchor = HAnchor.Stretch,
                VAnchor = VAnchor.Bottom
            };

            this.AddChild(toolBar);

            var menuTheme = ApplicationController.Instance.MenuTheme;
            var homeButton = new ThemedTextIconButton("Home".Localize(), StaticData.Instance.LoadIcon("fa-home_16.png", 16, 16).GrayToColor(menuTheme.TextColor), theme)
            {
                BackgroundColor = theme.SlightShade,
                HoverColor = theme.SlightShade.WithAlpha(75),
                Margin = new BorderDouble(3, 3, 6, 3),
                ToolTipText = "Reset Zoom".Localize()
            };
            toolBar.AddChild(homeButton);

            homeButton.Click += (s, e) =>
            {
                UiThread.RunOnIdle(() =>
                {
                    ApplicationController.LaunchBrowser("https://www.matterhackers.com/store/c/3d-printer-filament");
                });
            };
        }

        public ETransformState TransformState { get; set; }
        private double layerScale { get; set; } = 1;

        private Affine ScalingTransform => Affine.NewScaling(layerScale, layerScale);
        private Affine TotalTransform => Affine.NewTranslation(unscaledRenderOffset) * ScalingTransform * Affine.NewTranslation(Width / 2, Height / 2);

        public override void OnDraw(Graphics2D graphics2D)
        {
            new VertexSourceApplyTransform(vertexStorage, TotalTransform).RenderCurve(graphics2D, theme.TextColor, 2, true, theme.PrimaryAccentColor.Blend(theme.TextColor, .5), theme.PrimaryAccentColor);

            base.OnDraw(graphics2D);
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            base.OnMouseDown(mouseEvent);
            if (MouseCaptured)
            {
                mouseDownPosition.X = mouseEvent.X;
                mouseDownPosition.Y = mouseEvent.Y;

                lastMousePosition = mouseDownPosition;
            }
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            base.OnMouseMove(mouseEvent);
            var mousePos = new Vector2(mouseEvent.X, mouseEvent.Y);

            if (MouseCaptured)
            {
                var mouseDelta = mousePos - lastMousePosition;
                switch (TransformState)
                {
                    case ETransformState.Scale:
                        double zoomDelta = 1;
                        if (mouseDelta.Y < 0)
                        {
                            zoomDelta = 1 - (-1 * mouseDelta.Y / 100);
                        }
                        else if (mouseDelta.Y > 0)
                        {
                            zoomDelta = 1 + (1 * mouseDelta.Y / 100);
                        }

                        var mousePreScale = mouseDownPosition;
                        TotalTransform.inverse_transform(ref mousePreScale);

                        layerScale *= zoomDelta;

                        var mousePostScale = mouseDownPosition;
                        TotalTransform.inverse_transform(ref mousePostScale);

                        unscaledRenderOffset += (mousePostScale - mousePreScale);
                        scaleChanged?.Invoke(unscaledRenderOffset, layerScale);
                        break;

                    case ETransformState.Move:
                    default: // also treat everything else like a move
                        ScalingTransform.inverse_transform(ref mouseDelta);

                        unscaledRenderOffset += mouseDelta;
                        scaleChanged?.Invoke(unscaledRenderOffset, layerScale);
                        break;
                }

                Invalidate();
            }

            lastMousePosition = mousePos;
        }

        public override void OnMouseWheel(MouseEventArgs mouseEvent)
        {
            base.OnMouseWheel(mouseEvent);
            if (FirstWidgetUnderMouse) // TODO: find a good way to decide if you are what the wheel is trying to do
            {
                const double deltaFor1Click = 120;
                double scaleAmount = (mouseEvent.WheelDelta / deltaFor1Click) * .1;

                ScalePartAndFixPosition(mouseEvent, layerScale + layerScale * scaleAmount);

                mouseEvent.WheelDelta = 0;

                Invalidate();
            }
        }

        public void Zoom(double scaleAmount)
        {
            ScalePartAndFixPosition(new MouseEventArgs(MouseButtons.None, 0, Width / 2, Height / 2, 0), layerScale * scaleAmount);
            Invalidate();
        }

        private void ScalePartAndFixPosition(MouseEventArgs mouseEvent, double scaleAmount)
        {
            var mousePreScale = new Vector2(mouseEvent.X, mouseEvent.Y);
            TotalTransform.inverse_transform(ref mousePreScale);

            layerScale = scaleAmount;

            var mousePostScale = new Vector2(mouseEvent.X, mouseEvent.Y);
            TotalTransform.inverse_transform(ref mousePostScale);

            unscaledRenderOffset += (mousePostScale - mousePreScale);

            scaleChanged?.Invoke(unscaledRenderOffset, layerScale);
        }
    }
}