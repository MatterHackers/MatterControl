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
	public class ViewGcodeBasic : PartPreview3DWidget
	{
		public enum WindowMode { Embeded, StandAlone };

		public SolidSlider selectLayerSlider;

		private SetLayerWidget setLayerWidget;
		private LayerNavigationWidget navigationWidget;
		public DoubleSolidSlider layerRenderRatioSlider;

		private TextWidget gcodeProcessingStateInfoText;
		private ViewGcodeWidget gcodeViewWidget;
		private PrintItemWrapper printItem => ApplicationController.Instance.ActivePrintItem;
		private bool startedSliceFromGenerateButton = false;
		private Button generateGCodeButton;
		private FlowLayoutWidget buttonBottomPanel;
		private FlowLayoutWidget layerSelectionButtonsPanel;

		private ViewControlsToggle viewControlsToggle;

		private GuiWidget gcodeDisplayWidget;

		private ColorGradientWidget gradientWidget;

		private EventHandler unregisterEvents;
		private WindowMode windowMode;

		private string partToStartLoadingOnFirstDraw = null;
		private string gcodeLoading = "Loading G-Code".Localize();

		public delegate Vector2 GetSizeFunction();

		private string slicingErrorMessage = "Slicing Error.\nPlease review your slice settings.".Localize();
		private string pressGenerateMessage = "Press 'generate' to view layers".Localize();
		private string fileNotFoundMessage = "File not found on disk.".Localize();
		private string fileTooBigToLoad = "GCode file too big to preview ({0}).".Localize();

		private Vector2 bedCenter;
		private Vector3 viewerVolume;
		private BedShape bedShape;
		private int sliderWidth;

		private PartViewMode activeViewMode = PartViewMode.Layers3D;


		private ApplicationController.View3DConfig options;

		public ViewGcodeBasic(Vector3 viewerVolume, Vector2 bedCenter, BedShape bedShape, WindowMode windowMode, ViewControls3D viewControls3D, ThemeConfig theme)
			: base(viewControls3D)
		{
			this.options = ApplicationController.Instance.Options.View3D;

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

			CreateAndAddChildren();

			ActiveSliceSettings.SettingChanged.RegisterEvent(CheckSettingChanged, ref unregisterEvents);
			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => gcodeViewWidget?.Clear3DGCode(), ref unregisterEvents);
		}

		private void CheckSettingChanged(object sender, EventArgs e)
		{
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null)
			{
				if (gcodeViewWidget?.LoadedGCode != null
					&& (
					stringEvent.Data == SettingsKey.filament_cost
					|| stringEvent.Data == SettingsKey.filament_diameter
					|| stringEvent.Data == SettingsKey.filament_density)
					)
				{
					UpdateMassText();
					UpdateEstimatedCost();
				}

				if (stringEvent.Data == SettingsKey.bed_size
					|| stringEvent.Data == SettingsKey.print_center
					|| stringEvent.Data == SettingsKey.build_height
					|| stringEvent.Data == SettingsKey.bed_shape)
				{
					viewerVolume = new Vector3(ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size), ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.build_height));
					bedShape = ActiveSliceSettings.Instance.GetValue<BedShape>(SettingsKey.bed_shape);
					bedCenter = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);

					double buildHeight = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.build_height);

					UiThread.RunOnIdle(() =>
					{
						meshViewerWidget.CreatePrintBed(
							viewerVolume,
							bedCenter,
							bedShape);
					});
				}
				else if (stringEvent.Data == "extruder_offset")
				{
					gcodeViewWidget.Clear3DGCode();
				}
			}
		}

		private void CreateAndAddChildren()
		{
			CloseAllChildren();

			var buttonFactory = ApplicationController.Instance.Theme.BreadCrumbButtonFactory;
			if (meshViewerWidget != null)
			{
				meshViewerWidget.Closed -= MeshViewerWidget_Closed;
				meshViewerWidget.TrackballTumbleWidget.DrawGlContent -= TrackballTumbleWidget_DrawGlContent;
			}

			meshViewerWidget = null;
			gcodeViewWidget = null;
			gcodeProcessingStateInfoText = null;

			FlowLayoutWidget mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainerTopToBottom.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			mainContainerTopToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;

			buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonBottomPanel.HAnchor = HAnchor.ParentLeftRight;
			buttonBottomPanel.Padding = new BorderDouble(3, 3);
			buttonBottomPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			generateGCodeButton = buttonFactory.Generate("Generate".Localize());
			generateGCodeButton.Name = "Generate Gcode Button";
			generateGCodeButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					if (ActiveSliceSettings.Instance.PrinterSelected)
					{
						// Save any pending changes before starting the print
						ApplicationController.Instance.ActiveView3DWidget.PersistPlateIfNeeded().ContinueWith((t) =>
						{
							if (ActiveSliceSettings.Instance.IsValid() && printItem != null)
							{
								generateGCodeButton.Visible = false;
								SlicingQueue.Instance.QueuePartForSlicing(printItem);
								startedSliceFromGenerateButton = true;
							}
						});
					}
					else
					{
						StyledMessageBox.ShowMessageBox(null, "Oops! Please select a printer in order to continue slicing.", "Select Printer", StyledMessageBox.MessageType.OK);
					}
				});
			};

			buttonBottomPanel.AddChild(generateGCodeButton);

			layerSelectionButtonsPanel = new FlowLayoutWidget(FlowDirection.RightToLeft);
			layerSelectionButtonsPanel.HAnchor = HAnchor.ParentLeftRight;
			layerSelectionButtonsPanel.Padding = new BorderDouble(0);

			GuiWidget holdPanelOpen = new GuiWidget(1, generateGCodeButton.Height);
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
			string firstProcessingMessage = "Press 'Add' to select an item.".Localize();

			if (printItem != null)
			{
				firstProcessingMessage = "Loading G-Code...".Localize();
				if (Path.GetExtension(printItem.FileLocation).ToUpper() == ".GCODE")
				{
					gcodeDisplayWidget.AddChild(CreateGCodeViewWidget(printItem.FileLocation));
				}
				else
				{
					if (File.Exists(printItem.FileLocation))
					{
						string gcodePathAndFileName = printItem.GetGCodePathAndFileName();
						bool gcodeFileIsComplete = printItem.IsGCodeFileComplete(gcodePathAndFileName);

						if (printItem.SlicingHadError)
						{
							firstProcessingMessage = slicingErrorMessage;
						}
						else
						{
							firstProcessingMessage = pressGenerateMessage;
						}

						if (File.Exists(gcodePathAndFileName) && gcodeFileIsComplete)
						{
							gcodeDisplayWidget.AddChild(CreateGCodeViewWidget(gcodePathAndFileName));
						}

						// we only hook these up to make sure we can regenerate the gcode when we want
						printItem.SlicingOutputMessage += sliceItem_SlicingOutputMessage;
						printItem.SlicingDone += sliceItem_Done;
					}
					else
					{
						firstProcessingMessage = string.Format("{0}\n'{1}'", fileNotFoundMessage, printItem.Name);
					}
				}
			}
			else
			{
				generateGCodeButton.Visible = false;
			}

			SetProcessingMessage(firstProcessingMessage);

			mainContainerTopToBottom.AddChild(gcodeDisplayWidget);

			// add in a spacer
			layerSelectionButtonsPanel.AddChild(new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight
			});
			buttonBottomPanel.AddChild(layerSelectionButtonsPanel);

			mainContainerTopToBottom.AddChild(buttonBottomPanel);
			this.AddChild(mainContainerTopToBottom);

			meshViewerWidget = new MeshViewerWidget(viewerVolume, bedCenter, bedShape, "")
			{
				Visible = (activeViewMode == PartViewMode.Layers3D),
				AllowBedRenderingWhenEmpty = true
			};
			meshViewerWidget.AnchorAll();
			gcodeDisplayWidget.AddChild(meshViewerWidget);
			meshViewerWidget.TrackballTumbleWidget.DrawGlContent += TrackballTumbleWidget_DrawGlContent;
			meshViewerWidget.Closed += MeshViewerWidget_Closed;

			// Apply active world view if initialized
			if (ApplicationController.Instance.PartPreviewState.RotationMatrix != Matrix4X4.Identity)
			{
				meshViewerWidget.World.RotationMatrix = ApplicationController.Instance.PartPreviewState.RotationMatrix;
				meshViewerWidget.World.TranslationMatrix = ApplicationController.Instance.PartPreviewState.TranslationMatrix;
			}

			viewControls3D.ResetView += (sender, e) =>
			{
				if (gcodeDisplayWidget.Visible)
				{
					gcodeViewWidget.CenterPartInView();
				}
				else
				{
					meshViewerWidget.ResetView();
				}
			};

			viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;

			viewControlsToggle = new ViewControlsToggle(ApplicationController.Instance.Theme.ViewControlsButtonFactory, activeViewMode)
			{
				Visible = false,
				HAnchor = HAnchor.ParentRight
			};
			AddChild(viewControlsToggle);

			viewControls3D.TransformStateChanged += (s, e) =>
			{
				switch (e.TransformMode)
				{
					case ViewControls3DButtons.Translate:
						if (gcodeViewWidget != null)
						{
							gcodeViewWidget.TransformState = ViewGcodeWidget.ETransformState.Move;
						}
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Translation;
						break;
					case ViewControls3DButtons.Scale:
						if (gcodeViewWidget != null)
						{
							gcodeViewWidget.TransformState = ViewGcodeWidget.ETransformState.Scale;
						}
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Scale;
						break;
					case ViewControls3DButtons.Rotate:
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
						break;

				}
			};
		}

		private void MeshViewerWidget_Closed(object sender, ClosedEventArgs e)
		{
			if (meshViewerWidget.Visible)
			{
				ApplicationController.Instance.PartPreviewState.RotationMatrix = meshViewerWidget.World.RotationMatrix;
				ApplicationController.Instance.PartPreviewState.TranslationMatrix = meshViewerWidget.World.TranslationMatrix;
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
			if (gcodeViewWidget?.LoadedGCode == null)
			{
				return;
			}

			GCodeRenderer.ExtrusionColor = ActiveTheme.Instance.PrimaryAccentColor;

			GCodeRenderInfo renderInfo = new GCodeRenderInfo(0,
				Math.Min(gcodeViewWidget.ActiveLayerIndex + 1, gcodeViewWidget.LoadedGCode.NumChangesInZ),
				gcodeViewWidget.TotalTransform,
				1,
				GetRenderType(),
				gcodeViewWidget.FeatureToStartOnRatio0To1,
				gcodeViewWidget.FeatureToEndOnRatio0To1,
				new Vector2[] { ActiveSliceSettings.Instance.Helpers.ExtruderOffset(0), ActiveSliceSettings.Instance.Helpers.ExtruderOffset(1) },
				MeshViewerWidget.GetMaterialColor);

			gcodeViewWidget.gCodeRenderer.Render3D(renderInfo);
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

		private FlowLayoutWidget CreateModelInfo()
		{
			var modelInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = new BorderDouble(5),
				Margin = new BorderDouble(0, 0, 35, 5),
				BackgroundColor = new RGBA_Bytes(0, 0, 0, ViewControlsBase.overlayAlpha),
				HAnchor = HAnchor.ParentRight | HAnchor.AbsolutePosition,
				VAnchor = VAnchor.ParentTop | VAnchor.FitToChildren,
				Width = 150
			};

			// put in the print time
			modelInfoContainer.AddChild(new TextWidget("Print Time".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
			{
				string timeRemainingText = "---";

				if (gcodeViewWidget != null && gcodeViewWidget.LoadedGCode != null)
				{
					int secondsRemaining = (int)gcodeViewWidget.LoadedGCode.Instruction(0).secondsToEndFromHere;
					int hoursRemaining = (int)(secondsRemaining / (60 * 60));
					int minutesRemaining = (int)((secondsRemaining + 30) / 60 - hoursRemaining * 60); // +30 for rounding
					secondsRemaining = secondsRemaining % 60;
					if (hoursRemaining > 0)
					{
						timeRemainingText = $"{hoursRemaining} h, {minutesRemaining} min";
					}
					else
					{
						timeRemainingText = $"{minutesRemaining} min";
					}
				}

				GuiWidget estimatedPrintTime = new TextWidget(timeRemainingText, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 14);
				estimatedPrintTime.Margin = new BorderDouble(0, 9, 0, 3);
				modelInfoContainer.AddChild(estimatedPrintTime);
			}

			// show the filament used
			modelInfoContainer.AddChild(new TextWidget("Filament Length".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
			{
				double filamentUsed = gcodeViewWidget.LoadedGCode.GetFilamentUsedMm(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter));

				GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.0} mm", filamentUsed), pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor);
				estimatedPrintTime.Margin = new BorderDouble(0, 9, 0, 3);
				modelInfoContainer.AddChild(estimatedPrintTime);
			}

			modelInfoContainer.AddChild(new TextWidget("Filament Volume".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
			{
				double filamentMm3 = gcodeViewWidget.LoadedGCode.GetFilamentCubicMm(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter));

				GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.00} cm³", filamentMm3 / 1000), pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor);
				estimatedPrintTime.Margin = new BorderDouble(0, 9, 0, 3);
				modelInfoContainer.AddChild(estimatedPrintTime);
			}

			modelInfoContainer.AddChild(GetEstimatedMassInfo());
			modelInfoContainer.AddChild(GetEstimatedCostInfo());

			// TODO: Every time you click Generate we wire up a listener - only when we close do they get released. This is a terrible pattern that has a good chance of creating a high leak scenario. Since RootedEventHandlers are normally only cleared when a widget is closed, we should **only** register them in widget constructors
			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(HookUpGCodeMessagesWhenDonePrinting, ref unregisterEvents);

			return modelInfoContainer;
		}

		double totalMass
		{
			get
			{
				double filamentDiameter = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter);
				double filamentDensity = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_density);

				return gcodeViewWidget.LoadedGCode.GetFilamentWeightGrams(filamentDiameter, filamentDensity);
			}
		}

		double totalCost
		{
			get
			{
				double filamentCost = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_cost);
				return totalMass / 1000 * filamentCost;
			}
		}

		TextWidget massTextWidget;

		void UpdateMassText()
		{
			if (totalMass != 0)
			{
				massTextWidget.Text = string.Format("{0:0.00} g", totalMass);
			}
			else
			{
				massTextWidget.Text = "Unknown";
			}
		}

		private GuiWidget GetEstimatedMassInfo()
		{
			FlowLayoutWidget estimatedMassInfo = new FlowLayoutWidget(FlowDirection.TopToBottom);
			estimatedMassInfo.AddChild(new TextWidget("Estimated Mass".Localize() + ":", pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor));
			massTextWidget = new TextWidget("", pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
			};
			massTextWidget.Margin = new BorderDouble(0, 9, 0, 3);
			estimatedMassInfo.AddChild(massTextWidget);

			UpdateMassText();

			return estimatedMassInfo;
		}

		FlowLayoutWidget estimatedCostInfo;
		TextWidget costTextWidget;

		void UpdateEstimatedCost()
		{
			costTextWidget.Text = string.Format("${0:0.00}", totalCost);
			if (totalCost == 0)
			{
				estimatedCostInfo.Visible = false;
			}
			else
			{
				estimatedCostInfo.Visible = true;
			}
		}

		private GuiWidget GetEstimatedCostInfo()
		{
			estimatedCostInfo = new FlowLayoutWidget(FlowDirection.TopToBottom);
			string costLabel = "Estimated Cost".Localize();
			string costLabelFull = string.Format("{0}:", costLabel);
			estimatedCostInfo.AddChild(new TextWidget(costLabelFull, pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor));
			costTextWidget = new TextWidget("", pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
			};
			costTextWidget.Margin = new BorderDouble(0, 9, 0, 3);
			estimatedCostInfo.AddChild(costTextWidget);

			UpdateEstimatedCost();

			return estimatedCostInfo;
		}

		internal GuiWidget ShowOverflowMenu()
		{
			var popupContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Padding = 12,
				BackgroundColor = RGBA_Bytes.White
			};

			// put in a show grid check box
			CheckBox showGrid = new CheckBox("Print Bed".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			showGrid.Checked = options.RenderGrid;
			meshViewerWidget.RenderBed = showGrid.Checked;
			showGrid.CheckedStateChanged += (sender, e) =>
			{
				options.RenderGrid = showGrid.Checked;
			};
			popupContainer.AddChild(showGrid);

			// put in a show moves checkbox
			var showMoves = new CheckBox("Moves".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			showMoves.Checked = options.RenderMoves;
			showMoves.CheckedStateChanged += (sender, e) =>
			{
				options.RenderMoves = showMoves.Checked;
			};
			popupContainer.AddChild(showMoves);

			// put in a show Retractions checkbox
			CheckBox showRetractions = new CheckBox("Retractions".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			showRetractions.Checked = options.RenderRetractions;
			showRetractions.CheckedStateChanged += (sender, e) =>
			{
				options.RenderRetractions = showRetractions.Checked;
			};
			popupContainer.AddChild(showRetractions);
			

			// put in a show speed checkbox
			var showSpeeds = new CheckBox("Speeds".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			showSpeeds.Checked = options.RenderSpeeds;
			//showSpeeds.Checked = gradient.Visible;
			showSpeeds.CheckedStateChanged += (sender, e) =>
			{
				gradientWidget.Visible = showSpeeds.Checked;
				options.RenderSpeeds = showSpeeds.Checked;
			};

			popupContainer.AddChild(showSpeeds);

			// put in a simulate extrusion checkbox
			var simulateExtrusion = new CheckBox("Extrusion".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			simulateExtrusion.Checked = options.SimulateExtrusion;
			simulateExtrusion.CheckedStateChanged += (sender, e) =>
			{
				options.SimulateExtrusion = simulateExtrusion.Checked;
			};
			popupContainer.AddChild(simulateExtrusion);

			// put in a render extrusion transparent checkbox
			var transparentExtrusion = new CheckBox("Transparent".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor)
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

			// put in a simulate extrusion checkbox
			if (ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count) > 1)
			{
				CheckBox hideExtruderOffsets = new CheckBox("Hide Offsets", textColor: ActiveTheme.Instance.PrimaryTextColor);
				hideExtruderOffsets.Checked = options.HideExtruderOffsets;
				hideExtruderOffsets.CheckedStateChanged += (sender, e) =>
				{
					options.HideExtruderOffsets = hideExtruderOffsets.Checked;
				};
				popupContainer.AddChild(hideExtruderOffsets);
			}

			// Respond to user driven view mode change events and store and switch to the new mode
			viewControlsToggle.ViewModeChanged += (s, e) =>
			{
				activeViewMode = e.ViewMode;
				SwitchViewModes();
			};

			// Switch to the most recent view mode, defaulting to Layers3D
			SwitchViewModes();

			// Put in the sync to print checkbox
			if (windowMode == WindowMode.Embeded)
			{
				var syncToPrint = new CheckBox("Sync To Print".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
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

					generateGCodeButton.Visible = false;

					// TODO: Bad pattern - figure out how to revise
					// However if the print finished or is canceled we are going to want to get updates again. So, hook the status event
					PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(HookUpGCodeMessagesWhenDonePrinting, ref unregisterEvents);
					UiThread.RunOnIdle(SetSyncToPrintVisibility);
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
				UiThread.RunOnIdle(gcodeViewWidget.CenterPartInView);
			}

			meshViewerWidget.Visible = inLayers3DMode;
			gcodeViewWidget.Visible = !inLayers3DMode;
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

				generateGCodeButton.Visible = true;
			}
			SetSyncToPrintVisibility();
		}

		private void LoadProgress_Changed(double progress0To1, string processingState, out bool continueProcessing)
		{
			SetProcessingMessage(string.Format("{0} {1:0}%...", gcodeLoading, progress0To1 * 100));
			continueProcessing = !this.HasBeenClosed;
		}

		private GuiWidget CreateGCodeViewWidget(string pathAndFileName)
		{
			gcodeViewWidget = new ViewGcodeWidget(new Vector2(viewerVolume.x, viewerVolume.y), bedCenter, LoadProgress_Changed);
			gcodeViewWidget.DoneLoading += DoneLoadingGCode;
			gcodeViewWidget.Visible = (activeViewMode == PartViewMode.Layers2D);
			partToStartLoadingOnFirstDraw = pathAndFileName;

			return gcodeViewWidget;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			bool printerIsRunningPrint = PrinterConnection.Instance.PrinterIsPaused || PrinterConnection.Instance.PrinterIsPrinting;
			if (options.SyncToPrint
				&& printerIsRunningPrint)
			{
				SetAnimationPosition();
			}

			if (partToStartLoadingOnFirstDraw != null)
			{
				gcodeViewWidget.LoadInBackground(partToStartLoadingOnFirstDraw);
				partToStartLoadingOnFirstDraw = null;
			}
			base.OnDraw(graphics2D);
		}

		private void Parent_KeyDown(object sender, KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Up)
			{
				if (gcodeViewWidget != null)
				{
					gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex + 1);
				}
			}
			else if (keyEvent.KeyCode == Keys.Down)
			{
				if (gcodeViewWidget != null)
				{
					gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex - 1);
				}
			}
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

		private void DoneLoadingGCode(object sender, EventArgs e)
		{
			SetProcessingMessage("");
			if (gcodeViewWidget != null
				&& gcodeViewWidget.LoadedGCode == null)
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

			if (gcodeViewWidget != null
				&& gcodeViewWidget.LoadedGCode != null
				&& gcodeViewWidget.LoadedGCode.LineCount > 0)
			{
				// TODO: Shouldn't we be clearing children from some known container and rebuilding?
				gradientWidget?.Close();
				gradientWidget = new ColorGradientWidget(gcodeViewWidget.LoadedGCode);
				AddChild(gradientWidget);
				gradientWidget.Visible = false;

				gradientWidget.Visible = options.RenderSpeeds;

				viewControlsToggle.Visible = true;

				setLayerWidget?.Close();
				setLayerWidget = new SetLayerWidget(gcodeViewWidget, ApplicationController.Instance.Theme.GCodeLayerButtons);
				setLayerWidget.VAnchor = Agg.UI.VAnchor.ParentTop;
				layerSelectionButtonsPanel.AddChild(setLayerWidget);

				
				navigationWidget?.Close();
				navigationWidget = new LayerNavigationWidget(gcodeViewWidget, ApplicationController.Instance.Theme.GCodeLayerButtons);
				navigationWidget.Margin = new BorderDouble(0, 0, 20, 0);
				layerSelectionButtonsPanel.AddChild(navigationWidget);

				selectLayerSlider?.Close();
				selectLayerSlider = new SolidSlider(new Vector2(), sliderWidth, 0, gcodeViewWidget.LoadedGCode.NumChangesInZ - 1, Orientation.Vertical);
				selectLayerSlider.ValueChanged += new EventHandler(selectLayerSlider_ValueChanged);
				gcodeViewWidget.ActiveLayerChanged += new EventHandler(gcodeViewWidget_ActiveLayerChanged);
				AddChild(selectLayerSlider);

				layerRenderRatioSlider?.Close();
				layerRenderRatioSlider = new DoubleSolidSlider(new Vector2(), sliderWidth);
				layerRenderRatioSlider.FirstValue = 0;
				layerRenderRatioSlider.FirstValueChanged += new EventHandler(layerStartRenderRatioSlider_ValueChanged);
				layerRenderRatioSlider.SecondValue = 1;
				layerRenderRatioSlider.SecondValueChanged += new EventHandler(layerEndRenderRatioSlider_ValueChanged);
				AddChild(layerRenderRatioSlider);

				SetSliderSizes();

				// let's change the active layer so that it is set to the first layer with data
				gcodeViewWidget.ActiveLayerIndex = gcodeViewWidget.ActiveLayerIndex + 1;
				gcodeViewWidget.ActiveLayerIndex = gcodeViewWidget.ActiveLayerIndex - 1;

				BoundsChanged += new EventHandler(PartPreviewGCode_BoundsChanged);

				this.AddChild(CreateModelInfo());

				meshViewerWidget.partProcessingInfo.Visible = false;
			}
		}

		private void layerStartRenderRatioSlider_ValueChanged(object sender, EventArgs e)
		{
			gcodeViewWidget.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
			gcodeViewWidget.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;
			gcodeViewWidget.Invalidate();
		}

		private void layerEndRenderRatioSlider_ValueChanged(object sender, EventArgs e)
		{
			gcodeViewWidget.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
			gcodeViewWidget.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;
			gcodeViewWidget.Invalidate();
		}

		private void gcodeViewWidget_ActiveLayerChanged(object sender, EventArgs e)
		{
			if (gcodeViewWidget.ActiveLayerIndex != (int)(selectLayerSlider.Value + .5))
			{
				selectLayerSlider.Value = gcodeViewWidget.ActiveLayerIndex;
			}
		}

		private void selectLayerSlider_ValueChanged(object sender, EventArgs e)
		{
			gcodeViewWidget.ActiveLayerIndex = (int)(selectLayerSlider.Value + .5);
		}

		private void PartPreviewGCode_BoundsChanged(object sender, EventArgs e)
		{
			SetSliderSizes();
		}

		private void SetSliderSizes()
		{
			selectLayerSlider.OriginRelativeParent = new Vector2(gcodeDisplayWidget.Width - 20, 70);
			selectLayerSlider.TotalWidthInPixels = gcodeDisplayWidget.Height - 80;

			layerRenderRatioSlider.OriginRelativeParent = new Vector2(60, 70);
			layerRenderRatioSlider.TotalWidthInPixels = gcodeDisplayWidget.Width - 100;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);

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
			StringEventArgs message = e as StringEventArgs;
			if (message != null && message.Data != null)
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
