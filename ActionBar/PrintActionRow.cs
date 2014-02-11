using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading;

using MatterHackers.Agg.Image;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.ActionBar
{
    class PrintActionRow : ActionRowBase
    {
        Stopwatch timeSincePrintStarted = new Stopwatch();

		MatterHackers.MatterControl.TextImageButtonFactory textImageButtonFactory = new MatterHackers.MatterControl.TextImageButtonFactory();
        List<TooltipButton> activePrintButtons = new List<TooltipButton>();
        List<TooltipButton> allPrintButtons = new List<TooltipButton>();

        TooltipButton addButton;
        
        TooltipButton startButton;
        TooltipButton skipButton;
        TooltipButton removeButton;

        TooltipButton pauseButton;

        TooltipButton resumeButton;
        TooltipButton cancelButton;

        TooltipButton reprintButton;
        TooltipButton doneWithCurrentPartButton;

        protected override void Initialize()
        {
            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;
            textImageButtonFactory.disabledTextColor = RGBA_Bytes.LightGray;
            textImageButtonFactory.hoverTextColor = RGBA_Bytes.White;
            textImageButtonFactory.pressedTextColor = RGBA_Bytes.White;

            textImageButtonFactory.borderWidth = -1;
            textImageButtonFactory.disabledFillColor = ActiveTheme.Instance.PrimaryAccentColor;
        }

        protected override void AddChildElements()
        {         
			addButton = (TooltipButton)textImageButtonFactory.GenerateTooltipButton(new LocalizedString("Add").Translated, "icon_circle_plus.png");
			addButton.tooltipText = new LocalizedString("Add a file to be printed").Translated;
            addButton.Margin = new BorderDouble(0, 6, 6, 3);

			startButton = (TooltipButton)textImageButtonFactory.GenerateTooltipButton(new LocalizedString("Start").Translated, "icon_play_32x32.png");
			startButton.tooltipText = new LocalizedString("Begin printing the selected item.").Translated;
            startButton.Margin = new BorderDouble(0, 6, 6, 3);

			string skipButtonTxt = new LocalizedString("Skip").Translated;
			string skipButtonMessage = new LocalizedString("Skip the current item and move to the next in queue").Translated;
			skipButton = makeButton(skipButtonTxt, skipButtonMessage);

			string removeButtonTxt = new LocalizedString("Remove").Translated;
			string removeButtonMessage = new LocalizedString("Remove current item from queue").Translated;
			removeButton = makeButton(removeButtonTxt, removeButtonMessage);

			string pauseButtonTxt = new LocalizedString("Pause").Translated;
			string pauseButtonMessage = new LocalizedString("Pause the current print").Translated;
			pauseButton = makeButton(pauseButtonTxt, pauseButtonMessage);

			string cancelButtonTxt = new LocalizedString("Cancel").Translated;
			string cancelButtonMessage = new LocalizedString("Stop the current print").Translated;
			cancelButton = makeButton(cancelButtonTxt, cancelButtonMessage);

			string resumeButtonTxt = new LocalizedString("Resume").Translated;
			string resumeButtonMessage = new LocalizedString ("Resume the current print").Translated;
			resumeButton = makeButton(resumeButtonTxt, resumeButtonMessage);

			string reprintButtonTxt = new LocalizedString("Reprint").Translated;
			string reprintButtonMessage = new LocalizedString ("Print current item again").Translated;
			reprintButton = makeButton(reprintButtonTxt, reprintButtonMessage);

			string doneCurrentPartButtonTxt = new LocalizedString ("Done").Translated;
			string doenCurrentPartButtonMessage = new LocalizedString ("Move to next print in queue").Translated;
			doneWithCurrentPartButton = makeButton(doneCurrentPartButtonTxt, doenCurrentPartButtonMessage);

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
            PrinterCommunication.Instance.ConnectionStateChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
            addButton.Click += new ButtonBase.ButtonEventHandler(onAddButton_Click);
            startButton.Click += new ButtonBase.ButtonEventHandler(onStartButton_Click);
            skipButton.Click += new ButtonBase.ButtonEventHandler(onSkipButton_Click);
            removeButton.Click += new ButtonBase.ButtonEventHandler(onRemoveButton_Click);
            resumeButton.Click += new ButtonBase.ButtonEventHandler(onResumeButton_Click);

            pauseButton.Click += new ButtonBase.ButtonEventHandler(onPauseButton_Click);
            cancelButton.Click += (sender, e) => { UiThread.RunOnIdle(CancelButton_Click); };
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
            OpenFileDialogParams openParams = new OpenFileDialogParams("Select an STL file|*.stl", multiSelect: true);

            FileDialog.OpenFileDialog(ref openParams);
            if (openParams.FileNames != null)
            {
                foreach (string loadedFileName in openParams.FileNames)
                {
                    PrintQueueItem queueItem = new PrintQueueItem(System.IO.Path.GetFileNameWithoutExtension(loadedFileName), System.IO.Path.GetFullPath(loadedFileName));
                    PrintQueueControl.Instance.AddChild(queueItem);
                }
                
                PrintQueueControl.Instance.EnsureSelection();
                PrintQueueControl.Instance.Invalidate();
            }
            PrintQueueControl.Instance.SaveDefaultQueue();
        }

        void partToPrint_SliceDone(object sender, EventArgs e)
        {
            PrintItemWrapper partToPrint = sender as PrintItemWrapper;
            if (partToPrint != null)
            {
                partToPrint.Done -= new EventHandler(partToPrint_SliceDone);
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

        void PrintActivePart()
        {
            if (ActiveSliceSettings.Instance.IsValid())
            {
                string pathAndFile = PrinterCommunication.Instance.ActivePrintItem.FileLocation;
                if (File.Exists(pathAndFile))
                {
                    string hideGCodeWarning = ApplicationSettings.Instance.get("HideGCodeWarning");

                    if (Path.GetExtension(pathAndFile).ToUpper() == ".GCODE" && hideGCodeWarning == null )
                    {
                        CheckBox hideGCodeWaringCheckBox = new CheckBox("Do not show this again");
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
                        string message = "The file you are attempting to print is a GCode file.\n\nGCode files tell your printer exactly what to do.  They are not modified by SliceSettings and my not be appropriate for your specific printer configuration.\n\nOnly print from GCode files if you know they mach your current printer and configuration.\n\nAre you sure you want to print this GCode file?";
                        if (!StyledMessageBox.ShowMessageBox(message, "Warning GCode file", new GuiWidget[] { hideGCodeWaringCheckBox }, StyledMessageBox.MessageType.YES_NO))
                        {
                            // the user selected 'no' they don't want to print the file
                            return;
                        }
                    }

                    PrinterCommunication.Instance.CommunicationState = PrinterCommunication.CommunicationStates.PreparingToPrint;
                    PrintItemWrapper partToPrint = PrinterCommunication.Instance.ActivePrintItem;
                    SlicingQueue.Instance.QueuePartForSlicing(partToPrint);
                    partToPrint.Done += new EventHandler(partToPrint_SliceDone);

                }
                else
                {
                    string message = String.Format("Cannot find\n'{0}'.\nWould you like to remove it from the queue?", pathAndFile);
                    if (StyledMessageBox.ShowMessageBox(message, "Item not found", StyledMessageBox.MessageType.YES_NO))
                    {
                        PrintQueueControl.Instance.RemoveIndex(PrintQueueControl.Instance.SelectedIndex);
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
            if (PrintQueueControl.Instance.Count > 1)
            {
                PrintQueueControl.Instance.MoveSelectedToBottom();
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
            PrintQueueControl.Instance.RemoveIndex(PrintQueueControl.Instance.SelectedIndex);
            PrintQueueControl.Instance.SaveDefaultQueue();
        }

        void onPauseButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.Pause();
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
            PrintQueueControl.Instance.RemoveIndex(PrintQueueControl.Instance.SelectedIndex);
            PrintQueueControl.Instance.SaveDefaultQueue();
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
                        this.activePrintButtons.Add(cancelButton);
                        EnableActiveButtons();
                        break;

                    case PrinterCommunication.CommunicationStates.Connected:
                        this.activePrintButtons.Add(startButton);

                        //Show 'skip' button if there are more items in queue
                        if (PrintQueueControl.Instance.Count > 1)
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
                        this.activePrintButtons.Add(pauseButton);
                        this.activePrintButtons.Add(cancelButton);
                        EnableActiveButtons();
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
