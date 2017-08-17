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
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.PrintQueue;
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

		public PrinterTabPage(PrinterConfig printer, ThemeConfig theme, PrintItemWrapper printItem, string tabTitle)
			: base(printer, theme, printItem, tabTitle)
		{
			modelViewer.meshViewerWidget.EditorMode = MeshViewerWidget.EditorType.Printer;

			gcodeOptions = printer.Bed.RendererOptions;

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

			selectLayerSlider = new SolidSlider(new Vector2(), sliderWidth, 0, 1, Orientation.Vertical);
			selectLayerSlider.ValueChanged += (s, e) =>
			{
				// TODO: Why would these need to be updated here as well as in the horizontal slider?
				printer.Bed.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
				printer.Bed.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;

				printer.Bed.ActiveLayerIndex = (int)(selectLayerSlider.Value + .5);

				// show the layer info next to the slider

				this.Invalidate();
			};

			layerRenderRatioSlider = new DoubleSolidSlider(new Vector2(), sliderWidth);
			layerRenderRatioSlider.FirstValue = 0;
			layerRenderRatioSlider.FirstValueChanged += (s, e) =>
			{
				printer.Bed.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
				printer.Bed.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;

				this.Invalidate();
			};
			layerRenderRatioSlider.SecondValue = 1;
			layerRenderRatioSlider.SecondValueChanged += (s, e) =>
			{
				printer.Bed.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
				printer.Bed.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;

				this.Invalidate();
			};
			
			currentLayerInfo = new ValueDisplayInfo("1000")
			{
				GetDisplayString = (value) => $"{value + 1}"
			};

			currentLayerInfo.EditComplete += (s, e) =>
			{
				printer.Bed.ActiveLayerIndex = (int)currentLayerInfo.Value - 1;
			};

			AddSettingsTabBar(leftToRight, modelViewer);

			view3DContainer.AddChild(layerRenderRatioSlider);
			view3DContainer.AddChild(selectLayerSlider);
			view3DContainer.AddChild(layerCountText);
			view3DContainer.AddChild(layerStartText);

			printer.Bed.ActiveLayerChanged += SetPositionAndValue;
			selectLayerSlider.MouseEnter += SetPositionAndValue;

			printer.Bed.LoadedGCodeChanged += BedPlate_LoadedGCodeChanged;

			this.ShowSliceLayers = false;

			currentLayerInfo.Visible = false;

			view3DContainer.AddChild(currentLayerInfo);

			modelViewer.BoundsChanged += (s, e) =>
			{
				SetSliderSizes();
			};

			// Must come after we have an instance of View3DWidget an its undo buffer
			topToBottom.AddChild(new PrinterActionsBar(modelViewer, this)
			{
				Padding = theme.ToolbarPadding
			}, 0);
			printer.Bed.ActiveLayerChanged += ActiveLayer_Changed;

			SetSliderSizes();
		}

		private GCodeFile loadedGCode => printer.Bed.LoadedGCode;

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
					modelViewer.Scene.ClearSelection();
				}

				var slidersVisible = printer.Bed.RenderInfo != null && value;

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
			var layerCount = printer.Bed.LoadedGCode.LayerCount;
			selectLayerSlider.Maximum = layerCount - 1;

			layerCountText.Text = layerCount.ToString();
			layerCountText.Visible = true;
			layerStartText.Visible = true;

			// ResetRenderInfo
			printer.Bed.RenderInfo = new GCodeRenderInfo(
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
				MeshViewerWidget.GetExtruderColor);

			// Close and remove any existing widget reference
			gcode2DWidget?.Close();

			var viewerVolume = printer.Bed.ViewerVolume;

			// Create and append new widget
			gcode2DWidget = new GCode2DWidget(new Vector2(viewerVolume.x, viewerVolume.y), printer.Bed.BedCenter)
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
			bool printerIsRunningPrint = PrinterConnection.Instance.PrinterIsPaused || PrinterConnection.Instance.PrinterIsPrinting;

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
				&& printer.Bed.ActiveLayerIndex != (int)(selectLayerSlider.Value + .5))
			{
				selectLayerSlider.Value = printer.Bed.ActiveLayerIndex;
			}
		}

		private void SetPositionAndValue(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				currentLayerInfo.Value = printer.Bed.ActiveLayerIndex;
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

		public override void OnDraw(Graphics2D graphics2D)
		{
			bool printerIsRunningPrint = PrinterConnection.Instance.PrinterIsPaused || PrinterConnection.Instance.PrinterIsPrinting;
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

			printer.Bed.ActiveLayerChanged -= ActiveLayer_Changed;
			printer.Bed.LoadedGCodeChanged -= BedPlate_LoadedGCodeChanged;

			printer.Bed.ActiveLayerChanged -= SetPositionAndValue;
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
						printer.Bed.ActiveLayerIndex += 1;
						break;
					case Keys.Down:
						printer.Bed.ActiveLayerIndex -= 1;
						break;
				}
			}
		}

		private void AddSettingsTabBar(GuiWidget parent, GuiWidget widgetTodockTo)
		{
			var sideBar = new DockingTabControl(widgetTodockTo, DockSide.Right, ApplicationController.Instance.Printer)
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
			terminalControls.VAnchor |= VAnchor.Stretch;
			sideBar.AddPage("Terminal".Localize(), terminalControls);
		}
	}

	public class PrinterTabBase : TabPage
	{
		internal View3DWidget modelViewer;

		private PrintItemWrapper printItem;
		protected ViewControls3D viewControls3D;

		protected PrinterConfig printer;
		protected ThemeConfig theme;

		protected GuiWidget view3DContainer;
		protected FlowLayoutWidget topToBottom;
		protected FlowLayoutWidget leftToRight;

		public PrinterTabBase(PrinterConfig printer, ThemeConfig theme, PrintItemWrapper printItem, string tabTitle)
			: base (tabTitle)
		{
			this.printer = printer;
			this.theme = theme;
			this.BackgroundColor = theme.TabBodyBackground;
			this.Padding = 0;

			viewControls3D = new ViewControls3D(theme, printer.Bed.Scene.UndoBuffer)
			{
				PartSelectVisible = false,
				VAnchor = VAnchor.Top | VAnchor.Fit | VAnchor.Absolute,
				HAnchor = HAnchor.Left | HAnchor.Fit,
				Visible = true,
				Margin = new BorderDouble(6, 0, 0, 43)
			};
			viewControls3D.ResetView += (sender, e) =>
			{
				if (modelViewer.Visible)
				{
					this.modelViewer.ResetView();
				}
			};
			viewControls3D.OverflowButton.DynamicPopupContent = () =>
			{
				return this.GetViewControls3DOverflowMenu();
			};

			bool isPrinterType = this.GetType() == typeof(PrinterTabPage);

			// The 3D model view
			modelViewer = new View3DWidget(printItem,
				printer,
				View3DWidget.AutoRotate.Disabled,
				viewControls3D,
				theme,
				View3DWidget.OpenMode.Editing,
				editorType: (isPrinterType) ? MeshViewerWidget.EditorType.Printer : MeshViewerWidget.EditorType.Part);

			topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			this.AddChild(topToBottom);

			leftToRight = new FlowLayoutWidget();
			leftToRight.Name = "View3DContainerParent";
			leftToRight.AnchorAll();
			topToBottom.AddChild(leftToRight);

			view3DContainer = new GuiWidget();
			view3DContainer.AnchorAll();
			view3DContainer.AddChild(modelViewer);

			leftToRight.AddChild(view3DContainer);

			modelViewer.BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor;

			if (ApplicationController.Instance.PartPreviewState.RotationMatrix == Matrix4X4.Identity)
			{
				this.modelViewer.ResetView();

				ApplicationController.Instance.PartPreviewState.RotationMatrix = modelViewer.World.RotationMatrix;
				ApplicationController.Instance.PartPreviewState.TranslationMatrix = modelViewer.World.TranslationMatrix;
			}
			else
			{
				modelViewer.World.RotationMatrix = ApplicationController.Instance.PartPreviewState.RotationMatrix;
				modelViewer.World.TranslationMatrix = ApplicationController.Instance.PartPreviewState.TranslationMatrix;
			}

			this.printItem = printItem;

			this.AddChild(viewControls3D);

			this.AnchorAll();
		}

		protected virtual GuiWidget GetViewControls3DOverflowMenu()
		{
			return modelViewer.ShowOverflowMenu();
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
			// Store active transforms on close
			ApplicationController.Instance.PartPreviewState.RotationMatrix = modelViewer.World.RotationMatrix;
			ApplicationController.Instance.PartPreviewState.TranslationMatrix = modelViewer.World.TranslationMatrix;

			base.OnClosed(e);
		}
	}
}
