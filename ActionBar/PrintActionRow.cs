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
using MatterHackers.MatterControl.PrinterCommunication;

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

            textImageButtonFactory.borderWidth = 1;
            textImageButtonFactory.normalBorderColor = new RGBA_Bytes(255,255,255, 100);
            textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(255,255,255, 100);
            textImageButtonFactory.disabledFillColor = ActiveTheme.Instance.PrimaryAccentColor;
        }

        protected override void AddChildElements()
        {
            addButton = (TooltipButton)textImageButtonFactory.GenerateTooltipButton(LocalizedString.Get("Add"), "icon_circle_plus.png");
			addButton.tooltipText = LocalizedString.Get("Add a file to be printed");
            addButton.Margin = new BorderDouble(0, 6, 6, 3);

			startButton = (TooltipButton)textImageButtonFactory.GenerateTooltipButton(LocalizedString.Get("Print"), "icon_play_32x32.png");
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
            PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
            addButton.Click += new EventHandler(onAddButton_Click);
            startButton.Click += new EventHandler(onStartButton_Click);
            skipButton.Click += new EventHandler(onSkipButton_Click);
            removeButton.Click += new EventHandler(onRemoveButton_Click);
            resumeButton.Click += new EventHandler(onResumeButton_Click);
            pauseButton.Click += new EventHandler(onPauseButton_Click);

			cancelButton.Click += (sender, e) => { UiThread.RunOnIdle(CancelButton_Click); };
			cancelConnectButton.Click += (sender, e) => { UiThread.RunOnIdle(CancelConnectionButton_Click); };            
            reprintButton.Click += new EventHandler(onReprintButton_Click);
            doneWithCurrentPartButton.Click += new EventHandler(onDoneWithCurrentPartButton_Click);
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
        }
			
        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public void ThemeChanged(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        void onAddButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle(AddButtonOnIdle);
        }

        void AddButtonOnIdle(object state)
        {
            FileDialog.OpenFileDialog(
                new OpenFileDialogParams(ApplicationSettings.OpenPrintableFileParams, multiSelect: true),
                (openParams) =>
                {
                    if (openParams.FileNames != null)
                    {
                        foreach (string loadedFileName in openParams.FileNames)
                        {
                            QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(loadedFileName), Path.GetFullPath(loadedFileName))));
                        }
                    }
                });
        }

        void onStartButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
            {
                PrinterConnectionAndCommunication.Instance.PrintActivePartIfPossible();
            });
        }

        void onSkipButton_Click(object sender, EventArgs mouseEvent)
        {
            if (QueueData.Instance.Count > 1)
            {
                queueDataView.MoveToNext();
            }
        }

        void onResumeButton_Click(object sender, EventArgs mouseEvent)
        {
            if (PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
            {
                PrinterConnectionAndCommunication.Instance.Resume();
            }
        }

        void onRemoveButton_Click(object sender, EventArgs mouseEvent)
        {
            QueueData.Instance.RemoveAt(queueDataView.SelectedIndex);
        }

        void onPauseButton_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.RequestPause();
        }

        string cancelCurrentPrintMessage = "Cancel the current print?".Localize();
        string cancelCurrentPrintTitle = "Cancel Print?".Localize();
        void CancelButton_Click(object state)
        {
            if (timeSincePrintStarted.IsRunning && timeSincePrintStarted.ElapsedMilliseconds > (2 * 60 * 1000))
            {
                StyledMessageBox.ShowMessageBox(onConfirmCancelPrint, cancelCurrentPrintMessage, cancelCurrentPrintTitle, StyledMessageBox.MessageType.YES_NO);                
            }
            else
            {
                CancelPrinting();
			}
		}

		void ResetConnectionButton_Click(object state)
		{
			PrinterConnectionAndCommunication.Instance.RebootBoard();
		}
		
		void onConfirmCancelPrint(bool messageBoxResponse)
        {
            if (messageBoxResponse)
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
            if (PrinterConnectionAndCommunication.Instance.CommunicationState == PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint)
            {
                SlicingQueue.Instance.CancelCurrentSlicing();
            }            
            PrinterConnectionAndCommunication.Instance.Stop();
            timeSincePrintStarted.Reset();
			UiThread.RunOnIdle((state2) => { SetButtonStates(); });
		}

        void onDoneWithCurrentPartButton_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.ResetToReadyState();
            QueueData.Instance.RemoveAt(queueDataView.SelectedIndex);
            // We don't have to change the selected index because we should be on the next one as we deleted the one 
            // we were on.
        }

        void onReprintButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
            {
                PrinterConnectionAndCommunication.Instance.PrintActivePartIfPossible();
            });
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
            if (PrinterConnectionAndCommunication.Instance.ActivePrintItem == null)
            {
                this.activePrintButtons.Add(addButton);
                ShowActiveButtons();
                EnableActiveButtons();
            }
            else
            {
                switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
                {
                    case PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect:
                        this.activePrintButtons.Add(cancelConnectButton);
                        EnableActiveButtons();
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.Connected:
                        this.activePrintButtons.Add(startButton);

                        //Show 'skip' button if there are more items in queue
                        if (QueueData.Instance.Count > 1)
                        {
                            this.activePrintButtons.Add(skipButton);
                        }

                        this.activePrintButtons.Add(removeButton);
                        EnableActiveButtons();
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint:
                        this.activePrintButtons.Add(cancelButton);
                        EnableActiveButtons();
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.PrintingFromSd:
                    case PrinterConnectionAndCommunication.CommunicationStates.PrintingToSd:
                    case PrinterConnectionAndCommunication.CommunicationStates.Printing:
						if (!timeSincePrintStarted.IsRunning)
						{
							timeSincePrintStarted.Restart();
						}

						if (!PrinterConnectionAndCommunication.Instance.PrintWasCanceled)
						{
							this.activePrintButtons.Add(pauseButton);
							this.activePrintButtons.Add(cancelButton);
						}
                        EnableActiveButtons();
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.Paused:
                        this.activePrintButtons.Add(resumeButton);
                        this.activePrintButtons.Add(cancelButton);
                        EnableActiveButtons();
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.FinishedPrint:
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
