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
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MeshVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class ViewGcodeBasic : PartPreview3DWidget
    {
        public Slider selectLayerSlider;
        public Slider layerStartRenderRatioSlider;
        public Slider layerEndRenderRatioSlider;
        TextWidget gcodeProcessingStateInfoText;
        GCodeViewWidget gcodeViewWidget;
        PrintItemWrapper printItem;
        bool startedSliceFromGenerateButton = false;
        Button generateGCodeButton;
        FlowLayoutWidget buttonBottomPanel;
        FlowLayoutWidget layerSelectionButtonsPanel;

        FlowLayoutWidget modelOptionsContainer;
        FlowLayoutWidget displayOptionsContainer;

        CheckBox expandModelOptions;
        CheckBox expandDisplayOptions;
        CheckBox syncToPrint;

        GuiWidget gcodeDispalyWidget;

        EventHandler unregisterEvents;
        bool widgetHasCloseButton;

        public delegate Vector2 GetSizeFunction();

        static string slicingErrorMessage = "Slicing Error.\nPlease review your slice settings.".Localize();
        static string pressGenerateMessage = "Press 'generate' to view layers".Localize();
        static string fileNotFoundMessage = "File not found on disk.".Localize();

        Vector2 bedCenter;
        Vector3 viewerVolume;
        MeshViewerWidget.BedShape bedShape;

        public ViewGcodeBasic(PrintItemWrapper printItem, Vector3 viewerVolume, MeshViewerWidget.BedShape bedShape, Vector2 bedCenter, bool addCloseButton)
        {
            this.viewerVolume = viewerVolume;
            this.bedShape = bedShape;
            this.bedCenter = bedCenter;
            widgetHasCloseButton = addCloseButton;
            this.printItem = printItem;

            CreateAndAddChildren(null);
        }

        void CreateAndAddChildren(object state)
        {
            TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

            RemoveAllChildren();
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
            generateGCodeButton.Click += new ButtonBase.ButtonEventHandler(generateButton_Click);
            buttonBottomPanel.AddChild(generateGCodeButton);

            layerSelectionButtonsPanel = new FlowLayoutWidget(FlowDirection.RightToLeft);
            layerSelectionButtonsPanel.HAnchor = HAnchor.ParentLeftRight;
            layerSelectionButtonsPanel.Padding = new BorderDouble(0);

            GuiWidget holdPanelOpen = new GuiWidget(1, generateGCodeButton.Height);
            layerSelectionButtonsPanel.AddChild(holdPanelOpen);

            if (widgetHasCloseButton)
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

            gcodeDispalyWidget = new GuiWidget(HAnchor.ParentLeftRight, Agg.UI.VAnchor.ParentBottomTop);

            SetProcessingMessage("Press 'Add' to select an item.".Localize());
            if (printItem != null)
            {
                SetProcessingMessage(LocalizedString.Get("Loading GCode..."));
                if (Path.GetExtension(printItem.FileLocation).ToUpper() == ".GCODE")
                {
                    gcodeDispalyWidget.AddChild(CreateGCodeViewWidget(printItem.FileLocation));
                }
                else
                {
                    if (File.Exists(printItem.FileLocation))
                    {
                        string gcodePathAndFileName = printItem.GCodePathAndFileName;
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
                            gcodeDispalyWidget.AddChild(CreateGCodeViewWidget(gcodePathAndFileName));
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

            centerPartPreviewAndControls.AddChild(gcodeDispalyWidget);

            buttonRightPanel = CreateRightButtonPanel();
            centerPartPreviewAndControls.AddChild(buttonRightPanel);

            // add in a spacer
            layerSelectionButtonsPanel.AddChild(new GuiWidget(HAnchor.ParentLeftRight));
            buttonBottomPanel.AddChild(layerSelectionButtonsPanel);

            mainContainerTopToBottom.AddChild(centerPartPreviewAndControls);
            mainContainerTopToBottom.AddChild(buttonBottomPanel);
            this.AddChild(mainContainerTopToBottom);

            meshViewerWidget = new MeshViewerWidget(viewerVolume, 1, bedShape, "".Localize());
            meshViewerWidget.AnchorAll();
            meshViewerWidget.AlwaysRenderBed = true;
            gcodeDispalyWidget.AddChild(meshViewerWidget);
            meshViewerWidget.Visible = false;
            meshViewerWidget.TrackballTumbleWidget.DrawGlContent += new EventHandler(TrackballTumbleWidget_DrawGlContent);

            viewControls2D = new ViewControls2D();
            AddChild(viewControls2D);
            viewControls3D = new ViewControls3D(meshViewerWidget);
            AddChild(viewControls3D);
            viewControls3D.Visible = false;
            viewControls3D.translateButton.ClickButton(null);

            viewControls2D.translateButton.Click += (sender, e) =>
            {
                gcodeViewWidget.TransformState = GCodeViewWidget.ETransformState.Move;
            };
            viewControls2D.scaleButton.Click += (sender, e) =>
            {
                gcodeViewWidget.TransformState = GCodeViewWidget.ETransformState.Scale;
            };

            AddHandlers();
        }

        void TrackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
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

            gcodeViewWidget.gCodeRenderer.Render3D(0, Math.Min(gcodeViewWidget.ActiveLayerIndex + 1, gcodeViewWidget.LoadedGCode.NumChangesInZ), gcodeViewWidget.TotalTransform, 1, renderType,
                gcodeViewWidget.FeatureToStartOnRatio0To1, gcodeViewWidget.FeatureToEndOnRatio0To1);
        }

        private void SetAnimationPosition()
        {
            int currentLayer = PrinterCommunication.Instance.CurrentlyPrintingLayer;
            if (currentLayer >= 1)
            {
                selectLayerSlider.Value = currentLayer-1;
                layerEndRenderRatioSlider.Value = PrinterCommunication.Instance.RatioIntoCurrentLayer;
                layerStartRenderRatioSlider.Value = 0;
            }
        }

        private FlowLayoutWidget CreateRightButtonPanel()
        {
            FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
            buttonRightPanel.Width = 200;
            {
                BorderDouble buttonMargin = new BorderDouble(top: 3);

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
                expandDisplayOptions.Checked = true;

                displayOptionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                displayOptionsContainer.HAnchor = HAnchor.ParentLeftRight;
                //displayOptionsContainer.Visible = false;
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
            int oldWidth = textImageButtonFactory.FixedWidth;
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
            int oldWidth = textImageButtonFactory.FixedWidth;
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
            int oldWidth = textImageButtonFactory.FixedWidth; 
            textImageButtonFactory.FixedWidth = 44;

            FlowLayoutWidget layerInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            layerInfoContainer.HAnchor = HAnchor.ParentLeftRight;
            layerInfoContainer.Padding = new BorderDouble(5);

            // put in a show grid check box
            {
                CheckBox showGrid = new CheckBox(LocalizedString.Get("Show Grid"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                showGrid.Checked = gcodeViewWidget.RenderGrid;
                showGrid.CheckedStateChanged += (sender, e) =>
                {
                    gcodeViewWidget.RenderGrid = showGrid.Checked;
                };
                layerInfoContainer.AddChild(showGrid);
            }

            // put in a show moves checkbox
            {
                CheckBox showMoves = new CheckBox(LocalizedString.Get("Show Moves"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                showMoves.Checked = gcodeViewWidget.RenderMoves;
                showMoves.CheckedStateChanged += (sender, e) =>
                {
                    gcodeViewWidget.RenderMoves = showMoves.Checked;
                };
                layerInfoContainer.AddChild(showMoves);
            }

            // put in a show Retractions checkbox
            {
                CheckBox showRetractions = new CheckBox(LocalizedString.Get("Show Retractions"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                showRetractions.Checked = gcodeViewWidget.RenderRetractions;
                showRetractions.CheckedStateChanged += (sender, e) =>
                {
                    gcodeViewWidget.RenderRetractions = showRetractions.Checked;
                };
                layerInfoContainer.AddChild(showRetractions);
            }

            // put in a show 3D view checkbox
            {
                CheckBox show3D = new CheckBox(LocalizedString.Get("Show 3D"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                show3D.CheckedStateChanged += (sender, e) =>
                {
                    // show the tumbel widget and not the line widget
                    if (show3D.Checked)
                    {
                        viewControls2D.Visible = false;
                        gcodeViewWidget.Visible = false;

                        viewControls3D.Visible = true;
                        meshViewerWidget.Visible = true;
                    }
                    else
                    {
                        viewControls2D.Visible = true;
                        gcodeViewWidget.Visible = true;

                        viewControls3D.Visible = false;
                        meshViewerWidget.Visible = false;
                    }
                };
                layerInfoContainer.AddChild(show3D);
            }

            // Put in the sync to print checkbox
            if (!widgetHasCloseButton)
            {
                syncToPrint = new CheckBox("Sync To Print".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
                syncToPrint.Checked = false;
                syncToPrint.CheckedStateChanged += (sender, e) =>
                {
                    if (syncToPrint.Checked)
                    {
                        SetAnimationPosition();
                    }
                    else
                    {
                        if (layerEndRenderRatioSlider != null)
                        {
                            layerEndRenderRatioSlider.Value = 1;
                            layerStartRenderRatioSlider.Value = 0;
                        }
                    }
                };
                layerInfoContainer.AddChild(syncToPrint);
            }

            //layerInfoContainer.AddChild(new CheckBox("Show Retractions", textColor: ActiveTheme.Instance.PrimaryTextColor));

            buttonPanel.AddChild(layerInfoContainer);

            textImageButtonFactory.FixedWidth = oldWidth;
        }

        public override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
        }

        string partToStartLoadingOnFirstDraw = null;
        private GuiWidget CreateGCodeViewWidget(string pathAndFileName)
        {
            gcodeViewWidget = new GCodeViewWidget(new Vector2(viewerVolume.x, viewerVolume.y), bedCenter);
            gcodeViewWidget.DoneLoading += DoneLoadingGCode;
            gcodeViewWidget.LoadingProgressChanged += LoadingProgressChanged;
            partToStartLoadingOnFirstDraw = pathAndFileName;

            return gcodeViewWidget;
        }

        bool hookedParentKeyDown = false;
        public override void OnDraw(Graphics2D graphics2D)
        {
            if (syncToPrint != null && syncToPrint.Checked)
            {
                SetAnimationPosition();
            }

            if (!hookedParentKeyDown)
            {
                GuiWidget parent = Parent;
                while (parent as SystemWindow == null)
                {
                    parent = parent.Parent;
                }
                parent.KeyDown += Parent_KeyDown;
                hookedParentKeyDown = true;
            }
            if (partToStartLoadingOnFirstDraw != null)
            {
                gcodeViewWidget.LoadInBackground(partToStartLoadingOnFirstDraw);
                partToStartLoadingOnFirstDraw = null;
            }
            base.OnDraw(graphics2D);
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

                gcodeDispalyWidget.AddChild(labelContainer);
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

        void DoneLoadingGCode(object sender, EventArgs e)
        {
            SetProcessingMessage("");
            if (gcodeViewWidget != null
                && gcodeViewWidget.LoadedGCode != null
                && gcodeViewWidget.LoadedGCode.Count > 0)
            {
                CreateOptionsContent();

                SetLayerWidget setLayerWidget = new SetLayerWidget(gcodeViewWidget);
                setLayerWidget.VAnchor = Agg.UI.VAnchor.ParentTop;
                layerSelectionButtonsPanel.AddChild(setLayerWidget);

                LayerNavigationWidget navigationWidget = new LayerNavigationWidget(gcodeViewWidget);
                navigationWidget.Margin = new BorderDouble(0, 0, 20, 0);
                layerSelectionButtonsPanel.AddChild(navigationWidget);

                selectLayerSlider = new Slider(new Vector2(), 10, 0, gcodeViewWidget.LoadedGCode.NumChangesInZ - 1, Orientation.Vertical);
                selectLayerSlider.ValueChanged += new EventHandler(selectLayerSlider_ValueChanged);
                gcodeViewWidget.ActiveLayerChanged += new EventHandler(gcodeViewWidget_ActiveLayerChanged);
                AddChild(selectLayerSlider);

                AddChild(new TextWidget(LocalizedString.Get("start:"), 50, 77, 10, Agg.Font.Justification.Right));
                layerStartRenderRatioSlider = new Slider(new Vector2(), 10);
                layerStartRenderRatioSlider.ValueChanged += new EventHandler(layerStartRenderRatioSlider_ValueChanged);
                AddChild(layerStartRenderRatioSlider);

                AddChild(new TextWidget(LocalizedString.Get("end:"), 50, 57, 10, Agg.Font.Justification.Right));
                layerEndRenderRatioSlider = new Slider(new Vector2(), 10);
                layerEndRenderRatioSlider.Value = 1;
                layerEndRenderRatioSlider.ValueChanged += new EventHandler(layerEndRenderRatioSlider_ValueChanged);
                AddChild(layerEndRenderRatioSlider);

                SetSliderSizes();

                // let's change the active layer so that it is set to the first layer with data
                gcodeViewWidget.ActiveLayerIndex = gcodeViewWidget.ActiveLayerIndex + 1;
                gcodeViewWidget.ActiveLayerIndex = gcodeViewWidget.ActiveLayerIndex - 1;

                BoundsChanged += new EventHandler(PartPreviewGCode_BoundsChanged);
            }
        }

        void layerStartRenderRatioSlider_ValueChanged(object sender, EventArgs e)
        {
            if (layerEndRenderRatioSlider.Value < layerStartRenderRatioSlider.Value)
            {
                layerEndRenderRatioSlider.Value = layerStartRenderRatioSlider.Value;
            }

            gcodeViewWidget.FeatureToStartOnRatio0To1 = layerStartRenderRatioSlider.Value;
            gcodeViewWidget.FeatureToEndOnRatio0To1 = layerEndRenderRatioSlider.Value;
            gcodeViewWidget.Invalidate();
        }

        void layerEndRenderRatioSlider_ValueChanged(object sender, EventArgs e)
        {
            if (layerStartRenderRatioSlider.Value > layerEndRenderRatioSlider.Value)
            {
                layerStartRenderRatioSlider.Value = layerEndRenderRatioSlider.Value;
            }

            gcodeViewWidget.FeatureToStartOnRatio0To1 = layerStartRenderRatioSlider.Value;
            gcodeViewWidget.FeatureToEndOnRatio0To1 = layerEndRenderRatioSlider.Value;
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
            selectLayerSlider.OriginRelativeParent = new Vector2(gcodeDispalyWidget.Width - 20, 100);
            selectLayerSlider.TotalWidthInPixels = gcodeDispalyWidget.Height - 80;

            layerStartRenderRatioSlider.OriginRelativeParent = new Vector2(60, 80);
            layerStartRenderRatioSlider.TotalWidthInPixels = gcodeDispalyWidget.Width - 100;

            layerEndRenderRatioSlider.OriginRelativeParent = new Vector2(60, 60);
            layerEndRenderRatioSlider.TotalWidthInPixels = gcodeDispalyWidget.Width - 100;
        }

        private void AddHandlers()
        {
            expandModelOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandModelOptions_CheckedStateChanged);
            expandDisplayOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandDisplayOptions_CheckedStateChanged);
        }

        void expandModelOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            modelOptionsContainer.Visible = expandModelOptions.Checked;
        }

        void expandDisplayOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            displayOptionsContainer.Visible = expandDisplayOptions.Checked;
        }

        public override void OnClosed(EventArgs e)
        {
            GuiWidget parent = Parent;
            while (parent as SystemWindow == null)
            {
                parent = parent.Parent;
            }
            parent.KeyDown -= Parent_KeyDown;

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

        void generateButton_Click(object sender, MouseEventArgs mouseEvent)
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
        GCodeViewWidget gcodeViewWidget;
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();        

        public SetLayerWidget(GCodeViewWidget gcodeViewWidget)
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
            setLayerButton.Click += new Button.ButtonEventHandler(layerCountTextWidget_EditComplete);
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

        void layerCountTextWidget_EditComplete(object sender, MouseEventArgs e)
        {
            gcodeViewWidget.ActiveLayerIndex = ((int)editCurrentLayerIndex.Value - 1);
        }
    }

    public class LayerNavigationWidget : FlowLayoutWidget
    {
        Button prevLayerButton;
        Button nextLayerButton;        
        TextWidget layerCountTextWidget;        
        GCodeViewWidget gcodeViewWidget;
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public LayerNavigationWidget(GCodeViewWidget gcodeViewWidget)
            :base(FlowDirection.LeftToRight)
        {
            this.gcodeViewWidget = gcodeViewWidget;

            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            
            prevLayerButton = textImageButtonFactory.Generate("<<");
            prevLayerButton.Click += new Button.ButtonEventHandler(prevLayer_ButtonClick);
            this.AddChild(prevLayerButton);            

            layerCountTextWidget = new TextWidget("/1____", 12);
            layerCountTextWidget.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            layerCountTextWidget.VAnchor = VAnchor.ParentCenter;
            layerCountTextWidget.AutoExpandBoundsToText = true;
            layerCountTextWidget.Margin = new BorderDouble(5, 0);
            this.AddChild(layerCountTextWidget);

            nextLayerButton = textImageButtonFactory.Generate(">>");
            nextLayerButton.Click += new Button.ButtonEventHandler(nextLayer_ButtonClick);
            this.AddChild(nextLayerButton);            
        }

        void nextLayer_ButtonClick(object sender, MouseEventArgs mouseEvent)
        {
            gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex + 1);
        }

        void prevLayer_ButtonClick(object sender, MouseEventArgs mouseEvent)
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
