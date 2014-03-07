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
    public class GcodeViewBasic : PartPreviewBaseWidget
    {
        public Slider selectLayerSlider;
        TextWidget gcodeProcessingStateInfoText;
        GCodeViewWidget gcodeViewWidget;
        PrintItemWrapper printItem;
        Button closeButton;
        bool startedSliceFromGenerateButton = false;
        Button generateGCodeButton;
        FlowLayoutWidget buttonBottomPanel;
        FlowLayoutWidget layerSelectionButtonsPannel;
        FlowLayoutWidget buttonRightPanel;

        FlowLayoutWidget modelOptionsContainer;
        FlowLayoutWidget layerOptionsContainer;
        FlowLayoutWidget displayOptionsContainer;

        CheckBox expandModelOptions;
        CheckBox expandLayerOptions;
        CheckBox expandDisplayOptions;

        GuiWidget gcodeDispalyWidget;

        GetSizeFunction bedSizeFunction;
        GetSizeFunction bedCenterFunction;

        public delegate Vector2 GetSizeFunction();

        public GcodeViewBasic(PrintItemWrapper printItem, GetSizeFunction bedSizeFunction, GetSizeFunction bedCenterFunction)
        {
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

			generateGCodeButton = textImageButtonFactory.Generate(new LocalizedString("Generate").Translated);
            generateGCodeButton.Click += new ButtonBase.ButtonEventHandler(generateButton_Click);
            buttonBottomPanel.AddChild(generateGCodeButton);

            layerSelectionButtonsPannel = new FlowLayoutWidget(FlowDirection.RightToLeft);
            layerSelectionButtonsPannel.HAnchor = HAnchor.ParentLeftRight;
            layerSelectionButtonsPannel.Padding = new BorderDouble(0);

			closeButton = textImageButtonFactory.Generate(new LocalizedString("Close").Translated);

            layerSelectionButtonsPannel.AddChild(closeButton);

            FlowLayoutWidget centerPartPreviewAndControls = new FlowLayoutWidget(FlowDirection.LeftToRight);
            centerPartPreviewAndControls.AnchorAll();

            gcodeDispalyWidget = new GuiWidget(HAnchor.ParentLeftRight, Agg.UI.VAnchor.ParentBottomTop);

            string startingMessage = new LocalizedString("No GCode Available...").Translated;
            if (printItem != null)
            {
                startingMessage = new LocalizedString("Loading GCode...").Translated;
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
                            startingMessage = new LocalizedString("Slicing Error. Please review your slice settings.").Translated;
                        }
                        else
                        {
                            startingMessage = new LocalizedString("Press 'generate' to view layers").Translated;
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
                        startingMessage = string.Format("{0}\n'{1}'", new LocalizedString("File not found on disk.").Translated, printItem.Name);
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

            AddViewControls();

            AddHandlers();
        }

        void AddViewControls()
        {
            TextImageButtonFactory iconTextImageButtonFactory = new TextImageButtonFactory();

            FlowLayoutWidget transformTypeSelector = new FlowLayoutWidget();
            transformTypeSelector.BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);
            iconTextImageButtonFactory.FixedHeight = 20;
            iconTextImageButtonFactory.FixedWidth = 20;

            string translateIconPath = Path.Combine("Icons", "ViewTransformControls", "translate.png");
            RadioButton translateButton = iconTextImageButtonFactory.GenerateRadioButton("", translateIconPath);
            translateButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(translateButton);
            translateButton.Click += (sender, e) =>
            {
                gcodeViewWidget.TransformState = GCodeViewWidget.ETransformState.Move;
            };

            string scaleIconPath = Path.Combine("Icons", "ViewTransformControls", "scale.png");
            RadioButton scaleButton = iconTextImageButtonFactory.GenerateRadioButton("", scaleIconPath);
            scaleButton.Margin = new BorderDouble(3);
            transformTypeSelector.AddChild(scaleButton);
            scaleButton.Click += (sender, e) =>
            {
                gcodeViewWidget.TransformState = GCodeViewWidget.ETransformState.Scale;
            };

            transformTypeSelector.Margin = new BorderDouble(5);
            transformTypeSelector.HAnchor |= Agg.UI.HAnchor.ParentLeft;
            transformTypeSelector.VAnchor = Agg.UI.VAnchor.ParentTop;
            AddChild(transformTypeSelector);
            translateButton.Checked = true;
        }

        private FlowLayoutWidget CreateRightButtonPannel()
        {
            FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
            buttonRightPanel.Width = 200;
            {
                BorderDouble buttonMargin = new BorderDouble(top: 3);

				expandModelOptions = expandMenuOptionFactory.GenerateCheckBoxButton(new LocalizedString("Model").Translated, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandModelOptions.Margin = new BorderDouble(bottom: 2);
                buttonRightPanel.AddChild(expandModelOptions);
                expandModelOptions.Checked = true;

                modelOptionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                modelOptionsContainer.HAnchor = HAnchor.ParentLeftRight;
                //modelOptionsContainer.Visible = false;
                buttonRightPanel.AddChild(modelOptionsContainer);

				expandLayerOptions = expandMenuOptionFactory.GenerateCheckBoxButton(new LocalizedString("Layer").Translated, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandLayerOptions.Margin = new BorderDouble(bottom: 2);
                //buttonRightPanel.AddChild(expandLayerOptions);

                layerOptionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                layerOptionsContainer.HAnchor = HAnchor.ParentLeftRight;
                layerOptionsContainer.Visible = false;
                buttonRightPanel.AddChild(layerOptionsContainer);

				expandDisplayOptions = expandMenuOptionFactory.GenerateCheckBoxButton(new LocalizedString("Display").Translated, "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
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

			string printTimeLbl = new LocalizedString ("Print Time").Translated;
			string printTimeLblFull = string.Format ("{0}:", printTimeLbl);
            // put in the print time
			modelInfoContainer.AddChild(new TextWidget(printTimeLblFull, textColor: RGBA_Bytes.White));
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

                GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0}", timeRemainingText), textColor: RGBA_Bytes.White, pointSize: 10);
                estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
                estimatedPrintTime.Margin = new BorderDouble(3, 0, 0, 3);
                modelInfoContainer.AddChild(estimatedPrintTime);
            }

            //modelInfoContainer.AddChild(new TextWidget("Size:", textColor: RGBA_Bytes.White));
            
			string filamentLengthLbl = new LocalizedString ("Filament Length").Translated;
			string filamentLengthLblFull = string.Format ("{0}:", filamentLengthLbl);
            // show the filament used
			modelInfoContainer.AddChild(new TextWidget(filamentLengthLblFull, textColor: RGBA_Bytes.White));
            {
                double filamentUsed = gcodeViewWidget.LoadedGCode.GetFilamentUsedMm(ActiveSliceSettings.Instance.NozzleDiameter);

                GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.0} mm", filamentUsed), textColor: RGBA_Bytes.White, pointSize: 10);
                estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
                estimatedPrintTime.Margin = new BorderDouble(3, 0, 0, 3);
                modelInfoContainer.AddChild(estimatedPrintTime);
            }

			string filamentVolumeLbl = new LocalizedString ("Filament Volume").Translated;
			string filamentVolumeLblFull = string.Format("{0}:", filamentVolumeLbl);
			modelInfoContainer.AddChild(new TextWidget(filamentVolumeLblFull, textColor: RGBA_Bytes.White));
            {
                var density = 1.0;
                string filamentType = "PLA";
                if(filamentType == "ABS")
                {
                    density = 1.04;
                }
                else if(filamentType == "PLA") 
                {
                    density = 1.24;
                }
                
                double filamentMm3 = gcodeViewWidget.LoadedGCode.GetFilamentCubicMm(ActiveSliceSettings.Instance.FillamentDiameter);

                GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.00} cm3", filamentMm3/1000), textColor: RGBA_Bytes.White, pointSize: 10);
                estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
                estimatedPrintTime.Margin = new BorderDouble(3, 0, 0, 3);
                modelInfoContainer.AddChild(estimatedPrintTime);
            }

			string weightLbl = new LocalizedString("Weight").Translated;
			string weightLblFull = string.Format("{0}:", weightLbl);
			modelInfoContainer.AddChild(new TextWidget(weightLblFull, textColor: RGBA_Bytes.White));
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

                double filamentWeightGrams = gcodeViewWidget.LoadedGCode.GetFilamentWeightGrams(ActiveSliceSettings.Instance.FillamentDiameter, density);

                GuiWidget estimatedPrintTime = new TextWidget(string.Format("{0:0.00} g", filamentWeightGrams), textColor: RGBA_Bytes.White, pointSize: 10);
                estimatedPrintTime.HAnchor = Agg.UI.HAnchor.ParentLeft;
                estimatedPrintTime.Margin = new BorderDouble(3, 0, 0, 3);
                modelInfoContainer.AddChild(estimatedPrintTime);
            }

            //modelInfoContainer.AddChild(new TextWidget("Layer Count:", textColor: RGBA_Bytes.White));

            buttonPanel.AddChild(modelInfoContainer);

            textImageButtonFactory.FixedWidth = 0;
        }

        private void AddLayerInfo(FlowLayoutWidget buttonPanel)
        {
            textImageButtonFactory.FixedWidth = 44;

            FlowLayoutWidget layerInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            layerInfoContainer.HAnchor = HAnchor.ParentLeftRight;
            layerInfoContainer.Padding = new BorderDouble(5);

			layerInfoContainer.AddChild(new TextWidget("Layer Number:", textColor: RGBA_Bytes.White));
            layerInfoContainer.AddChild(new TextWidget("Layer Height:", textColor: RGBA_Bytes.White));
            layerInfoContainer.AddChild(new TextWidget("Num GCodes:", textColor: RGBA_Bytes.White));
            layerInfoContainer.AddChild(new TextWidget("Filament Used:", textColor: RGBA_Bytes.White));
            layerInfoContainer.AddChild(new TextWidget("Weight:", textColor: RGBA_Bytes.White));
            layerInfoContainer.AddChild(new TextWidget("Print Time:", textColor: RGBA_Bytes.White));
            layerInfoContainer.AddChild(new TextWidget("Extrude Speeds:", textColor: RGBA_Bytes.White));
            layerInfoContainer.AddChild(new TextWidget("Move Speeds:", textColor: RGBA_Bytes.White));
            layerInfoContainer.AddChild(new TextWidget("Retract Speeds:", textColor: RGBA_Bytes.White));

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
			CheckBox showGrid = new CheckBox(new LocalizedString("Show Grid").Translated, textColor: RGBA_Bytes.White);
            showGrid.Checked = gcodeViewWidget.RenderGrid;
            showGrid.CheckedStateChanged += (sender, e) =>
            {
                gcodeViewWidget.RenderGrid = showGrid.Checked;
            };
            layerInfoContainer.AddChild(showGrid);

            // put in a show moves checkbox
			CheckBox showMoves = new CheckBox(new LocalizedString("Show Moves").Translated, textColor: RGBA_Bytes.White);
            showMoves.Checked = gcodeViewWidget.RenderMoves;
            showMoves.CheckedStateChanged += (sender, e) =>
            {
                gcodeViewWidget.RenderMoves = showMoves.Checked;
            };
            layerInfoContainer.AddChild(showMoves);

            // put in a show Retractions checkbox
            CheckBox showRetractions = new CheckBox(new LocalizedString("Show Retractions").Translated, textColor: RGBA_Bytes.White);
            showRetractions.Checked = gcodeViewWidget.RenderRetractions;
            showRetractions.CheckedStateChanged += (sender, e) =>
            {
                gcodeViewWidget.RenderRetractions = showRetractions.Checked;
            };
            layerInfoContainer.AddChild(showRetractions);

            //layerInfoContainer.AddChild(new CheckBox("Show Retractions", textColor: RGBA_Bytes.White));

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
                gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex + 1);
            }
            else if (keyEvent.KeyCode == Keys.Down)
            {
                gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex - 1);
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
                SetSliderSize();

                // let's change the active layer so that it is set to the first layer with data
                gcodeViewWidget.ActiveLayerIndex = gcodeViewWidget.ActiveLayerIndex + 1;
                gcodeViewWidget.ActiveLayerIndex = gcodeViewWidget.ActiveLayerIndex - 1;

                BoundsChanged += new EventHandler(PartPreviewGCode_BoundsChanged);
            }
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
            SetSliderSize();
        }

        void SetSliderSize()
        {
            selectLayerSlider.OriginRelativeParent = new Vector2(gcodeDispalyWidget.Width - 20, 80);
            selectLayerSlider.TotalWidthInPixels = gcodeDispalyWidget.Height - 80;
        }

        private void AddHandlers()
        {
            closeButton.Click += onCloseButton_Click;
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

        private void onCloseButton_Click(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(CloseOnIdle);
        }

        void CloseOnIdle(object state)
        {
            Close();
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
            
            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;
            textImageButtonFactory.hoverTextColor = RGBA_Bytes.White;
            textImageButtonFactory.disabledTextColor = RGBA_Bytes.White;
            textImageButtonFactory.pressedTextColor = RGBA_Bytes.White;
            
            editCurrentLayerIndex = new NumberEdit(1, pixelWidth: 40);
            editCurrentLayerIndex.VAnchor = VAnchor.ParentCenter;
            editCurrentLayerIndex.Margin = new BorderDouble(5, 0);
            editCurrentLayerIndex.EditComplete += new EventHandler(editCurrentLayerIndex_EditComplete);
            this.AddChild(editCurrentLayerIndex);
            gcodeViewWidget.ActiveLayerChanged += new EventHandler(gcodeViewWidget_ActiveLayerChanged);

			setLayerButton = textImageButtonFactory.Generate(new LocalizedString("Go").Translated);
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

            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;
            textImageButtonFactory.hoverTextColor = RGBA_Bytes.White;
            textImageButtonFactory.disabledTextColor = RGBA_Bytes.White;
            textImageButtonFactory.pressedTextColor = RGBA_Bytes.White;
            
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
