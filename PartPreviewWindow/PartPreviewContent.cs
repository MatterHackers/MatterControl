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
using static MatterHackers.MatterControl.PartPreviewWindow.View3DWidget;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartPreviewContent : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;

		private TabControl tabControl;
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

			this.AnchorAll();

			// LoadPrintItem {{
			var activeSettings = ActiveSliceSettings.Instance;

			tabControl = ApplicationController.Instance.Theme.CreateTabControl();

			string tabTitle = !activeSettings.PrinterSelected ? "Printer".Localize() : activeSettings.GetValue(SettingsKey.printer_name);

			RGBA_Bytes selectedTabColor;
			if (!UserSettings.Instance.IsTouchScreen)
			{
				tabControl.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				selectedTabColor = ActiveTheme.Instance.TabLabelSelected;
			}
			else
			{
				tabControl.TabBar.BackgroundColor = ActiveTheme.Instance.TransparentLightOverlay;
				selectedTabColor = ActiveTheme.Instance.SecondaryAccentColor;
			}

			// Add a tab for the current printer
			var printerTab = new SimpleTextTabWidget(
				new TabPage(new PrinterTabPage(ActiveSliceSettings.Instance, printItem), tabTitle.ToUpper()),
				"3D View Tab",
				tabControl.TextPointSize,
				selectedTabColor,
				new RGBA_Bytes(),
				ActiveTheme.Instance.TabLabelUnselected,
				new RGBA_Bytes());
			printerTab.ToolTipText = "Preview 3D Design".Localize();
			tabControl.AddTab(printerTab);

			this.AddChild(tabControl);
		}

		public void Reload(PrintItemWrapper printItem)
		{
			this.CloseAllChildren();
			//this.LoadPrintItem(printItem);
			System.Diagnostics.Debugger.Break();
		}

		public class PrinterTabPage : GuiWidget
		{
			private View3DWidget modelViewer;
			private ViewGcodeBasic gcodeViewer;
			private PrintItemWrapper printItem;
			private ViewControls3D viewControls3D;

			public PrinterTabPage(PrinterSettings activeSettings, PrintItemWrapper printItem)
			{
				double buildHeight = activeSettings.GetValue<double>(SettingsKey.build_height);

				viewControls3D = new ViewControls3D(ApplicationController.Instance.Theme.ViewControlsButtonFactory)
				{
					PartSelectVisible = false,
					VAnchor = VAnchor.ParentTop | VAnchor.FitToChildren | VAnchor.AbsolutePosition,
					HAnchor = HAnchor.ParentLeft | HAnchor.FitToChildren,
					Visible = true,
					Margin = new BorderDouble(3, 0, 0, 51)
				};
				viewControls3D.ResetView += (sender, e) =>
				{
					modelViewer.meshViewerWidget.ResetView();
				};
				viewControls3D.OverflowButton.DynamicPopupContent = () =>
				{
					if (modelViewer.Visible)
					{
						return modelViewer.ShowOverflowMenu();
					}

					return null;
				};

				// The 3D model view
				modelViewer = new View3DWidget(printItem,
					new Vector3(activeSettings.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
					activeSettings.GetValue<Vector2>(SettingsKey.print_center),
					activeSettings.GetValue<BedShape>(SettingsKey.bed_shape),
					View3DWidget.WindowMode.Embeded,
					View3DWidget.AutoRotate.Disabled,
					viewControls3D,
					View3DWidget.OpenMode.Editing);

				var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
				topToBottom.AnchorAll();
				this.AddChild(topToBottom);

				// Must come after we have an instance of View3DWidget an its undo buffer
				topToBottom.AddChild(new PrinterActionsBar(modelViewer.UndoBuffer));
				topToBottom.AddChild(modelViewer);

				// The slice layers view
				gcodeViewer = new ViewGcodeBasic(
					new Vector3(activeSettings.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
					activeSettings.GetValue<Vector2>(SettingsKey.print_center),
					activeSettings.GetValue<BedShape>(SettingsKey.bed_shape),
					ViewGcodeBasic.WindowMode.Embeded,
					viewControls3D);
				gcodeViewer.AnchorAll();
				this.gcodeViewer.Visible = false;
				topToBottom.AddChild(gcodeViewer);

				this.printItem = printItem;

				//this.DebugShowBounds = true;


				this.AddChild(viewControls3D);

				this.AnchorAll();
			}

			public void ToggleView()
			{
				bool layersVisible = gcodeViewer.Visible;

				if (layersVisible)
				{
					// Copy layers tumble state to partpreview
					modelViewer.meshViewerWidget.TrackballTumbleWidget.TrackBallController.CopyTransforms(gcodeViewer.meshViewerWidget.TrackballTumbleWidget.TrackBallController);
				}
				else
				{
					// Copy partpreview tumble state to layers
					gcodeViewer.meshViewerWidget.TrackballTumbleWidget.TrackBallController.CopyTransforms(modelViewer.meshViewerWidget.TrackballTumbleWidget.TrackBallController);
				}

				modelViewer.Visible = layersVisible;
				gcodeViewer.Visible = !modelViewer.Visible;
			}

			private async void LoadActivePrintItem()
			{
				await modelViewer.ClearBedAndLoadPrintItemWrapper(printItem);
			}

			public override void OnLoad(EventArgs args)
			{
				MatterControlApplication.Instance.ActiveView3DWidget = modelViewer;
				LoadActivePrintItem();
				base.OnLoad(args);
			}

			public override void OnClosed(ClosedEventArgs e)
			{
				base.OnClosed(e);
			}
		}
	}
}