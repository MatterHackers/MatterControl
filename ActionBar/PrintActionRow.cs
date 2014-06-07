using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;

namespace MatterHackers.MatterControl.ActionBar
{
    class PrintActionRow : ActionRowBase
    {
        Stopwatch timeSincePrintStarted = new Stopwatch();

		MatterHackers.MatterControl.TextImageButtonFactory textImageButtonFactory = new MatterHackers.MatterControl.TextImageButtonFactory();
        List<TooltipButton> activePrintButtons = new List<TooltipButton>();
        List<TooltipButton> allPrintButtons = new List<TooltipButton>();

        TooltipButton cancelConnectButton;

        TooltipButton addButton;
        
        TooltipButton startButton;
        TooltipButton skipButton;
        TooltipButton removeButton;

        TooltipButton pauseButton;

        TooltipButton resumeButton;
        TooltipButton cancelButton;

        TooltipButton reprintButton;
        TooltipButton doneWithCurrentPartButton;

        QueueDataView queueDataView;

        public PrintActionRow(QueueDataView queueDataView)
        {
            this.queueDataView = queueDataView;
        }

        protected override void Initialize()
        {
            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;
			textImageButtonFactory.disabledTextColor = RGBA_Bytes.LightGray;
            textImageButtonFactory.hoverTextColor = RGBA_Bytes.White;
            textImageButtonFactory.pressedTextColor = RGBA_Bytes.White;
            textImageButtonFactory.AllowThemeToAdjustImage = false;

            textImageButtonFactory.borderWidth = -1;
            textImageButtonFactory.disabledFillColor = ActiveTheme.Instance.PrimaryAccentColor;
        }

        protected override void AddChildElements()
        {
            addButton = (TooltipButton)textImageButtonFactory.GenerateTooltipButton(LocalizedString.Get("Add"), "icon_circle_plus.png");
			addButton.tooltipText = LocalizedString.Get("Add a file to be printed");
            addButton.Margin = new BorderDouble(0, 6, 6, 3);

			startButton = (TooltipButton)textImageButtonFactory.GenerateTooltipButton(LocalizedString.Get("Start"), "icon_play_32x32.png");
			startButton.tooltipText = LocalizedString.Get("Begin printing the selected item.");
            startButton.Margin = new BorderDouble(0, 6, 6, 3);

			string skipButtonText = LocalizedString.Get("Skip");
			string skipButtonMessage = LocalizedString.Get("Skip the current item and move to the next in queue");
			skipButton = makeButton(skipButtonText, skipButtonMessage);

			string removeButtonText = LocalizedString.Get("Remove");
			string removeButtonMessage = LocalizedString.Get("Remove current item from queue");
			removeButton = makeButton(removeButtonText, removeButtonMessage);

			string pauseButtonText = LocalizedString.Get("Pause");
			string pauseButtonMessage = LocalizedString.Get("Pause the current print");
			pauseButton = makeButton(pauseButtonText, pauseButtonMessage);

			string cancelCancelButtonText = LocalizedString.Get("Cancel Connect");
            string cancelConnectButtonMessage = LocalizedString.Get("Stop trying to connect to the printer.");
            cancelConnectButton = makeButton(cancelCancelButtonText, cancelConnectButtonMessage);

			string cancelButtonText = LocalizedString.Get("Cancel");
			string cancelButtonMessage = LocalizedString.Get("Stop the current print");
			cancelButton = makeButton(cancelButtonText, cancelButtonMessage);

			string resumeButtonText = LocalizedString.Get("Resume");
			string resumeButtonMessage = LocalizedString.Get ("Resume the current print");
			resumeButton = makeButton(resumeButtonText, resumeButtonMessage);

			string reprintButtonText = LocalizedString.Get("Reprint");
			string reprintButtonMessage = LocalizedString.Get ("Print current item again");
			reprintButton = makeButton(reprintButtonText, reprintButtonMessage);

			string doneCurrentPartButtonText = LocalizedString.Get ("Done");
			string doenCurrentPartButtonMessage = LocalizedString.Get ("Move to next print in queue");
			doneWithCurrentPartButton = makeButton(doneCurrentPartButtonText, doenCurrentPartButtonMessage);

            this.AddChild(addButton);
            allPrintButtons.Add(addButton);

            this.AddChild(startButton);
            allPrintButtons.Add(startButton);

            this.AddChild(pauseButton);
            allPrintButtons.Add(pauseButton);

            this.AddChild(resumeButton);
            allPrintButtons.Add(resumeButton);

            this.AddChild(doneWithCurrentPartButton);
            allPrintButtons.Add(doneWithCurrentPartButton);

            this.AddChild(skipButton);
            allPrintButtons.Add(skipButton);

            this.AddChild(cancelButton);
            allPrintButtons.Add(cancelButton);

            this.AddChild(cancelConnectButton);
            allPrintButtons.Add(cancelConnectButton);

            this.AddChild(reprintButton);
            allPrintButtons.Add(reprintButton);

            this.AddChild(removeButton);
            allPrintButtons.Add(removeButton);

            SetButtonStates();
        }

        event EventHandler unregisterEvents;
        protected override void AddHandlers()
        {
            PrinterCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
            PrinterCommunication.Instance.CommunicationStateChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
            addButton.Click += new ButtonBase.ButtonEventHandler(onAddButton_Click);
            startButton.Click += new ButtonBase.ButtonEventHandler(onStartButton_Click);
            skipButton.Click += new ButtonBase.ButtonEventHandler(onSkipButton_Click);
            removeButton.Click += new ButtonBase.ButtonEventHandler(onRemoveButton_Click);
            resumeButton.Click += new ButtonBase.ButtonEventHandler(onResumeButton_Click);
            pauseButton.Click += new ButtonBase.ButtonEventHandler(onPauseButton_Click);

			cancelButton.Click += (sender, e) => { UiThread.RunOnIdle(CancelButton_Click); };
            cancelConnectButton.Click += (sender, e) => { UiThread.RunOnIdle(CancelConnectionButton_Click); };            
            reprintButton.Click += new ButtonBase.ButtonEventHandler(onReprintButton_Click);
            doneWithCurrentPartButton.Click += new ButtonBase.ButtonEventHandler(onDoneWithCurrentPartButton_Click);
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
        }
			
        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        private void onThemeChanged(object sender, EventArgs e)
        {            
            this.Invalidate();
        }

        void onAddButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(AddButtonOnIdle);
        }

        void AddButtonOnIdle(object state)
        {
            string selectInstruction = "Select an STL file".Localize();
            OpenFileDialogParams openParams = new OpenFileDialogParams("{0}|*.stl".FormatWith(selectInstruction), multiSelect: true);

            FileDialog.OpenFileDialog(ref openParams);
            if (openParams.FileNames != null)
            {
                foreach (string loadedFileName in openParams.FileNames)
                {
                    QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(loadedFileName), Path.GetFullPath(loadedFileName))));
                }
            }
        }

        void partToPrint_SliceDone(object sender, EventArgs e)
        {
            PrintItemWrapper partToPrint = sender as PrintItemWrapper;
            if (partToPrint != null)
            {
                partToPrint.SlicingDone.UnregisterEvent(partToPrint_SliceDone, ref unregisterEvents);
                string gcodePathAndFileName = partToPrint.GetGCodePathAndFileName();
                if (gcodePathAndFileName != "")
                {
                    bool originalIsGCode = Path.GetExtension(partToPrint.FileLocation).ToUpper() == ".GCODE";
                    if (File.Exists(gcodePathAndFileName)
                        && (originalIsGCode || File.ReadAllText(gcodePathAndFileName).Contains("filament used")))
                    {
                        string gcodeFileContents = "";
                        using (FileStream fileStream = new FileStream(gcodePathAndFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (StreamReader gcodeStreamReader = new StreamReader(fileStream))
                            {
                                gcodeFileContents = gcodeStreamReader.ReadToEnd();
                            }
                        }

                        timeSincePrintStarted.Restart();
                        PrinterCommunication.Instance.StartPrint(gcodeFileContents);
                    }
                    else
                    {
                        PrinterCommunication.Instance.CommunicationState = PrinterCommunication.CommunicationStates.Connected;
                    }
                }
            }
        }

        string doNotShowAgainMessage = "Do not show this again".Localize();
        string gcodeWarningMessage = "The file you are attempting to print is a GCode file.\n\nGCode files tell your printer exactly what to do.  They are not modified by SliceSettings and my not be appropriate for your specific printer configuration.\n\nOnly print from GCode files if you know they mach your current printer and configuration.\n\nAre you sure you want to print this GCode file?".Localize();
        string removeFromQueueMessage = "Cannot find\n'{0}'.\nWould you like to remove it from the queue?".Localize();
        string itemNotFoundMessage = "Item not found".Localize();
        void PrintActivePart()
        {
            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
            if (levelingData.needsPrintLeveling
                && levelingData.sampledPosition0.z == 0
                && levelingData.sampledPosition1.z == 0
                && levelingData.sampledPosition2.z == 0)
            {
                LevelWizardBase.CreateAndShowWizard(LevelWizardBase.RuningState.InitialStartupCalibration);
                return;
            }

            // else print as normal
            if (ActiveSliceSettings.Instance.IsValid())
            {
                string pathAndFile = PrinterCommunication.Instance.ActivePrintItem.FileLocation;
                if (File.Exists(pathAndFile))
                {
                    string hideGCodeWarning = ApplicationSettings.Instance.get("HideGCodeWarning");

                    if (Path.GetExtension(pathAndFile).ToUpper() == ".GCODE" && hideGCodeWarning == null)
                    {
                        CheckBox hideGCodeWaringCheckBox = new CheckBox(doNotShowAgainMessage);
                        hideGCodeWaringCheckBox.HAnchor = Agg.UI.HAnchor.ParentCenter;
                        hideGCodeWaringCheckBox.TextColor = RGBA_Bytes.White;
                        hideGCodeWaringCheckBox.Click += (sender, e) =>
                        {
                            if (hideGCodeWaringCheckBox.Checked)
                            {
                                ApplicationSettings.Instance.set("HideGCodeWarning", "true");
                            }
                            else
                            {
                                ApplicationSettings.Instance.set("HideGCodeWarning", null);
                            }
                        };
                        if (!StyledMessageBox.ShowMessageBox(gcodeWarningMessage, "Warning GCode file".Localize(), new GuiWidget[] { hideGCodeWaringCheckBox }, StyledMessageBox.MessageType.YES_NO))
                        {
                            // the user selected 'no' they don't want to print the file
                            return;
                        }
                    }

                    PrinterCommunication.Instance.CommunicationState = PrinterCommunication.CommunicationStates.PreparingToPrint;
                    PrintItemWrapper partToPrint = PrinterCommunication.Instance.ActivePrintItem;
                    SlicingQueue.Instance.QueuePartForSlicing(partToPrint);
                    partToPrint.SlicingDone.RegisterEvent(partToPrint_SliceDone, ref unregisterEvents);

                }
                else
                {
                    string message = String.Format(removeFromQueueMessage, pathAndFile);
                    if (StyledMessageBox.ShowMessageBox(message, itemNotFoundMessage, StyledMessageBox.MessageType.YES_NO))
                    {
                        QueueData.Instance.RemoveAt(queueDataView.SelectedIndex);
                    }
                }
            }
        }

        void onStartButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrintActivePart();
        }

        void onSkipButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            if (QueueData.Instance.Count > 1)
            {
                queueDataView.MoveToNext();
            }
        }

        void onResumeButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            if (PrinterCommunication.Instance.PrinterIsPaused)
            {
                PrinterCommunication.Instance.Resume();
            }
        }

        void onRemoveButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            QueueData.Instance.RemoveAt(queueDataView.SelectedIndex);
        }

        void onPauseButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.RequestPause();
        }
			
        void CancelButton_Click(object state)
        {
            if (timeSincePrintStarted.IsRunning && timeSincePrintStarted.ElapsedMilliseconds > (2 * 60 * 1000))
            {
                if (StyledMessageBox.ShowMessageBox("Cancel the current print?", "Cancel Print?", StyledMessageBox.MessageType.YES_NO))
				{	
                    CancelPrinting();
                }
            }
            else
            {
                CancelPrinting();
            }
        }

        void CancelConnectionButton_Click(object state)
        {
            CancelPrinting();
        }

        private void CancelPrinting()
        {
            if (PrinterCommunication.Instance.CommunicationState == PrinterCommunication.CommunicationStates.PreparingToPrint)
            {
                SlicingQueue.Instance.CancelCurrentSlicing();
            }
            PrinterCommunication.Instance.Stop();
            timeSincePrintStarted.Reset();
        }

        void onDoneWithCurrentPartButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.ResetToReadyState();
            QueueData.Instance.RemoveAt(queueDataView.SelectedIndex);
            // We don't have to change the selected index because we should be on the next one as we deleted the one 
            // we were on.
        }

        void onReprintButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrintActivePart();
        }

        private void onStateChanged(object sender, EventArgs e)
        {
            SetButtonStates();
        }

        protected TooltipButton makeButton(string buttonText, string buttonToolTip = "")
        {
            TooltipButton button = (TooltipButton)textImageButtonFactory.GenerateTooltipButton(buttonText);
            button.tooltipText = buttonToolTip;
            button.Margin = new BorderDouble(0, 6, 6, 3);
            return button;
        }

        protected void EnableActiveButtons()
        {
            foreach (TooltipButton button in this.activePrintButtons)
            {
                button.Enabled = true;
            }
				
        }

        protected void ShowActiveButtons()
        {
            foreach (TooltipButton button in this.allPrintButtons)
            {
                if (activePrintButtons.IndexOf(button) >= 0)
                {
                    button.Visible = true;
                }
                else
                {
                    button.Visible = false;
                }
            }
        }

        protected void DisableActiveButtons()
        {
            foreach (TooltipButton button in this.activePrintButtons)
            {
                button.Enabled = false;
            }
        }

        //Set the states of the buttons based on the status of PrinterCommunication
        protected void SetButtonStates()
        {
            this.activePrintButtons.Clear();
            if (PrinterCommunication.Instance.ActivePrintItem == null)
            {
                this.activePrintButtons.Add(addButton);
                ShowActiveButtons();
                EnableActiveButtons();
            }
            else
            {
                switch (PrinterCommunication.Instance.CommunicationState)
                {
                    case PrinterCommunication.CommunicationStates.AttemptingToConnect:
                        this.activePrintButtons.Add(cancelConnectButton);
                        EnableActiveButtons();
                        break;

                    case PrinterCommunication.CommunicationStates.Connected:
                        this.activePrintButtons.Add(startButton);

                        //Show 'skip' button if there are more items in queue
                        if (QueueData.Instance.Count > 1)
                        {
                            this.activePrintButtons.Add(skipButton);
                        }                        

                        this.activePrintButtons.Add(removeButton);
                        EnableActiveButtons();
                        break;

                    case PrinterCommunication.CommunicationStates.PreparingToPrint:
                        this.activePrintButtons.Add(cancelButton);
                        EnableActiveButtons();
                        break;

				case PrinterCommunication.CommunicationStates.Printing:
					 this.activePrintButtons.Add (pauseButton);
					 this.activePrintButtons.Add (cancelButton);
					 EnableActiveButtons ();
                     break;

                    case PrinterCommunication.CommunicationStates.Paused:
                        this.activePrintButtons.Add(resumeButton);
                        this.activePrintButtons.Add(cancelButton);
                        EnableActiveButtons();
                        break;

                    case PrinterCommunication.CommunicationStates.FinishedPrint:
                        this.activePrintButtons.Add(reprintButton);
                        this.activePrintButtons.Add(doneWithCurrentPartButton);
                        EnableActiveButtons();
                        break;

                    default:                        
                        DisableActiveButtons();
                        break;
                }
            }
            ShowActiveButtons();
            
        }
    }
}
