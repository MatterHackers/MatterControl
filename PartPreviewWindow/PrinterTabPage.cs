/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.GCodeVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterTabPage : GuiWidget
	{
		private View3DWidget modelViewer;
		internal ViewGcodeBasic gcodeViewer;
		private PrintItemWrapper printItem;
		private ViewControls3D viewControls3D;

		private DoubleSolidSlider layerRenderRatioSlider;
		private SolidSlider selectLayerSlider;

		private PrinterConfig printer;
		private View3DConfig gcodeOptions;
		private GuiWidget view3DContainer;

		public PrinterTabPage(PrinterSettings activeSettings, PrintItemWrapper printItem)
		{
			printer = ApplicationController.Instance.Printer;
			gcodeOptions = printer.BedPlate.RendererOptions;

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
			viewControls3D.ViewModeChanged += (s, e) =>
			{
				this.ViewMode = e.ViewMode;
			};

			viewControls3D.ResetView += (sender, e) =>
			{
				modelViewer.meshViewerWidget.ResetView();
			};
			viewControls3D.OverflowButton.DynamicPopupContent = () =>
			{
				if (gcodeViewer.Visible)
				{
					return this.ShowGCodeOverflowMenu();
				}
				else
				{
					return modelViewer.ShowOverflowMenu();
				}
			};

			int sliderWidth = (UserSettings.Instance.IsTouchScreen) ? 20 : 10;

			selectLayerSlider = new SolidSlider(new Vector2(), sliderWidth, 0, 1, Orientation.Vertical);
			selectLayerSlider.ValueChanged += (s, e) =>
			{
				// TODO: Why would these need to be updated here as well as in the horizontal slider?
				printer.BedPlate.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
				printer.BedPlate.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;

				printer.BedPlate.ActiveLayerIndex = (int)(selectLayerSlider.Value + .5);

				this.Invalidate();
			};

			layerRenderRatioSlider = new DoubleSolidSlider(new Vector2(), sliderWidth);
			layerRenderRatioSlider.FirstValue = 0;
			layerRenderRatioSlider.FirstValueChanged += (s, e) =>
			{
				printer.BedPlate.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
				printer.BedPlate.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;

				this.Invalidate();
			};
			layerRenderRatioSlider.SecondValue = 1;
			layerRenderRatioSlider.SecondValueChanged += (s, e) =>
			{
				printer.BedPlate.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
				printer.BedPlate.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;


				this.Invalidate();
			};

			SetSliderSizes();

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

			modelViewer.meshViewerWidget.TrackballTumbleWidget.DrawGlContent += TrackballTumbleWidget_DrawGlContent;

			modelViewer.BoundsChanged += (s, e) =>
			{
				SetSliderSizes();
			};

			var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			this.AddChild(topToBottom);

			// Must come after we have an instance of View3DWidget an its undo buffer
			topToBottom.AddChild(new PrinterActionsBar(modelViewer, this)
			{
				Padding = new BorderDouble(bottom: 2)
			});

			var leftToRight = new FlowLayoutWidget();
			leftToRight.AnchorAll();
			topToBottom.AddChild(leftToRight);

			view3DContainer = new GuiWidget();
			view3DContainer.AnchorAll();
			view3DContainer.AddChild(modelViewer);

			leftToRight.AddChild(view3DContainer);

			// The slice layers view
			gcodeViewer = new ViewGcodeBasic(
				new Vector3(activeSettings.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
				activeSettings.GetValue<Vector2>(SettingsKey.print_center),
				activeSettings.GetValue<BedShape>(SettingsKey.bed_shape),
				viewControls3D);
			gcodeViewer.AnchorAll();
			this.gcodeViewer.Visible = false;

			view3DContainer.AddChild(gcodeViewer);
			view3DContainer.AddChild(layerRenderRatioSlider);
			view3DContainer.AddChild(selectLayerSlider);

			printer.BedPlate.ActiveLayerChanged += ActiveLayer_Changed;

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

			printer.BedPlate.LoadedGCodeChanged += BedPlate_LoadedGCodeChanged;
			
			this.ShowSliceLayers = false;

			this.printItem = printItem;

			this.AddChild(viewControls3D);

			this.AnchorAll();
		}

		private void BedPlate_LoadedGCodeChanged(object sender, EventArgs e)
		{
			selectLayerSlider.Maximum = printer.BedPlate.LoadedGCode.LayerCount - 1;
		
			// ResetRenderInfo
			printer.BedPlate.RenderInfo = new GCodeRenderInfo(
				0,
				1,
				Agg.Transform.Affine.NewIdentity(),
				1,
				0,
				1,
				new Vector2[]
				{
					ActiveSliceSettings.Instance.Helpers.ExtruderOffset(0),
					ActiveSliceSettings.Instance.Helpers.ExtruderOffset(1)
				},
				this.GetRenderType,
				MeshViewerWidget.GetMaterialColor);
		}

		private RenderType GetRenderType()
		{
			RenderType renderType = RenderType.Extrusions;
			if (gcodeOptions.RenderMoves)
			{
				renderType |= RenderType.Moves;
			}
			if (gcodeOptions.RenderRetractions)
			{
				renderType |= RenderType.Retractions;
			}
			if (gcodeOptions.RenderSpeeds)
			{
				renderType |= RenderType.SpeedColors;
			}
			if (gcodeOptions.SimulateExtrusion)
			{
				renderType |= RenderType.SimulateExtrusion;
			}
			if (gcodeOptions.TransparentExtrusion)
			{
				renderType |= RenderType.TransparentExtrusion;
			}
			if (gcodeOptions.HideExtruderOffsets)
			{
				renderType |= RenderType.HideExtruderOffsets;
			}

			return renderType;
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

		private GCodeFile loadedGCode => printer.BedPlate.LoadedGCode;

		private bool showSliceLayers;
		private bool ShowSliceLayers
		{
			get => showSliceLayers;
			set
			{
				showSliceLayers = value;
				gcodeViewer.Visible = value;

				modelViewer.meshViewerWidget.IsActive = !value;

				if (showSliceLayers)
				{
					modelViewer.Scene.ClearSelection();
				}

				var slidersVisible = printer.BedPlate.RenderInfo != null && value;

				selectLayerSlider.Visible = slidersVisible;
				layerRenderRatioSlider.Visible = slidersVisible;

				modelViewer.selectedObjectPanel.Visible = !showSliceLayers;
			}
		}

		private PartViewMode viewMode;
		public PartViewMode ViewMode
		{
			get => viewMode;
			set
			{
				if (viewMode != value)
				{
					viewMode = value;

					viewControls3D.ViewMode = viewMode;

					switch (viewMode)
					{
						case PartViewMode.Layers2D:
							UserSettings.Instance.set("LayerViewDefault", "2D Layer");
							if (gcodeViewer.gcode2DWidget != null)
							{
								gcodeViewer.gcode2DWidget.Visible = true;

								// HACK: Getting the Layer2D view to show content only works if CenterPartInView is called after the control is visible and after some cycles have passed
								UiThread.RunOnIdle(gcodeViewer.gcode2DWidget.CenterPartInView);
							}
							this.ShowSliceLayers = true;
							break;

						case PartViewMode.Layers3D:
							UserSettings.Instance.set("LayerViewDefault", "3D Layer");
							if (gcodeViewer.gcode2DWidget != null)
							{
								gcodeViewer.gcode2DWidget.Visible = false;
							}
							this.ShowSliceLayers = true;
							break;

						case PartViewMode.Model:
							this.ShowSliceLayers = false;
							break;
					}
				}
			}
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

		public override void OnDraw(Graphics2D graphics2D)
		{
			bool printerIsRunningPrint = PrinterConnection.Instance.PrinterIsPaused || PrinterConnection.Instance.PrinterIsPrinting;
			if (gcodeOptions.SyncToPrint
				&& printerIsRunningPrint
				&& gcodeViewer.Visible)
			{
				SetAnimationPosition();
				this.Invalidate();
			}

			base.OnDraw(graphics2D);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			// Store active transforms on close
			var visibleWidget = modelViewer.meshViewerWidget;
			ApplicationController.Instance.PartPreviewState.RotationMatrix = visibleWidget.World.RotationMatrix;
			ApplicationController.Instance.PartPreviewState.TranslationMatrix = visibleWidget.World.TranslationMatrix;

			if (modelViewer?.meshViewerWidget != null)
			{
				modelViewer.meshViewerWidget.TrackballTumbleWidget.DrawGlContent -= TrackballTumbleWidget_DrawGlContent;
			}

			printer.BedPlate.ActiveLayerChanged -= ActiveLayer_Changed;
			printer.BedPlate.LoadedGCodeChanged -= BedPlate_LoadedGCodeChanged;

			base.OnClosed(e);
		}

		private void TrackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			if (loadedGCode == null || printer.BedPlate.GCodeRenderer == null || !this.Visible)
			{
				return;
			}

			printer.BedPlate.Render3DLayerFeatures();
		}

		internal GuiWidget ShowGCodeOverflowMenu()
		{
			var textColor = RGBA_Bytes.Black;

			var popupContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Padding = 12,
				BackgroundColor = RGBA_Bytes.White
			};

			// put in a show grid check box
			CheckBox showGrid = new CheckBox("Print Bed".Localize(), textColor: textColor);
			showGrid.Checked = gcodeOptions.RenderGrid;
			showGrid.CheckedStateChanged += (sender, e) =>
			{
				// TODO: How (if at all) do we disable bed rendering on GCode2D?
				gcodeOptions.RenderGrid = showGrid.Checked;
			};
			popupContainer.AddChild(showGrid);

			// put in a show moves checkbox
			var showMoves = new CheckBox("Moves".Localize(), textColor: textColor);
			showMoves.Checked = gcodeOptions.RenderMoves;
			showMoves.CheckedStateChanged += (sender, e) =>
			{
				gcodeOptions.RenderMoves = showMoves.Checked;
			};
			popupContainer.AddChild(showMoves);

			// put in a show Retractions checkbox
			CheckBox showRetractions = new CheckBox("Retractions".Localize(), textColor: textColor);
			showRetractions.Checked = gcodeOptions.RenderRetractions;
			showRetractions.CheckedStateChanged += (sender, e) =>
			{
				gcodeOptions.RenderRetractions = showRetractions.Checked;
			};
			popupContainer.AddChild(showRetractions);

			// Speeds checkbox
			var showSpeeds = new CheckBox("Speeds".Localize(), textColor: textColor);
			showSpeeds.Checked = gcodeOptions.RenderSpeeds;
			showSpeeds.CheckedStateChanged += (sender, e) =>
			{
				//gradientWidget.Visible = showSpeeds.Checked;
				gcodeOptions.RenderSpeeds = showSpeeds.Checked;
			};

			popupContainer.AddChild(showSpeeds);

			// Extrusion checkbox
			var simulateExtrusion = new CheckBox("Extrusion".Localize(), textColor: textColor);
			simulateExtrusion.Checked = gcodeOptions.SimulateExtrusion;
			simulateExtrusion.CheckedStateChanged += (sender, e) =>
			{
				gcodeOptions.SimulateExtrusion = simulateExtrusion.Checked;
			};
			popupContainer.AddChild(simulateExtrusion);

			// Transparent checkbox
			var transparentExtrusion = new CheckBox("Transparent".Localize(), textColor: textColor)
			{
				Checked = gcodeOptions.TransparentExtrusion,
				Margin = new BorderDouble(5, 0, 0, 0),
				HAnchor = HAnchor.ParentLeft,
			};
			transparentExtrusion.CheckedStateChanged += (sender, e) =>
			{
				gcodeOptions.TransparentExtrusion = transparentExtrusion.Checked;
			};
			popupContainer.AddChild(transparentExtrusion);

			// Extrusion checkbox
			if (ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count) > 1)
			{
				CheckBox hideExtruderOffsets = new CheckBox("Hide Offsets", textColor: textColor);
				hideExtruderOffsets.Checked = gcodeOptions.HideExtruderOffsets;
				hideExtruderOffsets.CheckedStateChanged += (sender, e) =>
				{
					gcodeOptions.HideExtruderOffsets = hideExtruderOffsets.Checked;
				};
				popupContainer.AddChild(hideExtruderOffsets);
			}

			// Sync To Print checkbox
			{
				var syncToPrint = new CheckBox("Sync To Print".Localize(), textColor: textColor);
				syncToPrint.Checked = (UserSettings.Instance.get("LayerViewSyncToPrint") == "True");
				syncToPrint.Name = "Sync To Print Checkbox";
				syncToPrint.CheckedStateChanged += (s, e) =>
				{
					gcodeOptions.SyncToPrint = syncToPrint.Checked;
					SetSyncToPrintVisibility();
				};
				popupContainer.AddChild(syncToPrint);
			}

			return popupContainer;
		}

		private void SetSyncToPrintVisibility()
		{
			bool printerIsRunningPrint = PrinterConnection.Instance.PrinterIsPaused || PrinterConnection.Instance.PrinterIsPrinting;

			if (gcodeOptions.SyncToPrint && printerIsRunningPrint)
			{
				SetAnimationPosition();
				//navigationWidget.Visible = false;
				//setLayerWidget.Visible = false;
				layerRenderRatioSlider.Visible = false;
				selectLayerSlider.Visible = false;
			}
			else
			{
				if (layerRenderRatioSlider != null)
				{
					layerRenderRatioSlider.FirstValue = 0;
					layerRenderRatioSlider.SecondValue = 1;
				}

				//navigationWidget.Visible = true;
				//setLayerWidget.Visible = true;

				layerRenderRatioSlider.Visible = true;
				selectLayerSlider.Visible = true;
			}
		}

		private void SetSliderSizes()
		{
			if (selectLayerSlider == null || modelViewer == null)
			{
				return;
			}

			selectLayerSlider.OriginRelativeParent = new Vector2(modelViewer.Width - 20, 78);
			selectLayerSlider.TotalWidthInPixels = modelViewer.Height - 85;

			layerRenderRatioSlider.OriginRelativeParent = new Vector2(11, 65);
			layerRenderRatioSlider.TotalWidthInPixels = modelViewer.Width - 45;
		}
		private void SetAnimationPosition()
		{
			int currentLayer = PrinterConnection.Instance.CurrentlyPrintingLayer;
			if (currentLayer <= 0)
			{
				selectLayerSlider.Value = 0;
				layerRenderRatioSlider.SecondValue = 0;
				layerRenderRatioSlider.FirstValue = 0;
			}
			else
			{
				selectLayerSlider.Value = currentLayer - 1;
				layerRenderRatioSlider.SecondValue = PrinterConnection.Instance.RatioIntoCurrentLayer;
				layerRenderRatioSlider.FirstValue = 0;
			}
		}

		private void ActiveLayer_Changed(object sender, EventArgs e)
		{
			if (selectLayerSlider != null
				&& printer.BedPlate.ActiveLayerIndex != (int)(selectLayerSlider.Value + .5))
			{
				selectLayerSlider.Value = printer.BedPlate.ActiveLayerIndex;
			}
		}

	}
}
