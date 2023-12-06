/*
Copyright (c) 2023, Lars Brubaker
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
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterControlLib.PartPreviewWindow.View3D.GeometryNodes
{
    public class NodeEditor : GuiWidget
    {
        public enum ETransformState
        {
            Edit,
            Move,
            Scale
        };

        private Vector2 lastMousePosition = new Vector2(0, 0);
        private ETransformState mouseDownTransformOverride;
        private Vector2 mouseDownPosition = new Vector2(0, 0);
        private Action<Vector2, double> scaleChanged;
        private ThemeConfig theme;

        private Vector2 unscaledRenderOffset = new Vector2(0, 0);

        public NodeEditor(View3DWidget view3DWidget,
            ThemeConfig theme,
            ref int tabIndex,
            Vector2 unscaledRenderOffset = default,
            double layerScale = 1,
            Action<Vector2, double> scaleChanged = null)

        {
            HAnchor = HAnchor.Stretch;
            VAnchor = VAnchor.Stretch;

            this.view3DWidget = view3DWidget;

            // remember the initial tab index
            initialTabIndex = tabIndex;
            // and add to the tab index the number of controls we plan to add
            tabIndex += 2;

            BackgroundOutlineWidth = 1;
            BackgroundColor = theme.BackgroundColor;
            BorderColor = theme.TextColor;
            Margin = 1;

            this.unscaledRenderOffset = unscaledRenderOffset;
            this.layerScale = layerScale;

            var topToBottom = this;

            this.scaleChanged = scaleChanged;

            this.theme = theme;

            var toolBar = new FlowLayoutWidget()
            {
                HAnchor = HAnchor.Stretch,
                Margin = 5,
                BackgroundColor = theme.TextColor.WithAlpha(20),
            };

            toolBar.VAnchor |= VAnchor.Bottom;

            AddChild(toolBar);

            AddControlsToToolBar(theme, toolBar);

            view3DWidget.Scene.SelectionChanged += Scene_SelectionChanged;
        }

        private void AddControlsToToolBar(ThemeConfig theme, FlowLayoutWidget toolBar)
        {
            AddButtons(theme, toolBar);

            toolBar.AddChild(new HorizontalSpacer());
        }

        public static readonly int VectorXYEditWidth = (int)(60 * DeviceScale + .5);

        private void AddButtons(ThemeConfig theme, FlowLayoutWidget toolBar)
        {
            var homeButton = new ThemedIconButton(StaticData.Instance.LoadIcon("fa-home_16.png", 16, 16).GrayToColor(theme.TextColor), theme)
            {
                BackgroundColor = theme.SlightShade,
                HoverColor = theme.SlightShade.WithAlpha(75),
                Margin = new BorderDouble(3, 3, 6, 3),
                ToolTipText = "Reset Zoom".Localize()
            };
            toolBar.AddChild(homeButton);

            homeButton.Click += (s, e) =>
            {
                CenterPartInView();
            };
        }

        public ETransformState TransformState { get; set; }
        private double layerScale { get; set; } = 1;

        private UndoBuffer UndoBuffer => view3DWidget.Scene.UndoBuffer;

        private Affine ScalingTransform => Affine.NewScaling(layerScale, layerScale);
        public Affine TotalTransform => Affine.NewTranslation(unscaledRenderOffset) * ScalingTransform * Affine.NewTranslation(Width / 2, Height / 2);

        private View3DWidget view3DWidget;
        private int initialTabIndex;
        private bool hasBeenStartupPositioned;
        private NodesObject3D geometryNodes;

        public void CenterPartInView()
        {
            var ready = true;
            if (ready)
            {
                var partBounds = new RectangleDouble(0, 0, 100, 100);
                var weightedCenter = partBounds.Center;

                var bottomPixels = 20;
                var margin = 30;
                unscaledRenderOffset = -weightedCenter;
                layerScale = Math.Min((Height - margin - bottomPixels * 2) / partBounds.Height, (Width - margin) / partBounds.Width);
                unscaledRenderOffset += new Vector2(0, bottomPixels) / layerScale;

                Invalidate();
                scaleChanged?.Invoke(unscaledRenderOffset, layerScale);
            }
        }

        public override void OnClosed(EventArgs e)
        {
            view3DWidget.Scene.SelectionChanged -= Scene_SelectionChanged;
            base.OnClosed(e);
        }

        private void Scene_SelectionChanged(object sender, EventArgs e)
        {
            var selectedItem = view3DWidget.Scene.SelectedItem;

            // Change tree selection to current node
            if (selectedItem != null
                && selectedItem is NodesObject3D geometryNodes)
            {
                this.geometryNodes = geometryNodes;
            }
            else
            {
                // Clear the editor of any controls
                this.geometryNodes = null;
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            if (!hasBeenStartupPositioned)
            {
                if (unscaledRenderOffset == Vector2.Zero
                    && layerScale == 1)
                {
                    CenterPartInView();
                }
                hasBeenStartupPositioned = true;
            }

            var drawAxis = true;
            if (drawAxis)
            {
                var leftOrigin = new Vector2(-10000, 0);
                var rightOrigin = new Vector2(10000, 0);
                graphics2D.Line(TotalTransform.Transform(leftOrigin), TotalTransform.Transform(rightOrigin), Color.Red);
                var bottomOrigin = new Vector2(0, -10000);
                var topOrigin = new Vector2(0, 10000);
                graphics2D.Line(TotalTransform.Transform(bottomOrigin), TotalTransform.Transform(topOrigin), Color.Green);
            }

            if(geometryNodes != null)
            {
                foreach (var node in geometryNodes.Nodes)
                {
                    var position = TotalTransform.Transform(node.Position);
                    graphics2D.Circle(position, TotalTransform.GetScale() * 5, Color.Red);
                }
            }

            graphics2D.PushTransform();
            Affine currentGraphics2DTransform = graphics2D.GetTransform();
            Affine accumulatedTransform = currentGraphics2DTransform * TotalTransform;
            graphics2D.SetTransform(accumulatedTransform);
            base.OnDraw(graphics2D);
            graphics2D.PopTransform();
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            base.OnMouseDown(mouseEvent);
            if (MouseCaptured)
            {
                mouseDownPosition.X = mouseEvent.X;
                mouseDownPosition.Y = mouseEvent.Y;

                lastMousePosition = mouseDownPosition;

                mouseDownTransformOverride = TransformState;

                // check if not left button
                switch (mouseEvent.Button)
                {
                    case MouseButtons.Left:
                        if (Keyboard.IsKeyDown(Keys.ControlKey))
                        {
                            if (Keyboard.IsKeyDown(Keys.Alt))
                            {
                                mouseDownTransformOverride = ETransformState.Scale;
                            }
                            else
                            {
                                mouseDownTransformOverride = ETransformState.Move;
                            }
                        }
                        else
                        {
                            // we are in edit mode, check if we are over any control points
                        }
                        break;

                    case MouseButtons.Middle:
                    case MouseButtons.Right:
                        if (Keyboard.IsKeyDown(Keys.ControlKey))
                        {
                            mouseDownTransformOverride = ETransformState.Scale;
                        }
                        else
                        {
                            mouseDownTransformOverride = ETransformState.Move;
                        }
                        break;
                }
            }
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            base.OnMouseMove(mouseEvent);

            if (MouseCaptured)
            {
                DoTranslateAndZoom(mouseEvent);
            }
            else
            {
                // highlight any contorl points we are over
            }

            lastMousePosition = mouseEvent.Position;
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            base.OnMouseUp(mouseEvent);
        }

        private void DoTranslateAndZoom(MouseEventArgs mouseEvent)
        {
            var mousePos = new Vector2(mouseEvent.X, mouseEvent.Y);
            var mouseDelta = mousePos - lastMousePosition;

            switch (mouseDownTransformOverride)
            {
                case ETransformState.Scale:
                    double zoomDelta = 1;
                    if (mouseDelta.Y < 0)
                    {
                        zoomDelta = 1 - (-1 * mouseDelta.Y / 100);
                    }
                    else if (mouseDelta.Y > 0)
                    {
                        zoomDelta = 1 + 1 * mouseDelta.Y / 100;
                    }

                    var mousePreScale = mouseDownPosition;
                    TotalTransform.inverse_transform(ref mousePreScale);

                    layerScale *= zoomDelta;

                    var mousePostScale = mouseDownPosition;
                    TotalTransform.inverse_transform(ref mousePostScale);

                    unscaledRenderOffset += mousePostScale - mousePreScale;
                    scaleChanged?.Invoke(unscaledRenderOffset, layerScale);
                    break;

                case ETransformState.Move:
                    ScalingTransform.inverse_transform(ref mouseDelta);

                    unscaledRenderOffset += mouseDelta;
                    scaleChanged?.Invoke(unscaledRenderOffset, layerScale);
                    break;

                case ETransformState.Edit:
                default: // also treat everything else like an edit
                    break;
            }

            Invalidate();
        }

        public override void OnMouseWheel(MouseEventArgs mouseEvent)
        {
            base.OnMouseWheel(mouseEvent);
            if (FirstWidgetUnderMouse) // TODO: find a good way to decide if you are what the wheel is trying to do
            {
                const double deltaFor1Click = 120;
                double scaleAmount = mouseEvent.WheelDelta / deltaFor1Click * .1;

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

            unscaledRenderOffset += mousePostScale - mousePreScale;

            scaleChanged?.Invoke(unscaledRenderOffset, layerScale);
        }
    }
}