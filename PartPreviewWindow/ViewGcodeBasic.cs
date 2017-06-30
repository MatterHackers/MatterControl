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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ViewGcodeBasic : GuiWidget
	{
		private MeshViewerWidget externalMeshViewer;

		public enum WindowMode { Embeded, StandAlone };

		public SolidSlider selectLayerSlider;

		private SetLayerWidget setLayerWidget;
		private LayerNavigationWidget navigationWidget;
		public DoubleSolidSlider layerRenderRatioSlider;

		private TextWidget gcodeProcessingStateInfoText;
		private GCode2DWidget gcode2DWidget;
		private PrintItemWrapper printItem => ApplicationController.Instance.ActivePrintItem;
		private bool startedSliceFromGenerateButton = false;
		private FlowLayoutWidget buttonBottomPanel;
		private FlowLayoutWidget layerSelectionButtonsPanel;

		private ViewControlsToggle viewControlsToggle;

		private GuiWidget gcodeDisplayWidget;

		private ColorGradientWidget gradientWidget;

		private EventHandler unregisterEvents;
		private WindowMode windowMode;

		public delegate Vector2 GetSizeFunction();

		private string gcodeLoading = "Loading G-Code".Localize();
		private string slicingErrorMessage = "Slicing Error.\nPlease review your slice settings.".Localize();
		private string fileNotFoundMessage = "File not found on disk.".Localize();
		private string fileTooBigToLoad = "GCode file too big to preview ({0}).".Localize();

		private Vector2 bedCenter;
		private Vector3 viewerVolume;
		private BedShape bedShape;
		private int sliderWidth;

		private PartViewMode activeViewMode = PartViewMode.Layers3D;

		private View3DConfig options;

		private PrinterConfig printer;
		private ViewControls3D viewControls3D;

		private BedConfig bedPlate;

		private SystemWindow parentSystemWindow;

		public ViewGcodeBasic(Vector3 viewerVolume, Vector2 bedCenter, BedShape bedShape, WindowMode windowMode, ViewControls3D viewControls3D, ThemeConfig theme, MeshViewerWidget externalMeshViewer)
		{
			this.externalMeshViewer = externalMeshViewer;
			this.externalMeshViewer.TrackballTumbleWidget.DrawGlContent += TrackballTumbleWidget_DrawGlContent;

			options = ApplicationController.Instance.Options.View3D;
			printer = ApplicationController.Instance.Printer;

			this.viewControls3D = viewControls3D;
			this.viewerVolume = viewerVolume;
			this.bedShape = bedShape;
			this.bedCenter = bedCenter;
			this.windowMode = windowMode;

			if (UserSettings.Instance.IsTouchScreen)
			{
				sliderWidth = 20;
			}
			else
			{
				sliderWidth = 10;
			}

			RenderOpenGl.GLHelper.WireframeColor = ActiveTheme.Instance.PrimaryAccentColor;

			CreateAndAddChildren();

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent)
				{
					if (stringEvent.Data == "extruder_offset")
					{
						printer.BedPlate.GCodeRenderer.Clear3DGCode();
					}
				}
			}, ref unregisterEvents);

			bedPlate = ApplicationController.Instance.Printer.BedPlate;

			bedPlate.ActiveLayerChanged += ActiveLayer_Changed;

			// TODO: Why do we clear GCode on AdvancedControlsPanelReloading - assume some slice settings should invalidate. If so, code should be more specific and bound to slice settings changed
			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => printer.BedPlate.GCodeRenderer?.Clear3DGCode(), ref unregisterEvents);
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

		private void Parent_KeyDown(object sender, KeyEventArgs keyEvent)
		{
			if (this.Visible)
			{
				switch(keyEvent.KeyCode)
				{
					case Keys.Up:
						bedPlate.ActiveLayerIndex += 1;
						break;
					case Keys.Down:
						bedPlate.ActiveLayerIndex -= 1;
						break;
				}
			}
		}

		private void ActiveLayer_Changed(object sender, EventArgs e)
		{
			if (selectLayerSlider != null 
				&& bedPlate.ActiveLayerIndex != (int)(selectLayerSlider.Value + .5))
			{
				selectLayerSlider.Value = bedPlate.ActiveLayerIndex;
			}
		}

		private GCodeFile loadedGCode => printer.BedPlate.LoadedGCode;

		internal void CreateAndAddChildren()
		{
			CloseAllChildren();

			var buttonFactory = ApplicationController.Instance.Theme.BreadCrumbButtonFactory;

			externalMeshViewer = null;
			gcode2DWidget = null;
			gcodeProcessingStateInfoText = null;

			FlowLayoutWidget mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainerTopToBottom.HAnchor = HAnchor.Max_FitToChildren_ParentWidth;
			mainContainerTopToBottom.VAnchor = VAnchor.Max_FitToChildren_ParentHeight;

			buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonBottomPanel.HAnchor = HAnchor.ParentLeftRight;
			buttonBottomPanel.Padding = new BorderDouble(3, 3);
			buttonBottomPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
		
			layerSelectionButtonsPanel = new FlowLayoutWidget(FlowDirection.RightToLeft);
			layerSelectionButtonsPanel.HAnchor = HAnchor.ParentLeftRight;
			layerSelectionButtonsPanel.Padding = new BorderDouble(0);

			GuiWidget holdPanelOpen = new GuiWidget(1, 40);
			layerSelectionButtonsPanel.AddChild(holdPanelOpen);

			if (windowMode == WindowMode.StandAlone)
			{
				Button closeButton = buttonFactory.Generate("Close".Localize());
				layerSelectionButtonsPanel.AddChild(closeButton);
				closeButton.Click += (sender, e) =>
				{
					CloseOnIdle();
				};
			}

			gcodeDisplayWidget = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop
			};

			if (printItem != null)
			{
				SetProcessingMessage("Loading G-Code...".Localize());

				bool isGCode = Path.GetExtension(printItem.FileLocation).ToUpper() == ".GCODE";

				string gcodeFilePath = isGCode ? printItem.FileLocation : printItem.GetGCodePathAndFileName();
				if (File.Exists(gcodeFilePath))
				{
					var gcode2DWidget = new GCode2DWidget(new Vector2(viewerVolume.x, viewerVolume.y), bedCenter)
					{
						Visible = (activeViewMode == PartViewMode.Layers2D)
					};
					gcodeDisplayWidget.AddChild(gcode2DWidget);
				}
				else
				{
					SetProcessingMessage(string.Format("{0}\n'{1}'", fileNotFoundMessage, printItem.Name));
				}
			}

			mainContainerTopToBottom.AddChild(gcodeDisplayWidget);

			// add in a spacer
			layerSelectionButtonsPanel.AddChild(new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight
			});
			buttonBottomPanel.AddChild(layerSelectionButtonsPanel);

			mainContainerTopToBottom.AddChild(buttonBottomPanel);
			this.AddChild(mainContainerTopToBottom);

			viewControls3D.ResetView += (sender, e) =>
			{
				if (gcodeDisplayWidget.Visible)
				{
					gcode2DWidget.CenterPartInView();
				}
			};

			viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;

			viewControlsToggle = new ViewControlsToggle(ApplicationController.Instance.Theme.ViewControlsButtonFactory, activeViewMode)
			{
				Visible = false,
				HAnchor = HAnchor.ParentRight
			};
			viewControlsToggle.ViewModeChanged += (s, e) =>
			{
				// Respond to user driven view mode change events and store and switch to the new mode
				activeViewMode = e.ViewMode;
				SwitchViewModes();
			};
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
			this.AddChild(viewControlsToggle);

			this.AddGCodeFileControls(ApplicationController.Instance.Printer, ApplicationController.Instance.Theme);
		}


		private void AddGCodeFileControls(PrinterConfig printer, ThemeConfig theme)
		{
			SetProcessingMessage("");
			if (gcode2DWidget != null
				&& loadedGCode == null)
			{
				// If we have finished loading the gcode and the source file exists but we don't have any loaded gcode it is because the loader decided to not load it.
				if (File.Exists(printItem.FileLocation))
				{
					SetProcessingMessage(string.Format(fileTooBigToLoad, printItem.Name));
				}
				else
				{
					SetProcessingMessage(string.Format("{0}\n'{1}'", fileNotFoundMessage, Path.GetFileName(printItem.FileLocation)));
				}
			}

			if (gcode2DWidget != null
				&& loadedGCode?.LineCount > 0)
			{
				// TODO: Shouldn't we be clearing children from some known container and rebuilding?
				gradientWidget = new ColorGradientWidget(loadedGCode);
				AddChild(gradientWidget);
				gradientWidget.Visible = false;

				gradientWidget.Visible = options.RenderSpeeds;

				viewControlsToggle.Visible = true;

				setLayerWidget = new SetLayerWidget(theme.GCodeLayerButtons, printer.BedPlate);
				setLayerWidget.VAnchor = VAnchor.ParentTop;
				layerSelectionButtonsPanel.AddChild(setLayerWidget);

				navigationWidget = new LayerNavigationWidget(theme.GCodeLayerButtons);
				navigationWidget.Margin = new BorderDouble(0, 0, 20, 0);
				layerSelectionButtonsPanel.AddChild(navigationWidget);

				selectLayerSlider = new SolidSlider(new Vector2(), sliderWidth, 0, loadedGCode.LayerCount - 1, Orientation.Vertical);
				selectLayerSlider.ValueChanged += (s, e) =>
				{
					// TODO: Why would these need to be updated here as well as in assigned in the hslider below?
					printer.BedPlate.RenderInfo.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
					printer.BedPlate.RenderInfo.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;

					printer.BedPlate.ActiveLayerIndex = (int)(selectLayerSlider.Value + .5);

					this.Invalidate();
				};

				AddChild(selectLayerSlider);

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
				AddChild(layerRenderRatioSlider);

				SetSliderSizes();

				GCodeRenderer.ExtrusionColor = ActiveTheme.Instance.PrimaryAccentColor;

				this.gcodeDetails = new GCodeDetails(this.loadedGCode);

				this.AddChild(new GCodeDetailsView(gcodeDetails)
				{
					Margin = new BorderDouble(0, 0, 35, 5),
					Padding = new BorderDouble(10),
					BackgroundColor = new RGBA_Bytes(0, 0, 0, ViewControlsBase.overlayAlpha),
					HAnchor = HAnchor.ParentRight | HAnchor.AbsolutePosition,
					VAnchor = VAnchor.ParentTop | VAnchor.FitToChildren,
					Width = 150
				});

				// TODO: Bad pattern - figure out how to revise
				// However if the print finished or is canceled we are going to want to get updates again. So, hook the status event
				PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(HookUpGCodeMessagesWhenDonePrinting, ref unregisterEvents);
				UiThread.RunOnIdle(SetSyncToPrintVisibility);

				// Switch to the most recent view mode, defaulting to Layers3D
				SwitchViewModes();
			}
		}


		private RenderType GetRenderType()
		{
			var options = ApplicationController.Instance.Options.View3D;

			RenderType renderType = RenderType.Extrusions;
			if (options.RenderMoves)
			{
				renderType |= RenderType.Moves;
			}
			if (options.RenderRetractions)
			{
				renderType |= RenderType.Retractions;
			}
			if (options.RenderSpeeds)
			{
				renderType |= RenderType.SpeedColors;
			}
			if (options.SimulateExtrusion)
			{
				renderType |= RenderType.SimulateExtrusion;
			}
			if (options.TransparentExtrusion)
			{
				renderType |= RenderType.TransparentExtrusion;
			}
			if (options.HideExtruderOffsets)
			{
				renderType |= RenderType.HideExtruderOffsets;
			}

			return renderType;
		}

		private void TrackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			if (loadedGCode == null || printer.BedPlate.GCodeRenderer == null || !this.Visible)
			{
				return;
			}

			printer.BedPlate.RenderExtra();
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

		private GCodeDetails gcodeDetails;

		internal GuiWidget ShowOverflowMenu()
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
			showGrid.Checked = options.RenderGrid;
			showGrid.CheckedStateChanged += (sender, e) =>
			{
				// TODO: How (if at all) do we disable bed rendering on GCode2D?
				options.RenderGrid = showGrid.Checked;
			};
			popupContainer.AddChild(showGrid);

			// put in a show moves checkbox
			var showMoves = new CheckBox("Moves".Localize(), textColor: textColor);
			showMoves.Checked = options.RenderMoves;
			showMoves.CheckedStateChanged += (sender, e) =>
			{
				options.RenderMoves = showMoves.Checked;
			};
			popupContainer.AddChild(showMoves);

			// put in a show Retractions checkbox
			CheckBox showRetractions = new CheckBox("Retractions".Localize(), textColor: textColor);
			showRetractions.Checked = options.RenderRetractions;
			showRetractions.CheckedStateChanged += (sender, e) =>
			{
				options.RenderRetractions = showRetractions.Checked;
			};
			popupContainer.AddChild(showRetractions);
			

			// Speeds checkbox
			var showSpeeds = new CheckBox("Speeds".Localize(), textColor: textColor);
			showSpeeds.Checked = options.RenderSpeeds;
			showSpeeds.CheckedStateChanged += (sender, e) =>
			{
				gradientWidget.Visible = showSpeeds.Checked;
				options.RenderSpeeds = showSpeeds.Checked;
			};

			popupContainer.AddChild(showSpeeds);

			// Extrusion checkbox
			var simulateExtrusion = new CheckBox("Extrusion".Localize(), textColor: textColor);
			simulateExtrusion.Checked = options.SimulateExtrusion;
			simulateExtrusion.CheckedStateChanged += (sender, e) =>
			{
				options.SimulateExtrusion = simulateExtrusion.Checked;
			};
			popupContainer.AddChild(simulateExtrusion);

			// Transparent checkbox
			var transparentExtrusion = new CheckBox("Transparent".Localize(), textColor: textColor)
			{
				Checked = options.TransparentExtrusion,
				Margin = new BorderDouble(5, 0, 0, 0),
				HAnchor = HAnchor.ParentLeft,
			};
			transparentExtrusion.CheckedStateChanged += (sender, e) =>
			{
				options.TransparentExtrusion = transparentExtrusion.Checked;
			};
			popupContainer.AddChild(transparentExtrusion);

			// Extrusion checkbox
			if (ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count) > 1)
			{
				CheckBox hideExtruderOffsets = new CheckBox("Hide Offsets", textColor: textColor);
				hideExtruderOffsets.Checked = options.HideExtruderOffsets;
				hideExtruderOffsets.CheckedStateChanged += (sender, e) =>
				{
					options.HideExtruderOffsets = hideExtruderOffsets.Checked;
				};
				popupContainer.AddChild(hideExtruderOffsets);
			}

			// Sync To Print checkbox
			if (windowMode == WindowMode.Embeded)
			{
				var syncToPrint = new CheckBox("Sync To Print".Localize(), textColor: textColor);
				syncToPrint.Checked = (UserSettings.Instance.get("LayerViewSyncToPrint") == "True");
				syncToPrint.Name = "Sync To Print Checkbox";
				syncToPrint.CheckedStateChanged += (s, e) =>
				{
					options.SyncToPrint = syncToPrint.Checked;
					
					SetSyncToPrintVisibility();
				};
				popupContainer.AddChild(syncToPrint);

				// The idea here is we just got asked to rebuild the window (and it is being created now)
				// because the gcode finished creating for the print that is printing.
				// We don't want to be notified if any other updates happen to this gcode while it is printing.
				if (PrinterConnection.Instance.PrinterIsPrinting
					&& ApplicationController.Instance.ActivePrintItem == printItem)
				{
					printItem.SlicingOutputMessage -= sliceItem_SlicingOutputMessage;
					printItem.SlicingDone -= sliceItem_Done;
				}
			}

			return popupContainer;
		}

		private void SetSyncToPrintVisibility()
		{
			if (windowMode == WindowMode.Embeded)
			{
				bool printerIsRunningPrint = PrinterConnection.Instance.PrinterIsPaused || PrinterConnection.Instance.PrinterIsPrinting;

				if (options.SyncToPrint && printerIsRunningPrint)
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
					navigationWidget.Visible = true;
					setLayerWidget.Visible = true;
					layerRenderRatioSlider.Visible = true;
					selectLayerSlider.Visible = true;
				}
			}
		}

		private void SwitchViewModes()
		{
			bool inLayers3DMode = activeViewMode == PartViewMode.Layers3D;
			if (inLayers3DMode)
			{
				UserSettings.Instance.set("LayerViewDefault", "3D Layer");
			}
			else
			{
				UserSettings.Instance.set("LayerViewDefault", "2D Layer");

				// HACK: Getting the Layer2D view to show content only works if CenterPartInView is called after the control is visible and after some cycles have passed
				UiThread.RunOnIdle(gcode2DWidget.CenterPartInView);
			}

			gcode2DWidget.Visible = !inLayers3DMode;
		}

		private void HookUpGCodeMessagesWhenDonePrinting(object sender, EventArgs e)
		{
			if (!PrinterConnection.Instance.PrinterIsPaused && !PrinterConnection.Instance.PrinterIsPrinting)
			{
				// unregister first to make sure we don't double up in error (should not be needed but no harm)
				printItem.SlicingOutputMessage -= sliceItem_SlicingOutputMessage;
				printItem.SlicingDone -= sliceItem_Done;

				// register for done slicing and slicing messages
				printItem.SlicingOutputMessage += sliceItem_SlicingOutputMessage;
				printItem.SlicingDone += sliceItem_Done;
			}
			SetSyncToPrintVisibility();
		}

		private void LoadProgress_Changed((double progress0To1, string processingState) progress, CancellationTokenSource continueProcessing)
		{
			SetProcessingMessage(string.Format("{0} {1:0}%...", gcodeLoading, progress.progress0To1 * 100));
			if(this.HasBeenClosed)
			{
				continueProcessing.Cancel();
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			bool printerIsRunningPrint = PrinterConnection.Instance.PrinterIsPaused || PrinterConnection.Instance.PrinterIsPrinting;
			if (options.SyncToPrint
				&& printerIsRunningPrint)
			{
				SetAnimationPosition();
			}

			base.OnDraw(graphics2D);
		}

		private void SetProcessingMessage(string message)
		{
			if (gcodeProcessingStateInfoText == null)
			{
				gcodeProcessingStateInfoText = new TextWidget(message)
				{
					HAnchor = HAnchor.ParentCenter,
					VAnchor = VAnchor.ParentCenter,
					AutoExpandBoundsToText = true
				};

				var labelContainer = new GuiWidget();
				labelContainer.Selectable = false;
				labelContainer.AnchorAll();
				labelContainer.AddChild(gcodeProcessingStateInfoText);

				gcodeDisplayWidget.AddChild(labelContainer);
			}

			if (message == "")
			{
				gcodeProcessingStateInfoText.BackgroundColor = RGBA_Bytes.Transparent;
			}
			else
			{
				gcodeProcessingStateInfoText.BackgroundColor = RGBA_Bytes.White;
			}

			gcodeProcessingStateInfoText.Text = message;
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			SetSliderSizes();
			base.OnBoundsChanged(e);
		}

		private void SetSliderSizes()
		{
			if (selectLayerSlider == null)
			{
				return;
			}

			selectLayerSlider.OriginRelativeParent = new Vector2(gcodeDisplayWidget.Width - 20, 70);
			selectLayerSlider.TotalWidthInPixels = gcodeDisplayWidget.Height - 80;

			layerRenderRatioSlider.OriginRelativeParent = new Vector2(60, 70);
			layerRenderRatioSlider.TotalWidthInPixels = gcodeDisplayWidget.Width - 100;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);

			// Find and unhook the parent system window KeyDown event
			if (parentSystemWindow != null)
			{
				parentSystemWindow.KeyDown -= Parent_KeyDown;
			}

			if (externalMeshViewer != null)
			{
				externalMeshViewer.TrackballTumbleWidget.DrawGlContent -= TrackballTumbleWidget_DrawGlContent;
			}

			if (printItem != null)
			{
				printItem.SlicingOutputMessage -= sliceItem_SlicingOutputMessage;
				printItem.SlicingDone -= sliceItem_Done;
				if (startedSliceFromGenerateButton && printItem.CurrentlySlicing)
				{
					SlicingQueue.Instance.CancelCurrentSlicing();
				}
			}

			base.OnClosed(e);
		}

		private void sliceItem_SlicingOutputMessage(object sender, EventArgs e)
		{
			if (e is StringEventArgs message && message.Data != null)
			{
				SetProcessingMessage(message.Data);
			}
			else
			{
				SetProcessingMessage("");
			}
		}

		private void sliceItem_Done(object sender, EventArgs e)
		{
			// We can add this while we have it open (when we are done loading).
			// So we need to make sure we only have it added once. This will be ok to run when
			// not added or when added and will ensure we only have one hook.
			printItem.SlicingOutputMessage -= sliceItem_SlicingOutputMessage;
			printItem.SlicingDone -= sliceItem_Done;

			UiThread.RunOnIdle(CreateAndAddChildren);
			startedSliceFromGenerateButton = false;
		}
	}
}
