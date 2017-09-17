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

using System;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterTabPage : PrinterTabBase
	{
		internal GCode2DWidget gcode2DWidget;

		private View3DConfig gcodeOptions;
		private DoubleSolidSlider layerRenderRatioSlider;
		private SolidSlider selectLayerSlider;
		private TextWidget layerCountText;
		private TextWidget layerStartText;
		private ValueDisplayInfo currentLayerInfo;
		private SystemWindow parentSystemWindow;

		private PrinterConfig printer;

		public PrinterTabPage(PrinterConfig printer, ThemeConfig theme, string tabTitle)
			: base(printer, printer.Bed, theme, tabTitle)
		{
			this.printer = printer;
			modelViewer.meshViewerWidget.EditorMode = MeshViewerWidget.EditorType.Printer;

			gcodeOptions = sceneContext.RendererOptions;

			viewControls3D.TransformStateChanged += (s, e) =>
			{
				switch (e.TransformMode)
				{
					case ViewControls3DButtons.Translate:
						if (gcode2DWidget != null)
						{
							gcode2DWidget.TransformState = GCode2DWidget.ETransformState.Move;
						}
						break;

					case ViewControls3DButtons.Scale:
						if (gcode2DWidget != null)
						{
							gcode2DWidget.TransformState = GCode2DWidget.ETransformState.Scale;
						}
						break;
				}
			};

			viewControls3D.ResetView += (sender, e) =>
			{
				if (gcode2DWidget?.Visible == true)
				{
					gcode2DWidget.CenterPartInView();
				}
			};
			viewControls3D.ViewModeChanged += (s, e) =>
			{
				this.ViewMode = e.ViewMode;
			};

			int sliderWidth = (UserSettings.Instance.IsTouchScreen) ? 20 : 10;

			layerCountText = new TextWidget("", pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Visible = false,
				AutoExpandBoundsToText = true
			};

			layerStartText = new TextWidget("1", pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Visible = false,
				AutoExpandBoundsToText = true
			};

			selectLayerSlider = new SolidSlider(new Vector2(), sliderWidth, 0, 1, Orientation.Vertical)
			{
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(0, 20, 0, 10)
			};
			selectLayerSlider.ValueChanged += (s, e) =>
			{
				if (printer?.Bed?.RenderInfo != null)
				{
					sceneContext.ActiveLayerIndex = (int)(selectLayerSlider.Value + .5);
				}

				// show the layer info next to the slider
				this.Invalidate();
			};

			layerRenderRatioSlider = new DoubleSolidSlider(new Vector2(), sliderWidth);
			layerRenderRatioSlider.FirstValue = 0;
			layerRenderRatioSlider.FirstValueChanged += (s, e) =>
			{
				sceneContext.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
				sceneContext.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;

				this.Invalidate();
			};
			layerRenderRatioSlider.SecondValue = 1;
			layerRenderRatioSlider.SecondValueChanged += (s, e) =>
			{
				if (printer?.Bed?.RenderInfo != null)
				{
					sceneContext.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
					sceneContext.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;
				}

				this.Invalidate();
			};

			currentLayerInfo = new ValueDisplayInfo("1000")
			{
				GetDisplayString = (value) => $"{value + 1}"
			};

			currentLayerInfo.EditComplete += (s, e) =>
			{
				sceneContext.ActiveLayerIndex = (int)currentLayerInfo.Value - 1;
			};

			AddSettingsTabBar(leftToRight, modelViewer);

			view3DContainer.AddChild(layerRenderRatioSlider);
			view3DContainer.AddChild(selectLayerSlider);
			view3DContainer.AddChild(layerCountText);
			view3DContainer.AddChild(layerStartText);

			sceneContext.ActiveLayerChanged += SetPositionAndValue;
			selectLayerSlider.MouseEnter += SetPositionAndValue;

			sceneContext.LoadedGCodeChanged += BedPlate_LoadedGCodeChanged;

			this.ShowSliceLayers = false;

			currentLayerInfo.Visible = false;

			view3DContainer.AddChild(currentLayerInfo);

			modelViewer.BoundsChanged += (s, e) =>
			{
				SetSliderSizes();
			};

			// Must come after we have an instance of View3DWidget an its undo buffer
			topToBottom.AddChild(new PrinterActionsBar(printer, modelViewer, this)
			{
				Padding = theme.ToolbarPadding
			}, 0);
			sceneContext.ActiveLayerChanged += ActiveLayer_Changed;

			SetSliderSizes();
		}

		private GCodeFile loadedGCode => sceneContext.LoadedGCode;

		private bool showSliceLayers;
		private bool ShowSliceLayers
		{
			get => showSliceLayers;
			set
			{
				showSliceLayers = value;
				modelViewer.gcodeViewer.Visible = value;

				modelViewer.meshViewerWidget.IsActive = !value;

				if (showSliceLayers)
				{
					printer.Bed.Scene.ClearSelection();
				}

				var slidersVisible = sceneContext.RenderInfo != null && value;

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
							if (gcode2DWidget != null)
							{
								gcode2DWidget.Visible = true;

								// HACK: Getting the Layer2D view to show content only works if CenterPartInView is called after the control is visible and after some cycles have passed
								UiThread.RunOnIdle(gcode2DWidget.CenterPartInView);
							}
							this.ShowSliceLayers = true;
							break;

						case PartViewMode.Layers3D:
							UserSettings.Instance.set("LayerViewDefault", "3D Layer");
							if (gcode2DWidget != null)
							{
								gcode2DWidget.Visible = false;
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

		private void BedPlate_LoadedGCodeChanged(object sender, EventArgs e)
		{
			var layerCount = sceneContext.LoadedGCode.LayerCount;
			selectLayerSlider.Maximum = layerCount - 1;

			layerCountText.Text = layerCount.ToString();
			layerCountText.Visible = true;
			layerStartText.Visible = true;

			// ResetRenderInfo
			sceneContext.RenderInfo = new GCodeRenderInfo(
				0,
				1,
				Agg.Transform.Affine.NewIdentity(),
				1,
				0,
				1,
				new Vector2[]
				{
					printer.Settings.Helpers.ExtruderOffset(0),
					printer.Settings.Helpers.ExtruderOffset(1)
				},
				this.GetRenderType,
				MeshViewerWidget.GetExtruderColor);

			// Close and remove any existing widget reference
			gcode2DWidget?.Close();

			var viewerVolume = sceneContext.ViewerVolume;

			// Create and append new widget
			gcode2DWidget = new GCode2DWidget(new Vector2(viewerVolume.x, viewerVolume.y), sceneContext.BedCenter)
			{
				Visible = (this.ViewMode == PartViewMode.Layers2D)
			};
			view3DContainer.AddChild(gcode2DWidget);

			viewControls3D.Layers2DButton.Enabled = true;
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

		private void SetSyncToPrintVisibility()
		{
			bool printerIsRunningPrint = printer.Connection.PrinterIsPaused || printer.Connection.PrinterIsPrinting;

			if (gcodeOptions.SyncToPrint && printerIsRunningPrint)
			{
				SetAnimationPosition();
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

				layerRenderRatioSlider.Visible = true;
				selectLayerSlider.Visible = true;
			}
		}

		// TODO: Moved from View3DWidget as printer specialized logic can't be in the generic base. Consider moving to model
		private bool PartsAreInPrintVolume()
		{
			AxisAlignedBoundingBox allBounds = AxisAlignedBoundingBox.Empty;
			foreach (var aabb in printer.Bed.Scene.Children.Select(item => item.GetAxisAlignedBoundingBox(Matrix4X4.Identity)))
			{
				allBounds += aabb;
			}

			bool onBed = allBounds.minXYZ.z > -.001 && allBounds.minXYZ.z < .001; // really close to the bed
			RectangleDouble bedRect = new RectangleDouble(0, 0, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).x, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).y);
			bedRect.Offset(printer.Settings.GetValue<Vector2>(SettingsKey.print_center) - printer.Settings.GetValue<Vector2>(SettingsKey.bed_size) / 2);

			bool inBounds = bedRect.Contains(new Vector2(allBounds.minXYZ)) && bedRect.Contains(new Vector2(allBounds.maxXYZ));

			return onBed && inBounds;
		}

		private void SetSliderSizes()
		{
			if (selectLayerSlider == null || modelViewer == null)
			{
				return;
			}

			selectLayerSlider.OriginRelativeParent = new Vector2(modelViewer.Width - 20, 78);
			selectLayerSlider.TotalWidthInPixels = modelViewer.Height - 100;

			layerRenderRatioSlider.OriginRelativeParent = new Vector2(11, 65);
			layerRenderRatioSlider.TotalWidthInPixels = modelViewer.Width - 45;

			layerCountText.OriginRelativeParent = new Vector2(modelViewer.Width - 26 + (layerCountText.Width / 2), modelViewer.Height - 15);
			layerStartText.OriginRelativeParent = new Vector2(modelViewer.Width - 26 + (layerStartText.Width / 2), 63);
		}
		private void SetAnimationPosition()
		{
			int currentLayer = printer.Connection.CurrentlyPrintingLayer;
			if (currentLayer <= 0)
			{
				selectLayerSlider.Value = 0;
				layerRenderRatioSlider.SecondValue = 0;
				layerRenderRatioSlider.FirstValue = 0;
			}
			else
			{
				selectLayerSlider.Value = currentLayer - 1;
				layerRenderRatioSlider.SecondValue = printer.Connection.RatioIntoCurrentLayer;
				layerRenderRatioSlider.FirstValue = 0;
			}
		}

		private void ActiveLayer_Changed(object sender, EventArgs e)
		{
			if (selectLayerSlider != null
				&& sceneContext.ActiveLayerIndex != (int)(selectLayerSlider.Value + .5))
			{
				selectLayerSlider.Value = sceneContext.ActiveLayerIndex;
			}
		}

		private void SetPositionAndValue(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				currentLayerInfo.Value = sceneContext.ActiveLayerIndex;
				//currentLayerInfo.DebugShowBounds = true;
				currentLayerInfo.OriginRelativeParent = selectLayerSlider.OriginRelativeParent
					+ new Vector2(-currentLayerInfo.Width - 10, selectLayerSlider.PositionPixelsFromFirstValue - currentLayerInfo.Height / 2);
				currentLayerInfo.Visible = true;
			});
		}

		internal GuiWidget ShowGCodeOverflowMenu()
		{
			var textColor = RGBA_Bytes.Black;

			var popupContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
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
				HAnchor = HAnchor.Left,
			};
			transparentExtrusion.CheckedStateChanged += (sender, e) =>
			{
				gcodeOptions.TransparentExtrusion = transparentExtrusion.Checked;
			};
			popupContainer.AddChild(transparentExtrusion);

			// Extrusion checkbox
			if (printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1)
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

		public override void OnDraw(Graphics2D graphics2D)
		{
			bool printerIsRunningPrint = printer.Connection.PrinterIsPaused || printer.Connection.PrinterIsPrinting;
			if (gcodeOptions.SyncToPrint
				&& printerIsRunningPrint
				&& modelViewer.gcodeViewer.Visible)
			{
				SetAnimationPosition();
				this.Invalidate();
			}

			base.OnDraw(graphics2D);
		}

		protected override GuiWidget GetViewControls3DOverflowMenu()
		{
			if (modelViewer.gcodeViewer.Visible)
			{
				return this.ShowGCodeOverflowMenu();
			}
			else
			{
				return modelViewer.ShowOverflowMenu();
			}
		}

		public override void OnLoad(EventArgs args)
		{
			// Find and hook the parent system window KeyDown event
			if (this.Parents<SystemWindow>().FirstOrDefault() is SystemWindow systemWindow)
			{
				systemWindow.KeyDown += Parent_KeyDown;
				parentSystemWindow = systemWindow;
			}

			base.OnLoad(args);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			// Find and unhook the parent system window KeyDown event
			if (parentSystemWindow != null)
			{
				parentSystemWindow.KeyDown -= Parent_KeyDown;
			}

			sceneContext.ActiveLayerChanged -= ActiveLayer_Changed;
			sceneContext.LoadedGCodeChanged -= BedPlate_LoadedGCodeChanged;

			sceneContext.ActiveLayerChanged -= SetPositionAndValue;
			selectLayerSlider.MouseEnter -= SetPositionAndValue;

			base.OnClosed(e);
		}

		private void Parent_KeyDown(object sender, KeyEventArgs keyEvent)
		{
			if (modelViewer.gcodeViewer.Visible)
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.Up:
						sceneContext.ActiveLayerIndex += 1;
						break;
					case Keys.Down:
						sceneContext.ActiveLayerIndex -= 1;
						break;
				}
			}
		}

		private void AddSettingsTabBar(GuiWidget parent, GuiWidget widgetTodockTo)
		{
			var sideBar = new DockingTabControl(widgetTodockTo, DockSide.Right, ApplicationController.Instance.ActivePrinter)
			{
				ControlIsPinned = ApplicationController.Instance.ActivePrinter.ViewState.SliceSettingsTabPinned
			};
			sideBar.PinStatusChanged += (s, e) =>
			{
				ApplicationController.Instance.ActivePrinter.ViewState.SliceSettingsTabPinned = sideBar.ControlIsPinned;
			};
			parent.AddChild(sideBar);

			if (printer.Settings.PrinterSelected)
			{
				sideBar.AddPage(
					"Slice Settings".Localize(), 
					new SliceSettingsWidget(
						printer, 
						new SettingsContext(
							printer,
							null, 
							SlicerConfiguration.NamedSettingsLayers.All)));
			}
			else
			{
				sideBar.AddPage("Slice Settings".Localize(), new SliceSettingsMissingWidget());
			}

			sideBar.AddPage("Controls".Localize(), new ManualPrinterControls(printer));

			sideBar.AddPage("Terminal".Localize(), new TerminalWidget(printer)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch
			});
		}
	}
}
