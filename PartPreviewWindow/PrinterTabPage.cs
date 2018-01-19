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
using System.IO;
using System.Linq;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterTabPage : PartTabPage
	{
		internal GCode2DWidget gcode2DWidget;

		private View3DConfig gcodeOptions;
		private DoubleSolidSlider layerRenderRatioSlider;
		private SystemWindow parentSystemWindow;
		private SliceLayerSelector layerScrollbar;
		internal PrinterConfig printer;
		internal GCode3DWidget gcode3DWidget;
		internal PrinterActionsBar printerActionsBar;
		private DockingTabControl sideBar;
		private SliceSettingsWidget sliceSettingsWidget;

		public PrinterTabPage(PrinterConfig printer, ThemeConfig theme, string tabTitle)
			: base(printer, printer.Bed, theme, tabTitle)
		{
			this.printer = printer;
			view3DWidget.meshViewerWidget.EditorMode = MeshViewerWidget.EditorType.Printer;

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

			printer.ViewState.ViewModeChanged += (s, e) =>
			{
				this.SetViewMode(e.ViewMode);
			};

			layerScrollbar = new SliceLayerSelector(printer, sceneContext)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Right | HAnchor.Absolute,
				Width = 60,
				Margin = new BorderDouble(0, 80, 8, 42),
				Maximum = sceneContext.LoadedGCode?.LayerCount ?? 1
			};
			view3DContainer.AddChild(layerScrollbar);

			layerRenderRatioSlider = new DoubleSolidSlider(new Vector2(), SliceLayerSelector.SliderWidth);
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
			view3DContainer.AddChild(layerRenderRatioSlider);

			sceneContext.LoadedGCodeChanged += BedPlate_LoadedGCodeChanged;

			AddSettingsTabBar(leftToRight, view3DWidget);

			view3DWidget.BoundsChanged += (s, e) =>
			{
				SetSliderSizes();
			};

			printerActionsBar = new PrinterActionsBar(printer, this, theme);
			printerActionsBar.OverflowMenu.BackgroundColor = theme.ResolveColor(theme.TabBodyBackground, theme.TabBodyBackground);

			// Must come after we have an instance of View3DWidget an its undo buffer
			topToBottom.AddChild(printerActionsBar, 0);

			topToBottom.AddChild(new HorizontalLine(20), 1);

			var trackball = view3DWidget.InteractionLayer.Children<TrackballTumbleWidget>().FirstOrDefault();

			var position = view3DWidget.InteractionLayer.Children.IndexOf(trackball);

			// The slice layers view
			gcode3DWidget = new GCode3DWidget(printer, sceneContext, theme)
			{
				Name = "GCode3DWidget",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Visible = false
			};
			view3DWidget.InteractionLayer.AddChild(gcode3DWidget, position);

			var viewerVolume = sceneContext.ViewerVolume;

			view3DContainer.AddChild(new RunningTasksWidget(theme)
			{
				MinimumSize = new Vector2(100, 0),
				Margin = new BorderDouble(left: 10, bottom: 90),
				VAnchor = VAnchor.Bottom | VAnchor.Fit,
				HAnchor = HAnchor.Left | HAnchor.Fit,
			});

			// Create and append new widget
			gcode2DWidget = new GCode2DWidget(new Vector2(viewerVolume.X, viewerVolume.Y), sceneContext.BedCenter)
			{
				Visible = (printer.ViewState.ViewMode == PartViewMode.Layers2D)
			};
			view3DWidget.InteractionLayer.AddChild(gcode2DWidget);

			SetSliderSizes();

			this.SetViewMode(printer.ViewState.ViewMode);

			printer.ViewState.ConfigurePrinterChanged += ConfigurePrinter_Changed;
		}

		private void ConfigurePrinter_Changed(object sender, EventArgs e)
		{
			this.ProcessOptionalTab();
		}

		private void SetViewMode(PartViewMode viewMode)
		{
			if (gcode3DWidget == null || gcode2DWidget == null)
			{
				// Wait for controls to initialize
				return;
			}

			switch (viewMode)
			{
				case PartViewMode.Layers2D:
					UserSettings.Instance.set("LayerViewDefault", "2D Layer");
					// HACK: Getting the Layer2D view to show content only works if CenterPartInView is called after the control is visible and after some cycles have passed
					gcode2DWidget.Visible = true;
					UiThread.RunOnIdle(gcode2DWidget.CenterPartInView);
					break;

				case PartViewMode.Layers3D:
					UserSettings.Instance.set("LayerViewDefault", "3D Layer");
					break;

				case PartViewMode.Model:
					break;
			}

			bool showSliceLayers = viewMode == PartViewMode.Layers3D;

			gcode3DWidget.Visible = viewMode == PartViewMode.Layers3D;
			gcode2DWidget.Visible = viewMode == PartViewMode.Layers2D;

			view3DWidget.meshViewerWidget.ModelView = viewMode == PartViewMode.Model;

			if (showSliceLayers)
			{
				printer.Bed.Scene.ClearSelection();
			}

			var slidersVisible = viewMode != PartViewMode.Model && printer.Bed.LoadedGCode?.LayerCount > 0;

			layerScrollbar.Visible = slidersVisible;
			layerRenderRatioSlider.Visible = slidersVisible;

			view3DWidget.selectedObjectContainer.Visible = view3DWidget.meshViewerWidget.ModelView
				&& sceneContext.Scene.HasSelection
				&& printer?.ViewState.ViewMode != PartViewMode.Layers2D;
		}

		private void BedPlate_LoadedGCodeChanged(object sender, EventArgs e)
		{
			bool gcodeLoaded = sceneContext.LoadedGCode != null;

			layerScrollbar.Visible = gcodeLoaded;
			layerRenderRatioSlider.Visible = gcodeLoaded;

			if (!gcodeLoaded)
			{
				return;
			}

			layerScrollbar.Maximum = sceneContext.LoadedGCode.LayerCount;
		}

		private void SetSyncToPrintVisibility()
		{
			bool printerIsRunningPrint = printer.Connection.PrinterIsPaused || printer.Connection.PrinterIsPrinting;

			if (gcodeOptions.SyncToPrint && printerIsRunningPrint)
			{
				SetAnimationPosition();
				layerRenderRatioSlider.Visible = false;
				layerScrollbar.Visible = false;
			}
			else
			{
				if (layerRenderRatioSlider != null)
				{
					layerRenderRatioSlider.FirstValue = 0;
					layerRenderRatioSlider.SecondValue = 1;
				}

				layerRenderRatioSlider.Visible = true;
				layerScrollbar.Visible = true;
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

			bool onBed = allBounds.minXYZ.Z > -.001 && allBounds.minXYZ.Z < .001; // really close to the bed
			RectangleDouble bedRect = new RectangleDouble(0, 0, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).X, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).Y);
			bedRect.Offset(printer.Settings.GetValue<Vector2>(SettingsKey.print_center) - printer.Settings.GetValue<Vector2>(SettingsKey.bed_size) / 2);

			bool inBounds = bedRect.Contains(new Vector2(allBounds.minXYZ)) && bedRect.Contains(new Vector2(allBounds.maxXYZ));

			return onBed && inBounds;
		}

		private void SetSliderSizes()
		{
			if (layerScrollbar == null || view3DWidget == null)
			{
				return;
			}

			
			layerRenderRatioSlider.OriginRelativeParent = new Vector2(11, 65);
			layerRenderRatioSlider.TotalWidthInPixels = view3DWidget.Width - 45;
		}

		private void SetAnimationPosition()
		{
			int currentLayer = printer.Connection.CurrentlyPrintingLayer;
			if (currentLayer <= 0)
			{
				layerScrollbar.Value = 0;
				layerRenderRatioSlider.SecondValue = 0;
				layerRenderRatioSlider.FirstValue = 0;
			}
			else
			{
				layerScrollbar.Value = currentLayer - 1;
				layerRenderRatioSlider.SecondValue = printer.Connection.RatioIntoCurrentLayer;
				layerRenderRatioSlider.FirstValue = 0;
			}
		}

		internal GuiWidget ShowGCodeOverflowMenu()
		{
			var popupMenu = new PopupMenu(theme);

			popupMenu.CreateBoolMenuItem(
				"Show Print Bed".Localize(),
				() => gcodeOptions.RenderBed,
				(value) =>
				{
					gcodeOptions.RenderBed = value;
					view3DWidget.meshViewerWidget.RenderBed = value;
				});

			popupMenu.CreateBoolMenuItem(
				"Moves".Localize(),
				() => gcodeOptions.RenderMoves,
				(value) => gcodeOptions.RenderMoves = value);

			popupMenu.CreateBoolMenuItem(
				"Retractions".Localize(),
				() => gcodeOptions.RenderRetractions,
				(value) => gcodeOptions.RenderRetractions = value);

			popupMenu.CreateBoolMenuItem(
				"Speeds".Localize(),
				() => gcodeOptions.RenderSpeeds,
				(value) => gcodeOptions.RenderSpeeds = value);

			popupMenu.CreateHorizontalLine();

			popupMenu.CreateBoolMenuItem(
				"Extrusion".Localize(),
				() => gcodeOptions.SimulateExtrusion,
				(value) => gcodeOptions.SimulateExtrusion = value);

			popupMenu.CreateBoolMenuItem(
				"Transparent".Localize(),
				() => gcodeOptions.TransparentExtrusion,
				(value) => gcodeOptions.TransparentExtrusion = value);

			popupMenu.CreateHorizontalLine();

			if (printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1)
			{
				popupMenu.CreateBoolMenuItem(
					"Hide Offsets".Localize(),
					() => gcodeOptions.HideExtruderOffsets,
					(value) => gcodeOptions.HideExtruderOffsets = value);
			}

			popupMenu.CreateBoolMenuItem(
				"Sync To Print".Localize(),
				() => gcodeOptions.SyncToPrint,
				(value) =>
				{
					gcodeOptions.SyncToPrint = value;
					if (!gcodeOptions.SyncToPrint)
					{
						// If we are turning off sync to print, set the slider to full.
						layerRenderRatioSlider.SecondValue = 1;
					}
				});

			return popupMenu;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			bool printerIsRunningPrint = printer.Connection.PrinterIsPaused || printer.Connection.PrinterIsPrinting;
			if (gcodeOptions.SyncToPrint
				&& printerIsRunningPrint
				&& (gcode3DWidget.Visible || gcode2DWidget.Visible))
			{
				SetAnimationPosition();
				this.Invalidate();
			}

			base.OnDraw(graphics2D);
		}

		protected override GuiWidget GetViewControls3DOverflowMenu()
		{
			if (gcode3DWidget.Visible
				|| gcode2DWidget.Visible)
			{
				return this.ShowGCodeOverflowMenu();
			}
			else
			{
				return view3DWidget.ShowOverflowMenu();
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

			sceneContext.LoadedGCodeChanged -= BedPlate_LoadedGCodeChanged;
			printer.ViewState.ConfigurePrinterChanged -= ConfigurePrinter_Changed;

			base.OnClosed(e);
		}

		private void Parent_KeyDown(object sender, KeyEventArgs keyEvent)
		{
			if (!keyEvent.Handled
				&& (gcode3DWidget.Visible
				|| gcode2DWidget.Visible))
			{
				switch (keyEvent.KeyCode)
				{
					case Keys.Up:
						layerScrollbar.Value += 1;
						break;
					case Keys.Down:
						layerScrollbar.Value -= 1;
						break;
				}
			}
		}

		private void AddSettingsTabBar(GuiWidget parent, GuiWidget widgetTodockTo)
		{
			sideBar = new DockingTabControl(widgetTodockTo, DockSide.Right, ApplicationController.Instance.ActivePrinter)
			{
				Name = "DockingTabControl",
				ControlIsPinned = ApplicationController.Instance.ActivePrinter.ViewState.SliceSettingsTabPinned
			};
			sideBar.PinStatusChanged += (s, e) =>
			{
				ApplicationController.Instance.ActivePrinter.ViewState.SliceSettingsTabPinned = sideBar.ControlIsPinned;
			};
			parent.AddChild(sideBar);

			sideBar.AddPage(
				"Slice Settings".Localize(),
				sliceSettingsWidget = new SliceSettingsWidget(
					printer,
					new SettingsContext(
						printer,
						null,
						NamedSettingsLayers.All),
					theme));

			sideBar.AddPage("Controls".Localize(), new ManualPrinterControls(printer));

			sideBar.AddPage("Terminal".Localize(), new TerminalWidget(printer)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch
			});

			this.ProcessOptionalTab();
		}

		private void ProcessOptionalTab()
		{
			if (ApplicationController.Instance.ActivePrinter.ViewState.ConfigurePrinterVisible)
			{
				sideBar.AddPage(
					"Printer".Localize(),
					new ConfigurePrinterWidget(sliceSettingsWidget.settingsContext, printer, theme)
					{
						BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor,
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Stretch,
					});
			}
			else
			{
				sideBar.RemovePage("Printer");
			}
		}

		public static GuiWidget PrintProgressWidget(PrinterConfig printer)
		{
			var bodyRow = new GuiWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Center,
				VAnchor = VAnchor.Top | VAnchor.Fit,
				//BackgroundColor = new Color(ActiveTheme.Instance.PrimaryBackgroundColor, 128),
				MinimumSize = new Vector2(275, 140),
				Selectable = false
			};

			// Progress section
			var expandingContainer = new HorizontalSpacer()
			{
				VAnchor = VAnchor.Fit | VAnchor.Center
			};
			bodyRow.AddChild(expandingContainer);

			var progressContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(50, 0),
				VAnchor = VAnchor.Center | VAnchor.Fit,
				HAnchor = HAnchor.Center | HAnchor.Fit,
			};
			expandingContainer.AddChild(progressContainer);

			var progressDial = new ProgressDial()
			{
				HAnchor = HAnchor.Center,
				Height = 200 * DeviceScale,
				Width = 200 * DeviceScale
			};
			progressContainer.AddChild(progressDial);

			var timeContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Center | HAnchor.Fit,
				Margin = 3
			};
			progressContainer.AddChild(timeContainer);

			timeContainer.AddChild(new ImageWidget(AggContext.StaticData.LoadIcon("fa-clock_24.png", IconColor.Theme))
			{
				VAnchor = VAnchor.Center
			});

			var timeWidget = new TextWidget("", pointSize: 22, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(10, 0, 0, 0),
				VAnchor = VAnchor.Center,
			};

			timeContainer.AddChild(timeWidget);

			Action updatePrintProgress = null;
			updatePrintProgress = () =>
			{
				int secondsPrinted = printer.Connection.SecondsPrinted;
				int hoursPrinted = (int)(secondsPrinted / (60 * 60));
				int minutesPrinted = (secondsPrinted / 60 - hoursPrinted * 60);
				secondsPrinted = secondsPrinted % 60;

				// TODO: Consider if the consistency of a common time format would look and feel better than changing formats based on elapsed duration
				timeWidget.Text = (hoursPrinted <= 0) ? $"{minutesPrinted}:{secondsPrinted:00}" : $"{hoursPrinted}:{minutesPrinted:00}:{secondsPrinted:00}";

				progressDial.LayerCount = printer.Connection.CurrentlyPrintingLayer;
				progressDial.LayerCompletedRatio = printer.Connection.RatioIntoCurrentLayer;
				progressDial.CompletedRatio = printer.Connection.PercentComplete / 100;

				if (!bodyRow.HasBeenClosed)
				{
					switch (printer.Connection.CommunicationState)
					{
						case CommunicationStates.PreparingToPrint:
						case CommunicationStates.Printing:
						case CommunicationStates.Paused:
							bodyRow.Visible = true;
							break;

						default:
							bodyRow.Visible = false;
							break;
					}

					UiThread.RunOnIdle(updatePrintProgress, 1);
				}
			};

			UiThread.RunOnIdle(updatePrintProgress, 1);

			bodyRow.Visible = false;

			return bodyRow;
		}
	}
}
