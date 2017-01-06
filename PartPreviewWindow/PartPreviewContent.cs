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

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using System.IO;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartPreviewContent : GuiWidget
	{
		private EventHandler unregisterEvents;

		private View3DWidget partPreviewView;
		private ViewGcodeBasic viewGcodeBasic;
		private TabControl tabControl;
		private Tab layerViewTab;
		private View3DWidget.AutoRotate autoRotate3DView;
		private View3DWidget.OpenMode openMode;
		private View3DWidget.WindowMode windowMode;

		PrintItemWrapper printItem;

		public PartPreviewContent(PrintItemWrapper printItem, View3DWidget.WindowMode windowMode, View3DWidget.AutoRotate autoRotate3DView, View3DWidget.OpenMode openMode = View3DWidget.OpenMode.Viewing)
		{
			this.printItem = printItem;
			this.openMode = openMode;
			this.autoRotate3DView = autoRotate3DView;
			this.windowMode = windowMode;

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.AnchorAll();
			this.LoadPrintItem(printItem);

			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent((s, e) =>
			{
				if (windowMode == View3DWidget.WindowMode.Embeded)
				{
					this.printItem = PrinterConnectionAndCommunication.Instance.ActivePrintItem;
					LoadActivePrintItem();
				}
			}, ref unregisterEvents);

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
			this.CloseAllChildren();
			this.LoadPrintItem(printItem);
		}

		private async void LoadActivePrintItem()
		{
			await partPreviewView.ClearBedAndLoadPrintItemWrapper(printItem);
			viewGcodeBasic.LoadItem(printItem);
		}

		private void LoadPrintItem(PrintItemWrapper printItem)
		{
			tabControl = new TabControl();
			tabControl.TabBar.BorderColor = new RGBA_Bytes(0, 0, 0, 0);

			tabControl.TabBar.Padding = new BorderDouble(top: 6);

			RGBA_Bytes selectedTabColor;
			if (UserSettings.Instance.DisplayMode == ApplicationDisplayType.Responsive)
			{
				tabControl.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				selectedTabColor = ActiveTheme.Instance.TabLabelSelected;
			}
			else
			{
				tabControl.TabBar.BackgroundColor = ActiveTheme.Instance.TransparentLightOverlay;
				selectedTabColor = ActiveTheme.Instance.SecondaryAccentColor;
			}

			double buildHeight = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.build_height);

			// put in the 3D view
			partPreviewView = new View3DWidget(printItem,
				new Vector3(ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
				ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center),
				ActiveSliceSettings.Instance.GetValue<BedShape>(SettingsKey.bed_shape),
				windowMode,
				autoRotate3DView,
				openMode);

			TabPage partPreview3DView = new TabPage(partPreviewView, string.Format("3D {0} ", "View".Localize()).ToUpper());

			// put in the gcode view
			ViewGcodeBasic.WindowMode gcodeWindowMode = ViewGcodeBasic.WindowMode.Embeded;
			if (windowMode == View3DWidget.WindowMode.StandAlone)
			{
				gcodeWindowMode = ViewGcodeBasic.WindowMode.StandAlone;
			}

			viewGcodeBasic = new ViewGcodeBasic(
				new Vector3(ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
				ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center),
				ActiveSliceSettings.Instance.GetValue<BedShape>(SettingsKey.bed_shape), gcodeWindowMode);

			if (windowMode == View3DWidget.WindowMode.StandAlone)
			{
				partPreviewView.Closed += (s, e) => Close();
				viewGcodeBasic.Closed += (s, e) => Close();
			}

			TabPage layerView = new TabPage(viewGcodeBasic, "Layer View".Localize().ToUpper());

			int tabPointSize = 16;
            // add the correct tabs based on whether we are stand alone or embedded
            Tab threeDViewTab;
            if (windowMode == View3DWidget.WindowMode.StandAlone || OsInformation.OperatingSystem == OSType.Android)
			{
                threeDViewTab = new SimpleTextTabWidget(partPreview3DView, "3D View Tab", tabPointSize,
                    selectedTabColor, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
                tabControl.AddTab(threeDViewTab);
                layerViewTab = new SimpleTextTabWidget(layerView, "Layer View Tab", tabPointSize,
                    selectedTabColor, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
                tabControl.AddTab(layerViewTab);
			}
			else
			{
                threeDViewTab = new PopOutTextTabWidget(partPreview3DView, "3D View Tab", new Vector2(590, 400), tabPointSize);
                tabControl.AddTab(threeDViewTab);
				layerViewTab = new PopOutTextTabWidget(layerView, "Layer View Tab", new Vector2(590, 400), tabPointSize);
				tabControl.AddTab(layerViewTab);
			}

            threeDViewTab.ToolTipText = "Preview 3D Design".Localize();
            layerViewTab.ToolTipText = "Preview layer Tool Paths".Localize();

            this.AddChild(tabControl);
		}

		public override void OnLoad(EventArgs args)
		{
			MatterControlApplication.Instance.ActiveView3DWidget = partPreviewView;

			LoadActivePrintItem();

			base.OnLoad(args);
		}

		public void SwitchToGcodeView()
		{
			tabControl.TabBar.SelectTab(layerViewTab);
			viewGcodeBasic.Focus();
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}