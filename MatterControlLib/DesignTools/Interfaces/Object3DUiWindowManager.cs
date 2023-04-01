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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools
{
    public class Object3DUiWindowManager
    {
        public WindowWidget WindowWidget { get; private set; }
        private Object3DControlsLayer controlLayer;
        private IObject3D item;

        public Object3DUiWindowManager()
        {
        }

        public bool CreateWidgetIfRequired(IObject3D item, Object3DControlsLayer controlLayer, string title)
        {
            if (WindowWidget == null
                || WindowWidget.Parents<SystemWindow>().Count() == 0)
            {
                if (WindowWidget != null)
                {
                    WindowWidget.Close();
                }

                this.controlLayer = controlLayer;
                this.item = item;
                var theme = ApplicationController.Instance.Theme;
                WindowWidget = new WindowWidget(theme, new RectangleDouble(10, 10, 700, 750))
                {
                    BackgroundColor = theme.BackgroundColor.WithAlpha(200),
                };
                
                WindowWidget.AddTitleBar(title, null);

                controlLayer.GuiSurface.AddChild(WindowWidget);
                controlLayer.GuiSurface.AfterDraw += GuiSurface_AfterDraw;

                return true;
            }

            return false;
        }

        private void GuiSurface_AfterDraw(object sender, DrawEventArgs e)
        {
            if (!controlLayer.Scene.Contains(item))
            {
                WindowWidget.Close();
                if (sender is GuiWidget guiWidget)
                {
                    guiWidget.AfterDraw -= GuiSurface_AfterDraw;
                }
            }
            else
            {
                if (controlLayer.Scene.SelectedItem == item)
                {
                    WindowWidget.Visible = true;
                }
                else
                {
                    WindowWidget.Visible = false;
                }
            }
        }


        public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
        {
            throw new System.NotImplementedException();
        }

        public void Close()
        {
            if (WindowWidget != null)
            {
                WindowWidget.Close();
            }
        }

        public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
        {
            throw new System.NotImplementedException();
        }
    }
}