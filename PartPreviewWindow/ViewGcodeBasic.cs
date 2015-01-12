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

using System;
using System.ComponentModel;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Agg.UI;
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

        SetLayerWidget setLayerWidget;
        LayerNavigationWidget navigationWidget;
        public DoubleSolidSlider layerRenderRatioSlider;
        
        TextWidget gcodeProcessingStateInfoText;
        ViewGcodeWidget gcodeViewWidget;
        PrintItemWrapper printItem;
        bool startedSliceFromGenerateButton = false;
        Button generateGCodeButton;
        FlowLayoutWidget buttonBottomPanel;
        FlowLayoutWidget layerSelectionButtonsPanel;

        FlowLayoutWidget modelOptionsContainer;
        FlowLayoutWidget displayOptionsContainer;
		ViewControlsToggle viewControlsToggle;

        CheckBox expandModelOptions;
        CheckBox expandDisplayOptions;
        CheckBox syncToPrint;

        GuiWidget gcodeDisplayWidget;

        EventHandler unregisterEvents;
        WindowMode windowMode;

        public delegate Vector2 GetSizeFunction();

        static string slicingErrorMessage = "Slicing Error.\nPlease review your slice settings.".Localize();
        static string pressGenerateMessage = "Press 'generate' to view layers".Localize();
        static string fileNotFoundMessage = "File not found on disk.".Localize();

        Vector2 bedCenter;
        Vector3 viewerVolume;
        MeshViewerWidget.BedShape bedShape;
        int sliderWidth;

        public ViewGcodeBasic(PrintItemWrapper printItem, Vector3 viewerVolume, Vector2 bedCenter, MeshViewerWidget.BedShape bedShape, WindowMode windowMode)
        {
            this.viewerVolume = viewerVolume;
            this.bedShape = bedShape;
            this.bedCenter = bedCenter;
            this.windowMode = windowMode;
            this.printItem = printItem;

            if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
            {
                sliderWidth = 20;
            }
            else
            {
                sliderWidth = 10;
            }

            CreateAndAddChildren(null);

            SliceSettingsWidget.RegisterForSettingsChange("bed_size", RecreateBedAndPartPosition, ref unregisterEvents);
            SliceSettingsWidget.RegisterForSettingsChange("print_center", RecreateBedAndPartPosition, ref unregisterEvents);
            SliceSettingsWidget.RegisterForSettingsChange("build_height", RecreateBedAndPartPosition, ref unregisterEvents);
            SliceSettingsWidget.RegisterForSettingsChange("bed_shape", RecreateBedAndPartPosition, ref unregisterEvents);
            SliceSettingsWidget.RegisterForSettingsChange("center_part_on_bed", RecreateBedAndPartPosition, ref unregisterEvents);

            SliceSettingsWidget.RegisterForSettingsChange("extruder_offset", Clear3DGCode, ref unregisterEvents);

            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(RecreateBedAndPartPosition, ref unregisterEvents);
        }

        void Clear3DGCode(object sender, EventArgs e)
        {
            if (gcodeViewWidget != null
                && gcodeViewWidget.gCodeRenderer != null)
            {
                gcodeViewWidget.gCodeRenderer.Clear3DGCode();
                gcodeViewWidget.Invalidate();
            }
        }

        void RecreateBedAndPartPosition(object sender, EventArgs e)
        {
            viewerVolume = new Vector3(ActiveSliceSettings.Instance.BedSize, ActiveSliceSettings.Instance.BuildHeight);
            bedShape = ActiveSliceSettings.Instance.BedShape;
            bedCenter = ActiveSliceSettings.Instance.BedCenter;

            double buildHeight = ActiveSliceSettings.Instance.BuildHeight;

            UiThread.RunOnIdle((state) =>
            {
                meshViewerWidget.CreatePrintBed(
                    viewerVolume,
                    bedCenter,
                    bedShape);
            });
        }

        void CreateAndAddChildren(object state)
        {
            TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

            CloseAndRemoveAllChildren();
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

            SetProcessingMessage("Press 'Add' to select an item.".Localize());
            if (printItem != null)
            {
                SetProcessingMessage(LocalizedString.Get("Loading GCode..."));
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
                            SetProcessingMessage(slicingErrorMessage);
                        }
                        else
                        {
                            SetProcessingMessage(pressGenerateMessage);
                        }

                        if (File.Exists(gcodePathAndFileName) && gcodeFileIsComplete)
                        {
                            gcodeDisplayWidget.AddChild(CreateGCodeViewWidget(gcodePathAndFileName));
                        }

                        // we only hook these up to make sure we can regenerate the gcode when we want
                        printItem.SlicingOutputMessage.RegisterEvent(sliceItem_SlicingOutputMessage, ref unregisterEvents);
                        printItem.SlicingDone.RegisterEvent(sliceItem_Done, ref unregisterEvents);
                    }
                    else
                    {
                        SetProcessingMessage(string.Format("{0}\n'{1}'", fileNotFoundMessage, printItem.Name));
                    }
                }
            }
            else
            {
                generateGCodeButton.Visible = false;
            }

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
            viewControls3D = new ViewControls3D(meshViewerWidget);
            viewControls3D.PartSelectVisible = false;
            AddChild(viewControls3D);
			viewControls3D.rotateButton.ClickButton(null);
			viewControls3D.Visible = false;

			viewControlsToggle = new ViewControlsToggle ();
            viewControlsToggle.HAnchor = Agg.UI.HAnchor.ParentRight;
			AddChild (viewControlsToggle);
            viewControlsToggle.Visible = false;

            //viewControls3D.translateButton.ClickButton(null);
            
            // move things into the right place and scale
            {
                Vector3 bedCenter3D = new Vector3(bedCenter, 0);
                meshViewerWidget.PrinterBed.Translate(bedCenter3D);
                meshViewerWidget.TrackballTumbleWidget.TrackBallController.Scale = .05;
                meshViewerWidget.TrackballTumbleWidget.TrackBallController.Translate(-bedCenter3D);
            }

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

        RenderType GetRenderType()
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
            if (gcodeViewWidget.HideExtruderOffsets)
            {
                renderType |= RenderType.HideExtruderOffsets;
            }

            return renderType;
        }

        void TrackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
        {
            GCodeRenderer.ExtrusionColor = ActiveTheme.Instance.PrimaryAccentColor;
            
            GCodeRenderInfo renderInfo = new GCodeRenderInfo(0, 
                Math.Min(gcodeViewWidget.ActiveLayerIndex + 1,gcodeViewWidget.LoadedGCode.NumChangesInZ),
                gcodeViewWidget.TotalTransform,
                1, 
                GetRenderType(),
                gcodeViewWidget.FeatureToStartOnRatio0To1, 
                gcodeViewWidget.FeatureToEndOnRatio0To1,
                new Vector2[] { ActiveSliceSettings.Instance.GetOffset(0), ActiveSliceSettings.Instance.GetOffset(1) });

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
				expandModelOptions = expandMenuOptionFactory.GenerateCheckBoxButton(label, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandModelOptions.Margin = new BorderDouble(bottom: 2);
                buttonRightPanel.AddChild(expandModelOptions);
                expandModelOptions.Checked = true;

                modelOptionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                modelOptionsContainer.HAnchor = HAnchor.ParentLeftRight;
                //modelOptionsContainer.Visible = false;
                buttonRightPanel.AddChild(modelOptionsContainer);

				expandDisplayOptions = expandMenuOptionFactory.GenerateCheckBoxButton("Display".Localize().ToUpper(), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
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
            buttonPanel.CloseAndRemoveAllChildren();

            double oldWidth = textImageButtonFactory.FixedWidth;
            textImageButtonFactory.FixedWidth = 44;

            FlowLayoutWidget modelInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            modelInfoContainer.HAnchor = HAnchor.ParentLeftRight;
            modelInfoContainer.Padding = new BorderDouble(5);

			string printTimeLabel = "Print Time".Localize().ToUpper();
			string printTimeLabelFull = string.Format ("{0}:", printTimeLabel);
            // put in the print time
            modelInfoContainer.AddChild(new TextWidget(printTimeLabelFull, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize:10));
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
                estimatedPrintTime.Margin = new BorderDouble(0, 9, 0 ,3);
                modelInfoContainer.AddChild(estimatedPrintTime);
            }

            //modelInfoContainer.AddChild(new TextWidget("Size:", textColor: ActiveTheme.Instance.PrimaryTextColor));
            
			string filamentLengthLabel = "Filament Length".Localize().ToUpper();
			string filamentLengthLabelFull = string.Format ("{0}:", filamentLengthLabel);
            // show the filament used
            modelInfoContainer.AddChild(new TextWidget(filamentLengthLabelFull, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
            {
                double filamentUsed = gcodeViewWidget.LoadedGCode.GetFilamentUsedMm(ActiveSliceSettings.Instance.NozzleDiameter);

                GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.0} mm", filamentUsed), pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor);
                //estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
                estimatedPrintTime.Margin = new BorderDouble(0, 9, 0, 3);
                modelInfoContainer.AddChild(estimatedPrintTime);
            }

			string filamentVolumeLabel = "Filament Volume".Localize().ToUpper();
			string filamentVolumeLabelFull = string.Format("{0}:", filamentVolumeLabel);
            modelInfoContainer.AddChild(new TextWidget(filamentVolumeLabelFull, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
            {
                double filamentMm3 = gcodeViewWidget.LoadedGCode.GetFilamentCubicMm(ActiveSliceSettings.Instance.FilamentDiameter);

                GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.00} cm3", filamentMm3 / 1000), pointSize:14, textColor: ActiveTheme.Instance.PrimaryTextColor);
                //estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
                estimatedPrintTime.Margin = new BorderDouble(0, 9, 0, 3);
                modelInfoContainer.AddChild(estimatedPrintTime);
            }

			string weightLabel = "Est. Weight".Localize().ToUpper();
			string weightLabelFull = string.Format("{0}:", weightLabel);
            modelInfoContainer.AddChild(new TextWidget(weightLabelFull, pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor));
            {
                var density = 1.0;
                string filamentType = "PLA";
                if (filamentType == "ABS")
                {
                    density = 1.04;
                }
                else if (filamentType == "PLA")
                {
                    density = 1.24;
                }

                double filamentWeightGrams = gcodeViewWidget.LoadedGCode.GetFilamentWeightGrams(ActiveSliceSettings.Instance.FilamentDiameter, density);

                GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.00} g", filamentWeightGrams), pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor);
                //estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
                estimatedPrintTime.Margin = new BorderDouble(0, 9, 0, 3);
                modelInfoContainer.AddChild(estimatedPrintTime);
            }

            //modelInfoContainer.AddChild(new TextWidget("Layer Count:", textColor: ActiveTheme.Instance.PrimaryTextColor));

            buttonPanel.AddChild(modelInfoContainer);

            textImageButtonFactory.FixedWidth = oldWidth;
        }

        private void AddLayerInfo(FlowLayoutWidget buttonPanel)
        {
            double oldWidth = textImageButtonFactory.FixedWidth;
            textImageButtonFactory.FixedWidth = 44;

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
            buttonPanel.CloseAndRemoveAllChildren();

            double oldWidth = textImageButtonFactory.FixedWidth; 
            textImageButtonFactory.FixedWidth = 44;

            FlowLayoutWidget layerInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            layerInfoContainer.HAnchor = HAnchor.ParentLeftRight;
            layerInfoContainer.Padding = new BorderDouble(5);

            // put in a show grid check box
            {
                CheckBox showGrid = new CheckBox(LocalizedString.Get("Grid"), textColor: ActiveTheme.Instance.PrimaryTextColor);
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
                CheckBox showSpeeds = new CheckBox(LocalizedString.Get("Speeds"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                showSpeeds.Checked = gcodeViewWidget.RenderSpeeds;
                showSpeeds.CheckedStateChanged += (sender, e) =>
                {
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

            // put in a simulate extrusion checkbox
            if(ActiveSliceSettings.Instance.ExtruderCount > 1)
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
				SetLayerViewType ();
            }

            // Put in the sync to print checkbox
            if (windowMode == WindowMode.Embeded)
            {
                syncToPrint = new CheckBox("Sync To Print".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				syncToPrint.Checked = (UserSettings.Instance.get("LayerViewSyncToPrint") == "True");
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
                    printItem.SlicingOutputMessage.UnregisterEvent(sliceItem_SlicingOutputMessage, ref unregisterEvents);
                    printItem.SlicingDone.UnregisterEvent(sliceItem_Done, ref unregisterEvents);

                    generateGCodeButton.Visible = false;
                    
                    // However if the print finished or is canceled we are going to want to get updates again. So, hook the status event
                    PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(HookUpGCodeMessagesWhenDonePrinting, ref unregisterEvents);
                    UiThread.RunOnIdle((state) =>
                    {
                        SetSyncToPrintVisibility();
                    });
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

		void SetLayerViewType()
		{
			if (viewControlsToggle.threeDimensionButton.Checked)
			{
				UserSettings.Instance.set ("LayerViewDefault", "3D Layer");
				viewControls2D.Visible = false;
				gcodeViewWidget.Visible = false;

				viewControls3D.Visible = true;
				meshViewerWidget.Visible = true;
			}
			else
			{
				UserSettings.Instance.set ("LayerViewDefault", "2D Layer");
				viewControls2D.Visible = true;
				gcodeViewWidget.Visible = true;

				viewControls3D.Visible = false;
				meshViewerWidget.Visible = false;
			}
		}

        void HookUpGCodeMessagesWhenDonePrinting(object sender, EventArgs e)
        {
            if(!PrinterConnectionAndCommunication.Instance.PrinterIsPaused && !PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
            {
                // unregister first to make sure we don't double up in error (should not be needed but no harm)
                printItem.SlicingOutputMessage.UnregisterEvent(sliceItem_SlicingOutputMessage, ref unregisterEvents);
                printItem.SlicingDone.UnregisterEvent(sliceItem_Done, ref unregisterEvents);

                // register for done slicing and slicing messages
                printItem.SlicingOutputMessage.RegisterEvent(sliceItem_SlicingOutputMessage, ref unregisterEvents);
                printItem.SlicingDone.RegisterEvent(sliceItem_Done, ref unregisterEvents);
            
                generateGCodeButton.Visible = true;
            }
            SetSyncToPrintVisibility();
        }

        string partToStartLoadingOnFirstDraw = null;
        private GuiWidget CreateGCodeViewWidget(string pathAndFileName)
        {
            gcodeViewWidget = new ViewGcodeWidget(new Vector2(viewerVolume.x, viewerVolume.y), bedCenter);
            gcodeViewWidget.DoneLoading += DoneLoadingGCode;
            gcodeViewWidget.LoadingProgressChanged += LoadingProgressChanged;
            partToStartLoadingOnFirstDraw = pathAndFileName;

            return gcodeViewWidget;
        }

        GuiWidget widgetThatHasKeyDownHooked = null;
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
                parent.KeyDown += Parent_KeyDown;
                widgetThatHasKeyDownHooked = parent;
            }
        }

        void Parent_KeyDown(object sender, KeyEventArgs keyEvent)
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

        void LoadingProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            SetProcessingMessage(string.Format("Loading GCode {0}%...", e.ProgressPercentage));
        }

        void CloseIfNotNull(GuiWidget widget)
        {
            if (widget != null)
            {
                widget.Close();
            }
        }

        void DoneLoadingGCode(object sender, EventArgs e)
        {
            SetProcessingMessage("");
            if (gcodeViewWidget != null
                && gcodeViewWidget.LoadedGCode != null
                && gcodeViewWidget.LoadedGCode.Count > 0)
            {
                CreateOptionsContent();
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

        void layerStartRenderRatioSlider_ValueChanged(object sender, EventArgs e)
        {
            gcodeViewWidget.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
            gcodeViewWidget.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;
            gcodeViewWidget.Invalidate();
        }

        void layerEndRenderRatioSlider_ValueChanged(object sender, EventArgs e)
        {
            gcodeViewWidget.FeatureToStartOnRatio0To1 = layerRenderRatioSlider.FirstValue;
            gcodeViewWidget.FeatureToEndOnRatio0To1 = layerRenderRatioSlider.SecondValue;
            gcodeViewWidget.Invalidate();
        }

        void gcodeViewWidget_ActiveLayerChanged(object sender, EventArgs e)
        {
            if (gcodeViewWidget.ActiveLayerIndex != (int)(selectLayerSlider.Value + .5))
            {
                selectLayerSlider.Value = gcodeViewWidget.ActiveLayerIndex;
            }
        }

        void selectLayerSlider_ValueChanged(object sender, EventArgs e)
        {
            gcodeViewWidget.ActiveLayerIndex = (int)(selectLayerSlider.Value + .5);
        }

        void PartPreviewGCode_BoundsChanged(object sender, EventArgs e)
        {
            SetSliderSizes();
        }

        void SetSliderSizes()
        {
            selectLayerSlider.OriginRelativeParent = new Vector2(gcodeDisplayWidget.Width - 20, 70);
            selectLayerSlider.TotalWidthInPixels = gcodeDisplayWidget.Height - 80;

            layerRenderRatioSlider.OriginRelativeParent = new Vector2(60, 70);
            layerRenderRatioSlider.TotalWidthInPixels = gcodeDisplayWidget.Width - 100;
        }

        private void AddHandlers()
        {
            expandModelOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandModelOptions_CheckedStateChanged);
            expandDisplayOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandDisplayOptions_CheckedStateChanged);
        }
        

        void expandModelOptions_CheckedStateChanged(object sender, EventArgs e)
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

        void expandDisplayOptions_CheckedStateChanged(object sender, EventArgs e)
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
            if (widgetThatHasKeyDownHooked != null)
            {
                widgetThatHasKeyDownHooked.KeyDown -= Parent_KeyDown;
            }

            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }

            if (printItem != null)
            {
                printItem.SlicingOutputMessage.UnregisterEvent(sliceItem_SlicingOutputMessage, ref unregisterEvents);
                printItem.SlicingDone.UnregisterEvent(sliceItem_Done, ref unregisterEvents);
                if (startedSliceFromGenerateButton && printItem.CurrentlySlicing)
                {
                    SlicingQueue.Instance.CancelCurrentSlicing();
                }
            }

            base.OnClosed(e);
        }

        void generateButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle(DoGenerateButton_Click, sender);
        }

        void DoGenerateButton_Click(object state)
        {
            if (ActiveSliceSettings.Instance.IsValid() && printItem != null)
            {
                ((Button)state).Visible = false;
                SlicingQueue.Instance.QueuePartForSlicing(printItem);
                startedSliceFromGenerateButton = true;
            }
        }

        void sliceItem_SlicingOutputMessage(object sender, EventArgs e)
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

        void sliceItem_Done(object sender, EventArgs e)
        {
            // We can add this while we have it open (when we are done loading).
            // So we need to make sure we only have it added once. This will be ok to run when
            // not added or when added and will ensure we only have one hook.
            printItem.SlicingOutputMessage.UnregisterEvent(sliceItem_SlicingOutputMessage, ref unregisterEvents);
            printItem.SlicingDone.UnregisterEvent(sliceItem_Done, ref unregisterEvents);

            UiThread.RunOnIdle(CreateAndAddChildren);
            startedSliceFromGenerateButton = false;
        }
    }

    public class SetLayerWidget : FlowLayoutWidget
    {
        NumberEdit editCurrentLayerIndex;
        Button setLayerButton;
        ViewGcodeWidget gcodeViewWidget;
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();        

        public SetLayerWidget(ViewGcodeWidget gcodeViewWidget)
            :base(FlowDirection.LeftToRight)
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
            this.AddChild(editCurrentLayerIndex);
            gcodeViewWidget.ActiveLayerChanged += new EventHandler(gcodeViewWidget_ActiveLayerChanged);

			setLayerButton = textImageButtonFactory.Generate(LocalizedString.Get("Go"));
            setLayerButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
            setLayerButton.Click += new EventHandler(layerCountTextWidget_EditComplete);
            this.AddChild(setLayerButton);
        }

        void gcodeViewWidget_ActiveLayerChanged(object sender, EventArgs e)
        {
            editCurrentLayerIndex.Value = gcodeViewWidget.ActiveLayerIndex + 1;
        }

        void editCurrentLayerIndex_EditComplete(object sender, EventArgs e)
        {
            gcodeViewWidget.ActiveLayerIndex = ((int)editCurrentLayerIndex.Value - 1);
            editCurrentLayerIndex.Value = gcodeViewWidget.ActiveLayerIndex + 1;
        }

        void layerCountTextWidget_EditComplete(object sender, EventArgs e)
        {
            gcodeViewWidget.ActiveLayerIndex = ((int)editCurrentLayerIndex.Value - 1);
        }
    }

    public class LayerNavigationWidget : FlowLayoutWidget
    {
        Button prevLayerButton;
        Button nextLayerButton;        
        TextWidget layerCountTextWidget;        
        ViewGcodeWidget gcodeViewWidget;
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public LayerNavigationWidget(ViewGcodeWidget gcodeViewWidget)
            :base(FlowDirection.LeftToRight)
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

        void nextLayer_ButtonClick(object sender, EventArgs mouseEvent)
        {
            gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex + 1);
        }

        void prevLayer_ButtonClick(object sender, EventArgs mouseEvent)
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
