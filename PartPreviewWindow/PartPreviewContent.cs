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
using System.Diagnostics;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartPreviewContent : GuiWidget
	{
        event EventHandler unregisterEvents;

        View3DWidget partPreviewView;
		ViewGcodeBasic viewGcodeBasic;
		TabControl tabControl;
		TabPage layerView;
		View3DWidget.AutoRotate autoRotate3DView;
        View3DWidget.OpenMode openMode;
		View3DWidget.WindowMode windowMode;

        public PartPreviewContent(PrintItemWrapper printItem, View3DWidget.WindowMode windowMode, View3DWidget.AutoRotate autoRotate3DView, View3DWidget.OpenMode openMode = View3DWidget.OpenMode.Viewing)
		{
			this.openMode = openMode;
			this.autoRotate3DView = autoRotate3DView;
			this.windowMode = windowMode;

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.AnchorAll();
			this.Load(printItem);
        
            // We do this after showing the system window so that when we try and take focus of the parent window (the system window)
            // it exists and can give the focus to its child the gcode window.
            if (printItem != null 
                && Path.GetExtension(printItem.FileLocation).ToUpper() == ".GCODE")
            {
                SwitchToGcodeView();
            }
        }

		public void Reload(PrintItemWrapper printItem)
		{
			this.RemoveAllChildren();
			this.Load(printItem);
		}

		void Load(PrintItemWrapper printItem)
		{
			tabControl = new TabControl();
			tabControl.TabBar.BorderColor = new RGBA_Bytes(0, 0, 0, 0);

			tabControl.TabBar.Padding = new BorderDouble(top: 6);

			RGBA_Bytes selectedTabColor;
			if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Responsive)
			{
				tabControl.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				selectedTabColor = ActiveTheme.Instance.TabLabelSelected;
			}
			else
			{
				tabControl.TabBar.BackgroundColor = ActiveTheme.Instance.TransparentLightOverlay;
				selectedTabColor = ActiveTheme.Instance.SecondaryAccentColor;
			}

			double buildHeight = ActiveSliceSettings.Instance.BuildHeight;

			// put in the 3D view
			{
				string part3DViewLabelFull = string.Format("{0} {1} ", "3D", "View".Localize()).ToUpper();

				partPreviewView = new View3DWidget(printItem,
					new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight),
					ActiveSliceSettings.Instance.BedCenter,
					ActiveSliceSettings.Instance.BedShape,
                    windowMode,
					autoRotate3DView,
                    openMode);

                partPreviewView.Closed += (sender, e) =>
                {
                    Close();
                };

				TabPage partPreview3DView = new TabPage(partPreviewView, part3DViewLabelFull);
				tabControl.AddTab(new SimpleTextTabWidget(partPreview3DView, "3D View Tab", 16,
					selectedTabColor, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes()));
			}

			// put in the gcode view
			{
                ViewGcodeBasic.WindowMode gcodeWindowMode = ViewGcodeBasic.WindowMode.Embeded;
                if (windowMode == View3DWidget.WindowMode.StandAlone)
                {
                    gcodeWindowMode = ViewGcodeBasic.WindowMode.StandAlone;
                }

                viewGcodeBasic = new ViewGcodeBasic(printItem,
                    new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight),
                    ActiveSliceSettings.Instance.BedCenter,
                    ActiveSliceSettings.Instance.BedShape, gcodeWindowMode);

                viewGcodeBasic.Closed += (sender, e) =>
                {
                    Close();
                };

				layerView = new TabPage(viewGcodeBasic, LocalizedString.Get("Layer View").ToUpper());
				tabControl.AddTab(new SimpleTextTabWidget(layerView, "Layer View Tab", 16,
					selectedTabColor, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes()));
			}

			this.AddChild(tabControl);
        
            // if the embeded view was on the gcode tab than makes sure it stays there
            if (UserSettings.Instance.Fields.EmbededViewShowingGCode)
            {
                SwitchToGcodeView();
            }

            tabControl.TabBar.TabIndexChanged += (sender, e) =>
            {
                if (tabControl.TabBar.SelectedTabName == "Layer View Tab")
                {
                    UserSettings.Instance.Fields.EmbededViewShowingGCode = true;
                }
                else
                {
                    UserSettings.Instance.Fields.EmbededViewShowingGCode = false;
                }
            };
        }

		public void SwitchToGcodeView()
		{
			tabControl.TabBar.SwitchToPage(layerView);
			viewGcodeBasic.Focus();
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}
	}
}
