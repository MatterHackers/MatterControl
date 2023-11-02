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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.ImageProcessing;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.DesignTools
{
    public class PathEditor : IPropertyEditorFactory
    {
        [AttributeUsage(AttributeTargets.Property)]
        public class TopAndBottomMoveXOnlyAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class XMustBeGreaterThan0Attribute : Attribute
        {
        }

        private Action vertexChanged;
        private ThemeConfig theme;
        private VertexStorage vertexStorage;
        private ImageWidget imageWidget;
        private Object3D object3D;

        public GuiWidget CreateEditor(PropertyEditor propertyEditor, EditableProperty property, EditorContext context, ref int tabIndex)
        {
            if (property.Source is Object3D object3D)
            {
                this.object3D = object3D;
                object3D.Invalidated += RebuildImage;
            }

            if (property.Value is VertexStorage vertexStorage)
            {
                var wdiget = CreateEditor(vertexStorage, propertyEditor.UndoBuffer, propertyEditor.Theme, VertexBufferChanged);
                imageWidget.Closed += ImageWidget_Closed;

                return wdiget;
            }

            return null;
        }

        private void VertexBufferChanged()
        {
            object3D.Invalidate(InvalidateType.Path);
        }

        private void ImageWidget_Closed(object sender, EventArgs e)
        {
            imageWidget.Closed -= ImageWidget_Closed;
            object3D.Invalidated -= RebuildImage;
        }

        bool rebuildingImage = false;

        void RebuildImage(object item, EventArgs e)
        {
            if (!rebuildingImage
                && imageWidget.Image.Width != imageWidget.Width)
            {
                rebuildingImage = true;

                imageWidget.Height = imageWidget.Width / 2;
                imageWidget.Image.Allocate((int)imageWidget.Width, (int)imageWidget.Height, 32, new BlenderBGRA());

                var graphics2D = imageWidget.Image.NewGraphics2D();
                graphics2D.Clear(theme.BackgroundColor);

                var bounds = imageWidget.Image.GetBounds();
                graphics2D.Rectangle(bounds, theme.PrimaryAccentColor);

                var pathBounds = vertexStorage.GetBounds();

                new VertexSourceApplyTransform(vertexStorage, Affine.NewScaling(1 / pathBounds.Height * bounds.Height)).RenderCurve(graphics2D, theme.TextColor, 2, true, theme.PrimaryAccentColor.Blend(theme.TextColor, .5), theme.PrimaryAccentColor);

                rebuildingImage = false;
            }
        }

        public GuiWidget CreateEditor(VertexStorage vertexStorage, UndoBuffer undoBuffer, ThemeConfig theme, Action vertexChanged)
        {
            rebuildingImage = false;

            this.vertexChanged = vertexChanged;
            this.theme = theme;
            this.vertexStorage = vertexStorage;

            var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
            {
                HAnchor = HAnchor.Stretch,
                BackgroundOutlineWidth = 1,
                BackgroundColor = theme.BackgroundColor,
                BorderColor = theme.TextColor,
                Margin = 1,
            };

            imageWidget = new ImageWidget(100, 300)
            {
                HAnchor = HAnchor.Stretch,
            };
            imageWidget.SizeChanged += RebuildImage;

            topToBottom.AddChild(imageWidget);

            var toolBar = new FlowLayoutWidget()
            {
                HAnchor = HAnchor.Stretch,
            };
            topToBottom.AddChild(new HorizontalLine(theme.TextColor));
            topToBottom.AddChild(toolBar);

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

            return topToBottom;
        }
    }
}