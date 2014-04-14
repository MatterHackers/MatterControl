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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.ComponentModel;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class GcodeViewBasic : PartPreviewWidget
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
        FlowLayoutWidget layerSelectionButtonsPannel;

        FlowLayoutWidget modelOptionsContainer;
        FlowLayoutWidget layerOptionsContainer;
        FlowLayoutWidget displayOptionsContainer;

        CheckBox expandModelOptions;
        CheckBox expandLayerOptions;
        CheckBox expandDisplayOptions;

        GuiWidget gcodeDispalyWidget;

        GetSizeFunction bedSizeFunction;
        GetSizeFunction bedCenterFunction;
        bool widgetHasCloseButton;

        public delegate Vector2 GetSizeFunction();

        public GcodeViewBasic(PrintItemWrapper printItem, GetSizeFunction bedSizeFunction, GetSizeFunction bedCenterFunction, bool addCloseButton)
        {
            widgetHasCloseButton = addCloseButton;
            this.printItem = printItem;

            this.bedSizeFunction = bedSizeFunction;
            this.bedCenterFunction = bedCenterFunction;

            CreateAndAddChildren(null);
        }

        void CreateAndAddChildren(object state)
        {
            RemoveAllChildren();

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

            layerSelectionButtonsPannel = new FlowLayoutWidget(FlowDirection.RightToLeft);
            layerSelectionButtonsPannel.HAnchor = HAnchor.ParentLeftRight;
            layerSelectionButtonsPannel.Padding = new BorderDouble(0);

            GuiWidget holdPannelOpen = new GuiWidget(1, generateGCodeButton.Height);
            layerSelectionButtonsPannel.AddChild(holdPannelOpen);

            if (widgetHasCloseButton)
            {
                Button closeButton = textImageButtonFactory.Generate(LocalizedString.Get("Close"));
                layerSelectionButtonsPannel.AddChild(closeButton);
                closeButton.Click += (sender, e) =>
                {
                    CloseOnIdle();
                };
            }

            FlowLayoutWidget centerPartPreviewAndControls = new FlowLayoutWidget(FlowDirection.LeftToRight);
            centerPartPreviewAndControls.AnchorAll();

            gcodeDispalyWidget = new GuiWidget(HAnchor.ParentLeftRight, Agg.UI.VAnchor.ParentBottomTop);

            string startingMessage = "";
            if (printItem != null)
            {
                startingMessage = LocalizedString.Get("No GCode Available...");
                startingMessage = LocalizedString.Get("Loading GCode...");
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

                        if (gcodeProcessingStateInfoText != null && gcodeProcessingStateInfoText.Text == "Slicing Error")
                        {
                            startingMessage = LocalizedString.Get("Slicing Error. Please review your slice settings.");
                        }
                        else
                        {
                            startingMessage = LocalizedString.Get("Press 'generate' to view layers");
                        }

                        if (File.Exists(gcodePathAndFileName) && gcodeFileIsComplete)
                        {
                            gcodeDispalyWidget.AddChild(CreateGCodeViewWidget(gcodePathAndFileName));
                        }

                        // we only hook these up to make sure we can regenerate the gcode when we want
                        printItem.SlicingOutputMessage += sliceItem_SlicingOutputMessage;
                        printItem.Done += new EventHandler(sliceItem_Done);
                    }
                    else
                    {
                        startingMessage = string.Format("{0}\n'{1}'", LocalizedString.Get("File not found on disk."), printItem.Name);
                    }
                }
            }

            centerPartPreviewAndControls.AddChild(gcodeDispalyWidget);

            buttonRightPanel = CreateRightButtonPannel();
            centerPartPreviewAndControls.AddChild(buttonRightPanel);

            // add in a spacer
            layerSelectionButtonsPannel.AddChild(new GuiWidget(HAnchor.ParentLeftRight));
            buttonBottomPanel.AddChild(layerSelectionButtonsPannel);

            mainContainerTopToBottom.AddChild(centerPartPreviewAndControls);
            mainContainerTopToBottom.AddChild(buttonBottomPanel);
            this.AddChild(mainContainerTopToBottom);

            AddProcessingMessage(startingMessage);

            Add2DViewControls();
            translateButton.Click += (sender, e) =>
            {
                gcodeViewWidget.TransformState = GCodeViewWidget.ETransformState.Move;
            };
            scaleButton.Click += (sender, e) =>
            {
                gcodeViewWidget.TransformState = GCodeViewWidget.ETransformState.Scale;
            };

            AddHandlers();
        }

        private FlowLayoutWidget CreateRightButtonPannel()
        {
            FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
            buttonRightPanel.Width = 200;
            {
                BorderDouble buttonMargin = new BorderDouble(top: 3);

                string label = "MODEL".Localize().ToUpper();
				expandModelOptions = expandMenuOptionFactory.GenerateCheckBoxButton(label, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandModelOptions.Margin = new BorderDouble(bottom: 2);
                buttonRightPanel.AddChild(expandModelOptions);
                expandModelOptions.Checked = true;

                modelOptionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                modelOptionsContainer.HAnchor = HAnchor.ParentLeftRight;
                //modelOptionsContainer.Visible = false;
                buttonRightPanel.AddChild(modelOptionsContainer);

				expandLayerOptions = expandMenuOptionFactory.GenerateCheckBoxButton("Layer".Localize().ToUpper(), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandLayerOptions.Margin = new BorderDouble(bottom: 2);
                //buttonRightPanel.AddChild(expandLayerOptions);

                layerOptionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                layerOptionsContainer.HAnchor = HAnchor.ParentLeftRight;
                layerOptionsContainer.Visible = false;
                buttonRightPanel.AddChild(layerOptionsContainer);

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
            //AddLayerInfo(layerOptionsContainer);
            AddDisplayControls(displayOptionsContainer);
        }

        private void AddModelInfo(FlowLayoutWidget buttonPanel)
        {
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
                    int secondsRemaining = (int)gcodeViewWidget.LoadedGCode.GCodeCommandQueue[0].secondsToEndFromHere;
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
            
			string filamentLengthLbl = "Filament Length".Localize().ToUpper();
			string filamentLengthLblFull = string.Format ("{0}:", filamentLengthLbl);
            // show the filament used
            modelInfoContainer.AddChild(new TextWidget(filamentLengthLblFull, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
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

            textImageButtonFactory.FixedWidth = 0;
        }

        private void AddLayerInfo(FlowLayoutWidget buttonPanel)
        {
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

            textImageButtonFactory.FixedWidth = 0;
        }

        private void AddDisplayControls(FlowLayoutWidget buttonPanel)
        {
            textImageButtonFactory.FixedWidth = 44;

            FlowLayoutWidget layerInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            layerInfoContainer.HAnchor = HAnchor.ParentLeftRight;
            layerInfoContainer.Padding = new BorderDouble(5);

            // put in a show grid check box
            CheckBox showGrid = new CheckBox(LocalizedString.Get("Show Grid"), textColor: ActiveTheme.Instance.PrimaryTextColor);
            showGrid.Checked = gcodeViewWidget.RenderGrid;
            showGrid.CheckedStateChanged += (sender, e) =>
            {
                gcodeViewWidget.RenderGrid = showGrid.Checked;
            };
            layerInfoContainer.AddChild(showGrid);

            // put in a show moves checkbox
            CheckBox showMoves = new CheckBox(LocalizedString.Get("Show Moves"), textColor: ActiveTheme.Instance.PrimaryTextColor);
            showMoves.Checked = gcodeViewWidget.RenderMoves;
            showMoves.CheckedStateChanged += (sender, e) =>
            {
                gcodeViewWidget.RenderMoves = showMoves.Checked;
            };
            layerInfoContainer.AddChild(showMoves);

            // put in a show Retractions checkbox
            CheckBox showRetractions = new CheckBox(LocalizedString.Get("Show Retractions"), textColor: ActiveTheme.Instance.PrimaryTextColor);
            showRetractions.Checked = gcodeViewWidget.RenderRetractions;
            showRetractions.CheckedStateChanged += (sender, e) =>
            {
                gcodeViewWidget.RenderRetractions = showRetractions.Checked;
            };
            layerInfoContainer.AddChild(showRetractions);

            //layerInfoContainer.AddChild(new CheckBox("Show Retractions", textColor: ActiveTheme.Instance.PrimaryTextColor));

            buttonPanel.AddChild(layerInfoContainer);

            textImageButtonFactory.FixedWidth = 0;
        }

        public override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
        }

        string partToStartLoadingOnFirstDraw = null;
        private GuiWidget CreateGCodeViewWidget(string pathAndFileName)
        {
            gcodeViewWidget = new GCodeViewWidget(bedSizeFunction(), bedCenterFunction());
            gcodeViewWidget.DoneLoading += DoneLoadingGCode;
            gcodeViewWidget.LoadingProgressChanged += LoadingProgressChanged;
            partToStartLoadingOnFirstDraw = pathAndFileName;

            return gcodeViewWidget;
        }

        bool hookedParentKeyDown = false;
        public override void OnDraw(Graphics2D graphics2D)
        {
            if (!hookedParentKeyDown)
            {
                Parent.Parent.Parent.KeyDown += new KeyEventHandler(Parent_KeyDown);
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

        private void AddProcessingMessage(string message)
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

        void LoadingProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            gcodeProcessingStateInfoText.Text = string.Format("Loading GCode {0}%...", e.ProgressPercentage);
        }

        void DoneLoadingGCode(object sender, EventArgs e)
        {
            gcodeProcessingStateInfoText.Text = "";
            if (gcodeViewWidget != null
                && gcodeViewWidget.LoadedGCode != null
                && gcodeViewWidget.LoadedGCode.GCodeCommandQueue.Count > 0
                && gcodeViewWidget.LoadedGCode.GCodeCommandQueue[0].secondsToEndFromHere > 0)
            {
                CreateOptionsContent();

                SetLayerWidget setLayerWidget = new SetLayerWidget(gcodeViewWidget);
                setLayerWidget.VAnchor = Agg.UI.VAnchor.ParentTop;
                layerSelectionButtonsPannel.AddChild(setLayerWidget);

                LayerNavigationWidget navigationWidget = new LayerNavigationWidget(gcodeViewWidget);
                navigationWidget.Margin = new BorderDouble(0, 0, 20, 0);
                layerSelectionButtonsPannel.AddChild(navigationWidget);

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
            expandLayerOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandScaleOptions_CheckedStateChanged);
            expandDisplayOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandMirrorOptions_CheckedStateChanged);
        }

        void expandModelOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            modelOptionsContainer.Visible = expandModelOptions.Checked;
        }

        void expandMirrorOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            displayOptionsContainer.Visible = expandDisplayOptions.Checked;
        }

        void expandScaleOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            layerOptionsContainer.Visible = expandLayerOptions.Checked;
        }

        public override void OnClosed(EventArgs e)
        {
            if (printItem != null)
            {
                printItem.SlicingOutputMessage -= sliceItem_SlicingOutputMessage;
                printItem.Done -= new EventHandler(sliceItem_Done);
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
            if (ActiveSliceSettings.Instance.IsValid())
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
                gcodeProcessingStateInfoText.Text = message.Data;
            }
            else
            {
                gcodeProcessingStateInfoText.Text = "";
            }
        }

        void sliceItem_Done(object sender, EventArgs e)
        {
            // We can add this while we have it open (when we are done loading).
            // So we need to make sure we only have it added once. This will be ok to run when
            // not added or when added and will ensure we only have one hook.
            printItem.SlicingOutputMessage -= sliceItem_SlicingOutputMessage;
            printItem.Done -= sliceItem_Done;

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
