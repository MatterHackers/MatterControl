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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using System;

namespace MatterControlLib.PartPreviewWindow.View3D.GeometryNodes
{
    public class NodeEditor : GuiWidget
    {
        public static readonly int VectorXYEditWidth = (int)(60 * DeviceScale + .5);

        public Action ScaleChanged;
        public Action UnscaledPositionChanged;
        private double _layerScale = 1;
        private Vector2 _unscaledRenderOffset = new Vector2(0, 0);
        private NodesObject3D geometryNodes;

        private bool hasBeenStartupPositioned;

        private Vector2 lastMousePosition = new Vector2(0, 0);

        Vector2 lastOriginRelativeToParent = new Vector2(0, 0);
        private Vector2 mouseDownPosition = new Vector2(0, 0);

        private ETransformState mouseDownTransformOverride;
        private ThemeConfig theme;
        private View3DWidget view3DWidget;
        public NodeEditor(View3DWidget view3DWidget,
            ThemeConfig theme,
            Vector2 unscaledRenderOffset = default,
            double layerScale = 1)
        {
            HAnchor = HAnchor.Stretch;
            VAnchor = VAnchor.Stretch;

            this.view3DWidget = view3DWidget;

            BackgroundOutlineWidth = 1;
            BackgroundColor = theme.BackgroundColor.WithAlpha(150);
            BorderColor = theme.TextColor;

            this.UnscaledRenderOffset = unscaledRenderOffset;
            this.LayerScale = layerScale;

            var topToBottom = this;

            this.theme = theme;

            var toolBar = new FlowLayoutWidget()
            {
                HAnchor = HAnchor.Stretch,
                Margin = 5,
                BackgroundColor = theme.TextColor.WithAlpha(20),
                Name = "ToolBar",
                Selectable = false,
            };

            toolBar.VAnchor |= VAnchor.Bottom;

            ScrollArea = new GuiWidget()
            {
                Name = "ScrollArea",
                DebugShowBounds = true,
                Selectable = false,
            };
            AddChild(ScrollArea);

            AddChild(toolBar);

            AddControlsToToolBar(theme, toolBar);

            view3DWidget.Scene.SelectionChanged += Scene_SelectionChanged;
        }

        public enum ETransformState
        {
            Edit,
            Move,
            Scale
        };

        public double LayerScale
        {
            get => _layerScale;
            set
            {
                if (_layerScale != value)
                {
                    _layerScale = value;
                    ScaleChanged?.Invoke();
                }
            }
        }

        public GuiWidget ScrollArea { get; set; }
        public Affine TotalTransform => Affine.NewTranslation(UnscaledRenderOffset) * ScalingTransform * Affine.NewTranslation(Width / 2, Height / 2);
        public ETransformState TransformState { get; set; }
        private UndoBuffer UndoBuffer => view3DWidget.Scene.UndoBuffer;
        public Vector2 UnscaledRenderOffset
        {
            get => _unscaledRenderOffset;
            set
            {
                if (_unscaledRenderOffset != value)
                {
                    _unscaledRenderOffset = value;
                    UnscaledPositionChanged?.Invoke();
                }
            }
        }

        private Affine ScalingTransform => Affine.NewScaling(LayerScale, LayerScale);
        public void CenterPartInView()
        {
            var ready = false;
            if (ready)
            {
                var partBounds = new RectangleDouble(0, 0, 100, 100);
                var weightedCenter = partBounds.Center;

                var oldOrigin = ScrollArea.OriginRelativeParent - UnscaledRenderOffset;
                var bottomPixels = 20;
                var margin = 30;
                UnscaledRenderOffset = -weightedCenter;
                LayerScale = Math.Min((Height - margin - bottomPixels * 2) * partBounds.Height, (Width - margin) * partBounds.Width);
                UnscaledRenderOffset += new Vector2(0, bottomPixels) * LayerScale;

                Invalidate();
            }
        }

        public override void OnBoundsChanged(EventArgs e)
        {
            if (ScrollArea != null)
            {
                // make sure the scroll area is the right size
                AdjustScrollArea();
            }

            base.OnBoundsChanged(e);
        }

        public override void OnClosed(EventArgs e)
        {
            view3DWidget.Scene.SelectionChanged -= Scene_SelectionChanged;
            base.OnClosed(e);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            if (!hasBeenStartupPositioned)
            {
                if (UnscaledRenderOffset == Vector2.Zero
                    && LayerScale == 1)
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

            if (geometryNodes != null)
            {
                foreach (var node in geometryNodes.Nodes)
                {
                    var position = TotalTransform.Transform(node.Position);
                    graphics2D.Circle(position, TotalTransform.GetScale() * 5, Color.Red);
                }
            }

            var newTransform = false;
            if (newTransform)
            {
                graphics2D.PushTransform();
                Affine currentGraphics2DTransform = graphics2D.GetTransform();
                Affine accumulatedTransform = currentGraphics2DTransform * TotalTransform;
                graphics2D.SetTransform(accumulatedTransform);
                base.OnDraw(graphics2D);
                graphics2D.PopTransform();
            }
            else
            {
                base.OnDraw(graphics2D);
            }
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

        public override void OnMouseWheel(MouseEventArgs mouseEvent)
        {
            base.OnMouseWheel(mouseEvent);
            if (FirstWidgetUnderMouse) // TODO: find a good way to decide if you are what the wheel is trying to do
            {
                const double deltaFor1Click = 120;
                double scaleAmount = mouseEvent.WheelDelta / deltaFor1Click * .1;

                ScalePartAndFixPosition(mouseEvent, LayerScale + LayerScale * scaleAmount);

                mouseEvent.WheelDelta = 0;

                Invalidate();
            }
        }

        public void Zoom(double scaleAmount)
        {
            ScalePartAndFixPosition(new MouseEventArgs(MouseButtons.None, 0, Width / 2, Height / 2, 0), LayerScale * scaleAmount);
            Invalidate();
        }

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

        private void AddControlsToToolBar(ThemeConfig theme, FlowLayoutWidget toolBar)
        {
            AddButtons(theme, toolBar);

            toolBar.AddChild(new HorizontalSpacer());
        }

        private void AdjustScrollArea()
        {
            // Calculate the scaled width and height
            double scaledWidth = Width / LayerScale;
            double scaledHeight = Height / LayerScale;

            // Calculate the offset based on the UnscaledRenderOffset
            double offsetX = -UnscaledRenderOffset.X / LayerScale;
            double offsetY = -UnscaledRenderOffset.Y / LayerScale;

            // Set the LocalBounds of the ScrollArea with the offset
            ScrollArea.LocalBounds = new RectangleDouble(offsetX, offsetY, offsetX + scaledWidth, offsetY + scaledHeight);
            ScrollArea.OriginRelativeParent = UnscaledRenderOffset;
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

                    LayerScale *= zoomDelta;

                    var mousePostScale = mouseDownPosition;
                    TotalTransform.inverse_transform(ref mousePostScale);

                    UnscaledRenderOffset += mousePostScale - mousePreScale;
                    break;

                case ETransformState.Move:
                    ScalingTransform.inverse_transform(ref mouseDelta);

                    UnscaledRenderOffset += mouseDelta;
                    break;

                case ETransformState.Edit:
                default: // also treat everything else like an edit
                    break;
            }

            AdjustScrollArea();

            Invalidate();
        }

        private void ScalePartAndFixPosition(MouseEventArgs mouseEvent, double scaleAmount)
        {
            var mousePreScale = new Vector2(mouseEvent.X, mouseEvent.Y);
            TotalTransform.inverse_transform(ref mousePreScale);

            LayerScale = scaleAmount;

            var mousePostScale = new Vector2(mouseEvent.X, mouseEvent.Y);
            TotalTransform.inverse_transform(ref mousePostScale);

            UnscaledRenderOffset += mousePostScale - mousePreScale;

            AdjustScrollArea();
        }

        private void Scene_SelectionChanged(object sender, EventArgs e)
        {
            var selectedItem = view3DWidget.Scene.SelectedItem;

            // Change tree selection to current node
            if (selectedItem != null
                && selectedItem is NodesObject3D geometryNodes)
            {
                this.geometryNodes = geometryNodes;

                var yOffset = 0;
                foreach (var node in geometryNodes.Nodes)
                {
                    ScrollArea.AddChild(new GuiWidget()
                    {
                        Position = node.Position + new Vector2(10, yOffset),
                        VAnchor = VAnchor.Absolute,
                        HAnchor = HAnchor.Absolute,
                        Width = 100,
                        Height = 100,
                        BackgroundColor = Color.Red
                    });

                    yOffset += 20;
                }
            }
            else
            {
                // Clear the editor of any controls
                this.geometryNodes = null;
                ScrollArea.CloseChildren();
            }
        }
    }
}