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
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using System.ComponentModel;
using System.IO;

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
		private PrintItemWrapper printItem;
		private bool startedSliceFromGenerateButton = false;
		private Button generateGCodeButton;
		private FlowLayoutWidget buttonBottomPanel;
		private FlowLayoutWidget layerSelectionButtonsPanel;

		private FlowLayoutWidget modelOptionsContainer;
		private FlowLayoutWidget displayOptionsContainer;
		private ViewControlsToggle viewControlsToggle;

		private CheckBox expandModelOptions;
		private CheckBox expandDisplayOptions;
		private CheckBox syncToPrint;
		private CheckBox showSpeeds;

		private GuiWidget gcodeDisplayWidget;

		private ColorGradientWidget gradientWidget;

		private EventHandler unregisterEvents;
		private WindowMode windowMode;

		public delegate Vector2 GetSizeFunction();

		private static string slicingErrorMessage = "Slicing Error.\nPlease review your slice settings.".Localize();
		private static string pressGenerateMessage = "Press 'generate' to view layers".Localize();
		private static string fileNotFoundMessage = "File not found on disk.".Localize();
		private static string fileTooBigToLoad = "GCode file too big to preview ({0}).".Localize();

		private Vector2 bedCenter;
		private Vector3 viewerVolume;
		private BedShape bedShape;
		private int sliderWidth;

		public ViewGcodeBasic(PrintItemWrapper printItem, Vector3 viewerVolume, Vector2 bedCenter, BedShape bedShape, WindowMode windowMode)
		{
			this.viewerVolume = viewerVolume;
			this.bedShape = bedShape;
			this.bedCenter = bedCenter;
			this.windowMode = windowMode;
			this.printItem = printItem;

			if (UserSettings.Instance.DisplayMode == ApplicationDisplayType.Touchscreen)
			{
				sliderWidth = 20;
			}
			else
			{
				sliderWidth = 10;
			}

			CreateAndAddChildren();

			ActiveSliceSettings.SettingChanged.RegisterEvent(CheckSettingChanged, ref unregisterEvents);
			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => ClearGCode(), ref unregisterEvents);

			ActiveSliceSettings.ActivePrinterChanged.RegisterEvent(CheckSettingChanged, ref unregisterEvents);
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
					|| stringEvent.Data == SettingsKey.bed_shape
					|| stringEvent.Data == SettingsKey.center_part_on_bed)
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
				else if(stringEvent.Data == "extruder_offset")
				{
					ClearGCode();
				}
			}
		}

		private void ClearGCode()
		{
			if (gcodeViewWidget != null
				&& gcodeViewWidget.gCodeRenderer != null)
			{
				gcodeViewWidget.gCodeRenderer.Clear3DGCode();
				gcodeViewWidget.Invalidate();
			}
		}

		private void CreateAndAddChildren()
		{
			TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			CloseAllChildren();
			gcodeViewWidget = null;
			gcodeProcessingStateInfoText = null;

			FlowLayoutWidget mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainerTopToBottom.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			mainContainerTopToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;

			buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonBottomPanel.HAnchor = HAnchor.ParentLeftRight;
			buttonBottomPanel.Padding = new BorderDouble(3, 3);
			buttonBottomPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			generateGCodeButton = textImageButtonFactory.Generate(LocalizedString.Get("Generate"));
			generateGCodeButton.Name = "Generate Gcode Button";
			generateGCodeButton.Click += new EventHandler(generateButton_Click);
			buttonBottomPanel.AddChild(generateGCodeButton);

			layerSelectionButtonsPanel = new FlowLayoutWidget(FlowDirection.RightToLeft);
			layerSelectionButtonsPanel.HAnchor = HAnchor.ParentLeftRight;
			layerSelectionButtonsPanel.Padding = new BorderDouble(0);

			GuiWidget holdPanelOpen = new GuiWidget(1, generateGCodeButton.Height);
			layerSelectionButtonsPanel.AddChild(holdPanelOpen);

			if (windowMode == WindowMode.StandAlone)
			{
				Button closeButton = textImageButtonFactory.Generate(LocalizedString.Get("Close"));
				layerSelectionButtonsPanel.AddChild(closeButton);
				closeButton.Click += (sender, e) =>
				{
					CloseOnIdle();
				};
			}

			FlowLayoutWidget centerPartPreviewAndControls = new FlowLayoutWidget(FlowDirection.LeftToRight);
			centerPartPreviewAndControls.AnchorAll();

			gcodeDisplayWidget = new GuiWidget(HAnchor.ParentLeftRight, Agg.UI.VAnchor.ParentBottomTop);
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
			centerPartPreviewAndControls.AddChild(gcodeDisplayWidget);

			buttonRightPanel = CreateRightButtonPanel();
			buttonRightPanel.Visible = false;
			centerPartPreviewAndControls.AddChild(buttonRightPanel);

			// add in a spacer
			layerSelectionButtonsPanel.AddChild(new GuiWidget(HAnchor.ParentLeftRight));
			buttonBottomPanel.AddChild(layerSelectionButtonsPanel);

			mainContainerTopToBottom.AddChild(centerPartPreviewAndControls);
			mainContainerTopToBottom.AddChild(buttonBottomPanel);
			this.AddChild(mainContainerTopToBottom);

			meshViewerWidget = new MeshViewerWidget(viewerVolume, bedCenter, bedShape, "".Localize());
			meshViewerWidget.AnchorAll();
			meshViewerWidget.AllowBedRenderingWhenEmpty = true;
			gcodeDisplayWidget.AddChild(meshViewerWidget);
			meshViewerWidget.Visible = false;
			meshViewerWidget.TrackballTumbleWidget.DrawGlContent += new EventHandler(TrackballTumbleWidget_DrawGlContent);

			viewControls2D = new ViewControls2D();
			AddChild(viewControls2D);

			viewControls2D.ResetView += (sender, e) =>
			{
				SetDefaultView2D();
			};

			viewControls3D = new ViewControls3D(meshViewerWidget);
			viewControls3D.PartSelectVisible = false;
			AddChild(viewControls3D);

			viewControls3D.ResetView += (sender, e) =>
			{
				meshViewerWidget.ResetView();
			};

			viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
			viewControls3D.Visible = false;

			viewControlsToggle = new ViewControlsToggle();
			viewControlsToggle.HAnchor = Agg.UI.HAnchor.ParentRight;
			AddChild(viewControlsToggle);
			viewControlsToggle.Visible = false;

			//viewControls3D.translateButton.ClickButton(null);

			meshViewerWidget.ResetView();

			viewControls2D.translateButton.Click += (sender, e) =>
			{
				gcodeViewWidget.TransformState = ViewGcodeWidget.ETransformState.Move;
			};
			viewControls2D.scaleButton.Click += (sender, e) =>
			{
				gcodeViewWidget.TransformState = ViewGcodeWidget.ETransformState.Scale;
			};

			AddHandlers();
		}

		private void SetDefaultView2D()
		{
			gcodeViewWidget.CenterPartInView();
		}

		private RenderType GetRenderType()
		{
			RenderType renderType = RenderType.Extrusions;
			if (gcodeViewWidget.RenderMoves)
			{
				renderType |= RenderType.Moves;
			}
			if (gcodeViewWidget.RenderRetractions)
			{
				renderType |= RenderType.Retractions;
			}
			if (gcodeViewWidget.RenderSpeeds)
			{
				renderType |= RenderType.SpeedColors;
			}
            if (gcodeViewWidget.SimulateExtrusion)
            {
                renderType |= RenderType.SimulateExtrusion;
            }
            if (gcodeViewWidget.TransparentExtrusion)
            {
                renderType |= RenderType.TransparentExtrusion;
            }
            if (gcodeViewWidget.HideExtruderOffsets)
			{
				renderType |= RenderType.HideExtruderOffsets;
			}

			return renderType;
		}

		private void TrackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			GCodeRenderer.ExtrusionColor = ActiveTheme.Instance.PrimaryAccentColor;

			GCodeRenderInfo renderInfo = new GCodeRenderInfo(0,
				Math.Min(gcodeViewWidget.ActiveLayerIndex + 1, gcodeViewWidget.LoadedGCode.NumChangesInZ),
				gcodeViewWidget.TotalTransform,
				1,
				GetRenderType(),
				gcodeViewWidget.FeatureToStartOnRatio0To1,
				gcodeViewWidget.FeatureToEndOnRatio0To1,
				new Vector2[] { ActiveSliceSettings.Instance.Helpers.ExtruderOffset(0), ActiveSliceSettings.Instance.Helpers.ExtruderOffset(1) });

			gcodeViewWidget.gCodeRenderer.Render3D(renderInfo);
		}

		private void SetAnimationPosition()
		{
			int currentLayer = PrinterConnectionAndCommunication.Instance.CurrentlyPrintingLayer;
			if (currentLayer <= 0)
			{
				selectLayerSlider.Value = 0;
				layerRenderRatioSlider.SecondValue = 0;
				layerRenderRatioSlider.FirstValue = 0;
			}
			else
			{
				selectLayerSlider.Value = currentLayer - 1;
				layerRenderRatioSlider.SecondValue = PrinterConnectionAndCommunication.Instance.RatioIntoCurrentLayer;
				layerRenderRatioSlider.FirstValue = 0;
			}
		}

		private FlowLayoutWidget CreateRightButtonPanel()
		{
			FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
			buttonRightPanel.Width = 200;
			{
				string label = "Model".Localize().ToUpper();
				expandModelOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton(label,
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
				expandModelOptions.Margin = new BorderDouble(bottom: 2);
				buttonRightPanel.AddChild(expandModelOptions);
				expandModelOptions.Checked = true;

				modelOptionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
				modelOptionsContainer.HAnchor = HAnchor.ParentLeftRight;
				//modelOptionsContainer.Visible = false;
				buttonRightPanel.AddChild(modelOptionsContainer);

				expandDisplayOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton("Display".Localize().ToUpper(),
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
				expandDisplayOptions.Name = "Display Checkbox";
				expandDisplayOptions.Margin = new BorderDouble(bottom: 2);
				buttonRightPanel.AddChild(expandDisplayOptions);
				expandDisplayOptions.Checked = false;

				displayOptionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
				displayOptionsContainer.HAnchor = HAnchor.ParentLeftRight;
				displayOptionsContainer.Padding = new BorderDouble(left: 6);
				displayOptionsContainer.Visible = false;
				buttonRightPanel.AddChild(displayOptionsContainer);

				GuiWidget verticalSpacer = new GuiWidget();
				verticalSpacer.VAnchor = VAnchor.ParentBottomTop;
				buttonRightPanel.AddChild(verticalSpacer);
			}

			buttonRightPanel.Padding = new BorderDouble(6, 6);
			buttonRightPanel.Margin = new BorderDouble(0, 1);
			buttonRightPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			buttonRightPanel.VAnchor = VAnchor.ParentBottomTop;

			return buttonRightPanel;
		}

		private void CreateOptionsContent()
		{
			AddModelInfo(modelOptionsContainer);
			AddDisplayControls(displayOptionsContainer);
		}

		private void AddModelInfo(FlowLayoutWidget buttonPanel)
		{
			buttonPanel.CloseAllChildren();

			double oldWidth = textImageButtonFactory.FixedWidth;
			textImageButtonFactory.FixedWidth = 44 * GuiWidget.DeviceScale;

			FlowLayoutWidget modelInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			modelInfoContainer.HAnchor = HAnchor.ParentLeftRight;
			modelInfoContainer.Padding = new BorderDouble(5);

			string printTimeLabel = "Print Time".Localize();
			string printTimeLabelFull = string.Format("{0}:", printTimeLabel);
			// put in the print time
			modelInfoContainer.AddChild(new TextWidget(printTimeLabelFull, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
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
						timeRemainingText = string.Format("{0} h, {1} min", hoursRemaining, minutesRemaining);
					}
					else
					{
						timeRemainingText = string.Format("{0} min", minutesRemaining);
					}
				}

				GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0}", timeRemainingText), textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 14);
				//estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
				estimatedPrintTime.Margin = new BorderDouble(0, 9, 0, 3);
				modelInfoContainer.AddChild(estimatedPrintTime);
			}

			//modelInfoContainer.AddChild(new TextWidget("Size:", textColor: ActiveTheme.Instance.PrimaryTextColor));

			string filamentLengthLabel = "Filament Length".Localize();
			string filamentLengthLabelFull = string.Format("{0}:", filamentLengthLabel);
			// show the filament used
			modelInfoContainer.AddChild(new TextWidget(filamentLengthLabelFull, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
			{
				double filamentUsed = gcodeViewWidget.LoadedGCode.GetFilamentUsedMm(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter));

				GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.0} mm", filamentUsed), pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor);
				//estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
				estimatedPrintTime.Margin = new BorderDouble(0, 9, 0, 3);
				modelInfoContainer.AddChild(estimatedPrintTime);
			}

			string filamentVolumeLabel = "Filament Volume".Localize();
			string filamentVolumeLabelFull = string.Format("{0}:", filamentVolumeLabel);
			modelInfoContainer.AddChild(new TextWidget(filamentVolumeLabelFull, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
			{
				double filamentMm3 = gcodeViewWidget.LoadedGCode.GetFilamentCubicMm(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter));

				GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.00} cm³", filamentMm3 / 1000), pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor);
				//estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
				estimatedPrintTime.Margin = new BorderDouble(0, 9, 0, 3);
				modelInfoContainer.AddChild(estimatedPrintTime);
			}

			modelInfoContainer.AddChild(GetEstimatedMassInfo());
			modelInfoContainer.AddChild(GetEstimatedCostInfo());

			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(HookUpGCodeMessagesWhenDonePrinting, ref unregisterEvents);

			buttonPanel.AddChild(modelInfoContainer);

			textImageButtonFactory.FixedWidth = oldWidth;
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
			string massLabel = "Estimated Mass".Localize();
			string massLabelFull = string.Format("{0}:", massLabel);
			estimatedMassInfo.AddChild(new TextWidget(massLabelFull, pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor));
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

		private void AddLayerInfo(FlowLayoutWidget buttonPanel)
		{
			double oldWidth = textImageButtonFactory.FixedWidth;
			textImageButtonFactory.FixedWidth = 44 * GuiWidget.DeviceScale;

			FlowLayoutWidget layerInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			layerInfoContainer.HAnchor = HAnchor.ParentLeftRight;
			layerInfoContainer.Padding = new BorderDouble(5);

			layerInfoContainer.AddChild(new TextWidget("Layer Number:", textColor: ActiveTheme.Instance.PrimaryTextColor));
			layerInfoContainer.AddChild(new TextWidget("Layer Height:", textColor: ActiveTheme.Instance.PrimaryTextColor));
			layerInfoContainer.AddChild(new TextWidget("Num GCodes:", textColor: ActiveTheme.Instance.PrimaryTextColor));
			layerInfoContainer.AddChild(new TextWidget("Filament Used:", textColor: ActiveTheme.Instance.PrimaryTextColor));
			layerInfoContainer.AddChild(new TextWidget("Weight:", textColor: ActiveTheme.Instance.PrimaryTextColor));
			layerInfoContainer.AddChild(new TextWidget("Print Time:", textColor: ActiveTheme.Instance.PrimaryTextColor));
			layerInfoContainer.AddChild(new TextWidget("Extrude Speeds:", textColor: ActiveTheme.Instance.PrimaryTextColor));
			layerInfoContainer.AddChild(new TextWidget("Move Speeds:", textColor: ActiveTheme.Instance.PrimaryTextColor));
			layerInfoContainer.AddChild(new TextWidget("Retract Speeds:", textColor: ActiveTheme.Instance.PrimaryTextColor));

			buttonPanel.AddChild(layerInfoContainer);

			textImageButtonFactory.FixedWidth = oldWidth;
		}

		private void AddDisplayControls(FlowLayoutWidget buttonPanel)
		{
			buttonPanel.CloseAllChildren();

			double oldWidth = textImageButtonFactory.FixedWidth;
			textImageButtonFactory.FixedWidth = 44 * GuiWidget.DeviceScale;

			FlowLayoutWidget layerInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			layerInfoContainer.HAnchor = HAnchor.ParentLeftRight;
			layerInfoContainer.Padding = new BorderDouble(5);

			// put in a show grid check box
			{
				CheckBox showGrid = new CheckBox(LocalizedString.Get("Print Bed"), textColor: ActiveTheme.Instance.PrimaryTextColor);
				showGrid.Checked = gcodeViewWidget.RenderGrid;
				meshViewerWidget.RenderBed = showGrid.Checked;
				showGrid.CheckedStateChanged += (sender, e) =>
				{
					gcodeViewWidget.RenderGrid = showGrid.Checked;
					meshViewerWidget.RenderBed = showGrid.Checked;
				};
				layerInfoContainer.AddChild(showGrid);
			}

			// put in a show moves checkbox
			{
				CheckBox showMoves = new CheckBox(LocalizedString.Get("Moves"), textColor: ActiveTheme.Instance.PrimaryTextColor);
				showMoves.Checked = gcodeViewWidget.RenderMoves;
				showMoves.CheckedStateChanged += (sender, e) =>
				{
					gcodeViewWidget.RenderMoves = showMoves.Checked;
				};
				layerInfoContainer.AddChild(showMoves);
			}

			// put in a show Retractions checkbox
			{
				CheckBox showRetractions = new CheckBox(LocalizedString.Get("Retractions"), textColor: ActiveTheme.Instance.PrimaryTextColor);
				showRetractions.Checked = gcodeViewWidget.RenderRetractions;
				showRetractions.CheckedStateChanged += (sender, e) =>
				{
					gcodeViewWidget.RenderRetractions = showRetractions.Checked;
				};
				layerInfoContainer.AddChild(showRetractions);
			}

			// put in a show speed checkbox
			{
				showSpeeds = new CheckBox(LocalizedString.Get("Speeds"), textColor: ActiveTheme.Instance.PrimaryTextColor);
				showSpeeds.Checked = gcodeViewWidget.RenderSpeeds;
				//showSpeeds.Checked = gradient.Visible;
				showSpeeds.CheckedStateChanged += (sender, e) =>
				{
					/* if (!showSpeeds.Checked)
					 {
						 gradient.Visible = false;
					 }
					 else
					 {
						 gradient.Visible = true;
					 }*/

					gradientWidget.Visible = showSpeeds.Checked;

					gcodeViewWidget.RenderSpeeds = showSpeeds.Checked;
				};

				layerInfoContainer.AddChild(showSpeeds);
			}

			// put in a simulate extrusion checkbox
			{
				CheckBox simulateExtrusion = new CheckBox(LocalizedString.Get("Extrusion"), textColor: ActiveTheme.Instance.PrimaryTextColor);
				simulateExtrusion.Checked = gcodeViewWidget.SimulateExtrusion;
				simulateExtrusion.CheckedStateChanged += (sender, e) =>
				{
					gcodeViewWidget.SimulateExtrusion = simulateExtrusion.Checked;
				};
				layerInfoContainer.AddChild(simulateExtrusion);
			}

            // put in a render extrusion transparent checkbox
            {
                CheckBox transparentExtrusion = new CheckBox(LocalizedString.Get("Transparent"), textColor: ActiveTheme.Instance.PrimaryTextColor)
                {
                    Checked = gcodeViewWidget.TransparentExtrusion,
                    Margin = new BorderDouble(5, 0, 0, 0),
                    HAnchor = HAnchor.ParentLeft,
                };

                transparentExtrusion.CheckedStateChanged += (sender, e) =>
                {
                    gcodeViewWidget.TransparentExtrusion = transparentExtrusion.Checked;
                };
                layerInfoContainer.AddChild(transparentExtrusion);
            }

            // put in a simulate extrusion checkbox
            if (ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count) > 1)
			{
				CheckBox hideExtruderOffsets = new CheckBox("Hide Offsets", textColor: ActiveTheme.Instance.PrimaryTextColor);
				hideExtruderOffsets.Checked = gcodeViewWidget.HideExtruderOffsets;
				hideExtruderOffsets.CheckedStateChanged += (sender, e) =>
				{
					gcodeViewWidget.HideExtruderOffsets = hideExtruderOffsets.Checked;
				};
				layerInfoContainer.AddChild(hideExtruderOffsets);
			}

			// put in a show 3D view checkbox
			{
				viewControlsToggle.twoDimensionButton.CheckedStateChanged += (sender, e) =>
				{
					SetLayerViewType();
				};
				viewControlsToggle.threeDimensionButton.CheckedStateChanged += (sender, e) =>
				{
					SetLayerViewType();
				};
				SetLayerViewType();
			}

			// Put in the sync to print checkbox
			if (windowMode == WindowMode.Embeded)
			{
				syncToPrint = new CheckBox("Sync To Print".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				syncToPrint.Checked = (UserSettings.Instance.get("LayerViewSyncToPrint") == "True");
				syncToPrint.Name = "Sync To Print Checkbox";
				syncToPrint.CheckedStateChanged += (sender, e) =>
				{
					UserSettings.Instance.set("LayerViewSyncToPrint", syncToPrint.Checked.ToString());
					SetSyncToPrintVisibility();
				};
				layerInfoContainer.AddChild(syncToPrint);

				// The idea here is we just got asked to rebuild the window (and it is being created now)
				// because the gcode finished creating for the print that is printing.
				// We don't want to be notified if any other updates happen to this gcode while it is printing.
				if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting
					&& PrinterConnectionAndCommunication.Instance.ActivePrintItem == printItem)
				{
					printItem.SlicingOutputMessage -= sliceItem_SlicingOutputMessage;
					printItem.SlicingDone -= sliceItem_Done;

					generateGCodeButton.Visible = false;

					// However if the print finished or is canceled we are going to want to get updates again. So, hook the status event
					PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(HookUpGCodeMessagesWhenDonePrinting, ref unregisterEvents);
					UiThread.RunOnIdle(SetSyncToPrintVisibility);
				}
			}

			//layerInfoContainer.AddChild(new CheckBox("Show Retractions", textColor: ActiveTheme.Instance.PrimaryTextColor));

			buttonPanel.AddChild(layerInfoContainer);

			textImageButtonFactory.FixedWidth = oldWidth;
		}

		private void SetSyncToPrintVisibility()
		{
			if (windowMode == WindowMode.Embeded)
			{
				bool printerIsRunningPrint = PrinterConnectionAndCommunication.Instance.PrinterIsPaused || PrinterConnectionAndCommunication.Instance.PrinterIsPrinting;

				if (syncToPrint.Checked && printerIsRunningPrint)
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

		private void SetLayerViewType()
		{
			if (viewControlsToggle.threeDimensionButton.Checked)
			{
				UserSettings.Instance.set("LayerViewDefault", "3D Layer");
				viewControls2D.Visible = false;
				gcodeViewWidget.Visible = false;

				viewControls3D.Visible = true;
				meshViewerWidget.Visible = true;
			}
			else
			{
				UserSettings.Instance.set("LayerViewDefault", "2D Layer");
				viewControls2D.Visible = true;
				gcodeViewWidget.Visible = true;

				viewControls3D.Visible = false;
				meshViewerWidget.Visible = false;
			}
		}

		private void HookUpGCodeMessagesWhenDonePrinting(object sender, EventArgs e)
		{
			if (!PrinterConnectionAndCommunication.Instance.PrinterIsPaused && !PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
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

		private string partToStartLoadingOnFirstDraw = null;

		private GuiWidget CreateGCodeViewWidget(string pathAndFileName)
		{
			gcodeViewWidget = new ViewGcodeWidget(new Vector2(viewerVolume.x, viewerVolume.y), bedCenter);
			gcodeViewWidget.DoneLoading += DoneLoadingGCode;
			gcodeViewWidget.LoadingProgressChanged += LoadingProgressChanged;
			partToStartLoadingOnFirstDraw = pathAndFileName;

			return gcodeViewWidget;
		}

		private GuiWidget widgetThatHasKeyDownHooked = null;

		public override void OnDraw(Graphics2D graphics2D)
		{
			bool printerIsRunningPrint = PrinterConnectionAndCommunication.Instance.PrinterIsPaused || PrinterConnectionAndCommunication.Instance.PrinterIsPrinting;
			if (syncToPrint != null
				&& syncToPrint.Checked
				&& printerIsRunningPrint)
			{
				SetAnimationPosition();
			}

			EnsureKeyDownHooked();

			if (partToStartLoadingOnFirstDraw != null)
			{
				gcodeViewWidget.LoadInBackground(partToStartLoadingOnFirstDraw);
				partToStartLoadingOnFirstDraw = null;
			}
			base.OnDraw(graphics2D);
		}

		private void EnsureKeyDownHooked()
		{
			// let's just check that we are still hooked up to our parent window (this is to make pop outs work correctly)
			if (widgetThatHasKeyDownHooked != null)
			{
				GuiWidget topParent = Parent;
				while (topParent as SystemWindow == null)
				{
					topParent = topParent.Parent;
				}

				if (topParent != widgetThatHasKeyDownHooked)
				{
					widgetThatHasKeyDownHooked.KeyDown -= Parent_KeyDown;
					widgetThatHasKeyDownHooked = null;
				}
			}

			if (widgetThatHasKeyDownHooked == null)
			{
				GuiWidget parent = Parent;
				while (parent as SystemWindow == null)
				{
					parent = parent.Parent;
				}
				UnHookWidgetThatHasKeyDownHooked();
				parent.KeyDown += Parent_KeyDown;
				widgetThatHasKeyDownHooked = parent;
			}
		}

		private void UnHookWidgetThatHasKeyDownHooked()
		{
			if (widgetThatHasKeyDownHooked != null)
			{
				widgetThatHasKeyDownHooked.KeyDown -= Parent_KeyDown;
			}
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
				gcodeProcessingStateInfoText = new TextWidget(message);
				gcodeProcessingStateInfoText.HAnchor = HAnchor.ParentCenter;
				gcodeProcessingStateInfoText.VAnchor = VAnchor.ParentCenter;
				gcodeProcessingStateInfoText.AutoExpandBoundsToText = true;

				GuiWidget labelContainer = new GuiWidget();
				labelContainer.AnchorAll();
				labelContainer.AddChild(gcodeProcessingStateInfoText);
				labelContainer.Selectable = false;

				gcodeDisplayWidget.AddChild(labelContainer);
			}

			if (message == "")
			{
				gcodeProcessingStateInfoText.BackgroundColor = new RGBA_Bytes();
			}
			else
			{
				gcodeProcessingStateInfoText.BackgroundColor = RGBA_Bytes.White;
			}

			gcodeProcessingStateInfoText.Text = message;
		}

		private void LoadingProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			SetProcessingMessage(string.Format("{0} {1}%...", "Loading G-Code".Localize(), e.ProgressPercentage));
		}

		private void CloseIfNotNull(GuiWidget widget)
		{
			if (widget != null)
			{
				widget.Close();
			}
		}

		private static bool RunningIn32Bit()
		{
			if (IntPtr.Size == 4)
			{
				return true;
			}

			return false;
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
				CloseIfNotNull(gradientWidget);
				gradientWidget = new ColorGradientWidget(gcodeViewWidget.LoadedGCode);
				AddChild(gradientWidget);
				gradientWidget.Visible = false;

				CreateOptionsContent();
				setGradientVisibility();
				buttonRightPanel.Visible = true;
				viewControlsToggle.Visible = true;

				CloseIfNotNull(setLayerWidget);
				setLayerWidget = new SetLayerWidget(gcodeViewWidget);
				setLayerWidget.VAnchor = Agg.UI.VAnchor.ParentTop;
				layerSelectionButtonsPanel.AddChild(setLayerWidget);

				CloseIfNotNull(navigationWidget);
				navigationWidget = new LayerNavigationWidget(gcodeViewWidget);
				navigationWidget.Margin = new BorderDouble(0, 0, 20, 0);
				layerSelectionButtonsPanel.AddChild(navigationWidget);

				CloseIfNotNull(selectLayerSlider);
				selectLayerSlider = new SolidSlider(new Vector2(), sliderWidth, 0, gcodeViewWidget.LoadedGCode.NumChangesInZ - 1, Orientation.Vertical);
				selectLayerSlider.ValueChanged += new EventHandler(selectLayerSlider_ValueChanged);
				gcodeViewWidget.ActiveLayerChanged += new EventHandler(gcodeViewWidget_ActiveLayerChanged);
				AddChild(selectLayerSlider);

				CloseIfNotNull(layerRenderRatioSlider);
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

				meshViewerWidget.partProcessingInfo.Visible = false;
			}
		}

		private void setGradientVisibility()
		{
			if (showSpeeds.Checked)
			{
				gradientWidget.Visible = true;
			}
			else
			{
				gradientWidget.Visible = false;
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

		private void AddHandlers()
		{
			expandModelOptions.CheckedStateChanged += expandModelOptions_CheckedStateChanged;
			expandDisplayOptions.CheckedStateChanged += expandDisplayOptions_CheckedStateChanged;
		}

		private void expandModelOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (modelOptionsContainer.Visible = expandModelOptions.Checked)
			{
				if (expandModelOptions.Checked == true)
				{
					expandDisplayOptions.Checked = false;
				}
				modelOptionsContainer.Visible = expandModelOptions.Checked;
			}
		}

		private void expandDisplayOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (displayOptionsContainer.Visible != expandDisplayOptions.Checked)
			{
				if (expandDisplayOptions.Checked == true)
				{
					expandModelOptions.Checked = false;
				}
				displayOptionsContainer.Visible = expandDisplayOptions.Checked;
			}
		}

		public override void OnClosed(EventArgs e)
		{
			UnHookWidgetThatHasKeyDownHooked();

			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
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

		private void generateButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(DoGenerateButton_Click, sender);
		}

		private void DoGenerateButton_Click(object state)
		{
			if (PrinterConnectionAndCommunication.Instance.ActivePrinter != null)
			{
				if (ActiveSliceSettings.Instance.IsValid() && printItem != null)
				{
					((Button)state).Visible = false;
					SlicingQueue.Instance.QueuePartForSlicing(printItem);
					startedSliceFromGenerateButton = true;
				}
			}
			else
			{
				StyledMessageBox.ShowMessageBox(null, "Oops! Please select a printer in order to continue slicing.", "Select Printer", StyledMessageBox.MessageType.OK);
			}
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

	public class SetLayerWidget : FlowLayoutWidget
	{
		private NumberEdit editCurrentLayerIndex;
		private Button setLayerButton;
		private ViewGcodeWidget gcodeViewWidget;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		public SetLayerWidget(ViewGcodeWidget gcodeViewWidget)
			: base(FlowDirection.LeftToRight)
		{
			this.gcodeViewWidget = gcodeViewWidget;

			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			editCurrentLayerIndex = new NumberEdit(1, pixelWidth: 40);
			editCurrentLayerIndex.VAnchor = VAnchor.ParentCenter;
			editCurrentLayerIndex.Margin = new BorderDouble(5, 0);
			editCurrentLayerIndex.EditComplete += new EventHandler(editCurrentLayerIndex_EditComplete);
			editCurrentLayerIndex.Name = "Current GCode Layer Edit";
			this.AddChild(editCurrentLayerIndex);
			gcodeViewWidget.ActiveLayerChanged += new EventHandler(gcodeViewWidget_ActiveLayerChanged);

			setLayerButton = textImageButtonFactory.Generate(LocalizedString.Get("Go"));
			setLayerButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
			setLayerButton.Click += new EventHandler(layerCountTextWidget_EditComplete);
			this.AddChild(setLayerButton);
		}

		private void gcodeViewWidget_ActiveLayerChanged(object sender, EventArgs e)
		{
			editCurrentLayerIndex.Value = gcodeViewWidget.ActiveLayerIndex + 1;
		}

		private void editCurrentLayerIndex_EditComplete(object sender, EventArgs e)
		{
			gcodeViewWidget.ActiveLayerIndex = ((int)editCurrentLayerIndex.Value - 1);
			editCurrentLayerIndex.Value = gcodeViewWidget.ActiveLayerIndex + 1;
		}

		private void layerCountTextWidget_EditComplete(object sender, EventArgs e)
		{
			gcodeViewWidget.ActiveLayerIndex = ((int)editCurrentLayerIndex.Value - 1);
		}
	}

	public class LayerNavigationWidget : FlowLayoutWidget
	{
		private Button prevLayerButton;
		private Button nextLayerButton;
		private TextWidget layerCountTextWidget;
		private ViewGcodeWidget gcodeViewWidget;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		public LayerNavigationWidget(ViewGcodeWidget gcodeViewWidget)
			: base(FlowDirection.LeftToRight)
		{
			this.gcodeViewWidget = gcodeViewWidget;

			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			prevLayerButton = textImageButtonFactory.Generate("<<");
			prevLayerButton.Click += new EventHandler(prevLayer_ButtonClick);
			this.AddChild(prevLayerButton);

			layerCountTextWidget = new TextWidget("/1____", 12);
			layerCountTextWidget.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			layerCountTextWidget.VAnchor = VAnchor.ParentCenter;
			layerCountTextWidget.AutoExpandBoundsToText = true;
			layerCountTextWidget.Margin = new BorderDouble(5, 0);
			this.AddChild(layerCountTextWidget);

			nextLayerButton = textImageButtonFactory.Generate(">>");
			nextLayerButton.Click += new EventHandler(nextLayer_ButtonClick);
			this.AddChild(nextLayerButton);
		}

		private void nextLayer_ButtonClick(object sender, EventArgs mouseEvent)
		{
			gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex + 1);
		}

		private void prevLayer_ButtonClick(object sender, EventArgs mouseEvent)
		{
			gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex - 1);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (gcodeViewWidget.LoadedGCode != null)
			{
				layerCountTextWidget.Text = string.Format("{0} / {1}", gcodeViewWidget.ActiveLayerIndex + 1, gcodeViewWidget.LoadedGCode.NumChangesInZ.ToString());
			}
			base.OnDraw(graphics2D);
		}
	}
}
