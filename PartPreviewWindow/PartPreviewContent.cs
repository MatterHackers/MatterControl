/*
Copyright (c) 2017, Lars Brubaker
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
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using System.IO;
using static MatterHackers.MatterControl.PartPreviewWindow.View3DWidget;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.ActionBar;

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
				this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;
				this.Padding = new BorderDouble(top: 3);

				double buildHeight = activeSettings.GetValue<double>(SettingsKey.build_height);
				
				viewControls3D = new ViewControls3D(ApplicationController.Instance.Theme.ViewControlsButtonFactory)
				{
					PartSelectVisible = false,
					VAnchor = VAnchor.ParentTop | VAnchor.FitToChildren | VAnchor.AbsolutePosition,
					HAnchor = HAnchor.ParentLeft | HAnchor.FitToChildren,
					Visible = true,
					Margin = new BorderDouble(11, 0, 0, 50)
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
					else
					{
						return gcodeViewer.ShowOverflowMenu();
					}
				};

				// The 3D model view
				modelViewer = new View3DWidget(printItem,
					new Vector3(activeSettings.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
					activeSettings.GetValue<Vector2>(SettingsKey.print_center),
					activeSettings.GetValue<BedShape>(SettingsKey.bed_shape),
					View3DWidget.WindowMode.Embeded,
					View3DWidget.AutoRotate.Disabled,
					viewControls3D,
					ApplicationController.Instance.Theme,
					View3DWidget.OpenMode.Editing);

				var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
				topToBottom.AnchorAll();
				this.AddChild(topToBottom);

				// Must come after we have an instance of View3DWidget an its undo buffer
				topToBottom.AddChild(new PrinterActionsBar(modelViewer)
				{
					Padding = new BorderDouble(bottom: 2)
				});

				var leftToRight = new FlowLayoutWidget();
				leftToRight.AnchorAll();
				topToBottom.AddChild(leftToRight);

				var container = new GuiWidget();
				container.AnchorAll();
				container.AddChild(modelViewer);

				leftToRight.AddChild(container);

				// The slice layers view
				gcodeViewer = new ViewGcodeBasic(
					new Vector3(activeSettings.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
					activeSettings.GetValue<Vector2>(SettingsKey.print_center),
					activeSettings.GetValue<BedShape>(SettingsKey.bed_shape),
					ViewGcodeBasic.WindowMode.Embeded,
					viewControls3D,
					ApplicationController.Instance.Theme,
					modelViewer.meshViewerWidget);
				gcodeViewer.AnchorAll();
				this.gcodeViewer.Visible = false;

				container.AddChild(gcodeViewer);

				AddSettingsTabBar(leftToRight, modelViewer);

				modelViewer.BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor;

				if (ApplicationController.Instance.PartPreviewState.RotationMatrix == Matrix4X4.Identity)
				{
					modelViewer.meshViewerWidget.ResetView();

					ApplicationController.Instance.PartPreviewState.RotationMatrix = modelViewer.meshViewerWidget.World.RotationMatrix;
					ApplicationController.Instance.PartPreviewState.TranslationMatrix = modelViewer.meshViewerWidget.World.TranslationMatrix;
				}
				else
				{
					modelViewer.meshViewerWidget.World.RotationMatrix = ApplicationController.Instance.PartPreviewState.RotationMatrix;
					modelViewer.meshViewerWidget.World.TranslationMatrix = ApplicationController.Instance.PartPreviewState.TranslationMatrix;
				}

				this.printItem = printItem;

				this.AddChild(viewControls3D);

				var extruderTemperatureWidget = new TemperatureWidgetExtruder()
				{
					VAnchor = VAnchor.ParentTop | VAnchor.FitToChildren | VAnchor.AbsolutePosition,
					HAnchor = HAnchor.ParentRight | HAnchor.FitToChildren,
					Visible = true,
					Margin = new BorderDouble(0, 0, 800, 50)
				};
				this.AddChild(extruderTemperatureWidget);

				if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_heated_bed))
				{
					var bedTemperatureWidget = new TemperatureWidgetBed()
					{
						VAnchor = VAnchor.ParentTop | VAnchor.FitToChildren | VAnchor.AbsolutePosition,
						HAnchor = HAnchor.ParentRight | HAnchor.FitToChildren,
						Visible = true,
						Margin = new BorderDouble(0, 0, 860, 50)
					};
					this.AddChild(bedTemperatureWidget);
				}

				this.AnchorAll();
			}

			private void AddSettingsTabBar(GuiWidget parent, GuiWidget widgetTodockTo)
			{
				var sideBar = new DockingTabControl(widgetTodockTo, DockSide.Right)
				{
					ControlIsPinned = ApplicationController.Instance.PrintSettingsPinned
				};
				sideBar.PinStatusChanged += (s, e) =>
				{
					ApplicationController.Instance.PrintSettingsPinned = sideBar.ControlIsPinned;
				};
				parent.AddChild(sideBar);

				if (ActiveSliceSettings.Instance.PrinterSelected)
				{
					sideBar.AddPage("Slice Settings".Localize(), new SliceSettingsWidget());
				}
				else
				{
					sideBar.AddPage("Slice Settings".Localize(), new NoSettingsWidget());
				}

				sideBar.AddPage("Controls".Localize(), new ManualPrinterControls());

				var terminalControls = new TerminalControls();
				terminalControls.VAnchor |= VAnchor.ParentBottomTop;
				sideBar.AddPage("Terminal".Localize(), terminalControls);
			}

			public void ToggleView()
			{
				gcodeViewer.Visible = !gcodeViewer.Visible;
			}

			private async void LoadActivePrintItem()
			{
				await modelViewer.ClearBedAndLoadPrintItemWrapper(printItem);
			}

			public override void OnLoad(EventArgs args)
			{
				ApplicationController.Instance.ActiveView3DWidget = modelViewer;
				LoadActivePrintItem();
				base.OnLoad(args);
			}

			public override void OnClosed(ClosedEventArgs e)
			{
				var visibleWidget = modelViewer.meshViewerWidget;

				ApplicationController.Instance.PartPreviewState.RotationMatrix = visibleWidget.World.RotationMatrix;
				ApplicationController.Instance.PartPreviewState.TranslationMatrix = visibleWidget.World.TranslationMatrix;

				base.OnClosed(e);
			}
		}
	}


}