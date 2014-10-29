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
    public class PartPreviewMainWindow : SystemWindow
    {
		PartPreviewContainer partPreviewWidget;
        bool OpenInEditMode;


        public PartPreviewMainWindow(PrintItemWrapper printItem, View3DWidget.AutoRotate autoRotate3DView, bool openInEditMode = false)
            : base(690, 340)
        {
            UseOpenGL = true;
            this.OpenInEditMode = openInEditMode;
            string partPreviewTitle = LocalizedString.Get("MatterControl");
            Title = string.Format("{0}: ", partPreviewTitle) + Path.GetFileName(printItem.Name);

			partPreviewWidget = new PartPreviewContainer(printItem, false, autoRotate3DView, openInEditMode);

			this.AddChild(partPreviewWidget);

            AddHandlers();

            Width = 640;
            Height = 480;

            MinimumSize = new Vector2(400, 300);
            ShowAsSystemWindow();

            // We do this after showing the system window so that when we try and take focus of the parent window (the system window)
            // it exists and can give the focus to its child the gcode window.
            if (Path.GetExtension(printItem.FileLocation).ToUpper() == ".GCODE")
            {
				partPreviewWidget.SwitchToGcodeView();
            }
        }

        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
			partPreviewWidget.Closed += (sender, e) => { Close(); };
        }

        public void ThemeChanged(object sender, EventArgs e)
        {
            this.Invalidate();
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

	public class PartPreviewContainer : GuiWidget
	{
		View3DWidget partPreviewView;
		ViewGcodeBasic viewGcodeBasic;
		TabControl tabControl;
		TabPage layerView;
		View3DWidget.AutoRotate autoRotate3DView;
		bool openInEditMode;
		bool widgetIsEmbedded;

		public PartPreviewContainer(PrintItemWrapper printItem, bool widgetIsEmbedded, View3DWidget.AutoRotate autoRotate3DView, bool openInEditMode = false)
		{
			this.openInEditMode = openInEditMode;
			this.autoRotate3DView = autoRotate3DView;
			this.widgetIsEmbedded = widgetIsEmbedded;

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.AnchorAll();
			this.Load(printItem);
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

			View3DWidget.WindowType viewType;
			bool showCloseButton;
			if (widgetIsEmbedded)
			{
				viewType = View3DWidget.WindowType.Embeded;
				showCloseButton = false;
			}
			else
			{
				viewType = View3DWidget.WindowType.StandAlone;
				showCloseButton = true;
			}

			double buildHeight = ActiveSliceSettings.Instance.BuildHeight;

			// put in the 3D view
			{
				string part3DViewLabelFull = string.Format("{0} {1} ", "3D", "View".Localize()).ToUpper();

				partPreviewView = new View3DWidget(printItem,
					new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight),
					ActiveSliceSettings.Instance.BedCenter,
					ActiveSliceSettings.Instance.BedShape,
					viewType,
					autoRotate3DView,
					openInEditMode);



				TabPage partPreview3DView = new TabPage(partPreviewView, part3DViewLabelFull);
				tabControl.AddTab(new SimpleTextTabWidget(partPreview3DView, "3D View Tab", 16,
					selectedTabColor, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes()));
			}

			// put in the 2d gcode view
			{
				viewGcodeBasic = new ViewGcodeBasic(printItem,
					new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight),
					ActiveSliceSettings.Instance.BedCenter,
					ActiveSliceSettings.Instance.BedShape,showCloseButton);
				layerView = new TabPage(viewGcodeBasic, LocalizedString.Get("Layer View").ToUpper());
				tabControl.AddTab(new SimpleTextTabWidget(layerView, "Layer View Tab", 16,
					selectedTabColor, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes()));
			}

			this.AddChild(tabControl);
		}

		public void SwitchToGcodeView()
		{
			tabControl.TabBar.SwitchToPage(layerView);
			viewGcodeBasic.Focus();
		}
	}
}
