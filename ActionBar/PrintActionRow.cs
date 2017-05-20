/*
Copyright (c) 2016, Lars Brubaker
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;

#if __ANDROID__
using MatterHackers.SerialPortCommunication.FrostedSerial;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.ActionBar
{
	internal class PrintActionRow : FlowLayoutWidget
	{
		private List<Button> activePrintButtons = new List<Button>();
		private Button addButton;
		private List<Button> allPrintButtons = new List<Button>();
		private Button cancelButton;
		private Button cancelConnectButton;
		private Button touchScreenConnectButton;
		private Button addPrinterButton;
		private Button selectPrinterButton;
		private Button resetConnectionButton;
		private Button doneWithCurrentPartButton;
		private Button pauseButton;
		private QueueDataView queueDataView;
		private Button removeButton;
		private Button reprintButton;
		private Button resumeButton;
		private Button skipButton;
		private Button startButton;
		private Button finishSetupButton;
		private TextImageButtonFactory textImageButtonFactory;
		private EventHandler unregisterEvents;

		public PrintActionRow(QueueDataView queueDataView)
		{
			this.HAnchor = HAnchor.ParentLeftRight;

			textImageButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = RGBA_Bytes.White,
				disabledTextColor = RGBA_Bytes.LightGray,
				hoverTextColor = RGBA_Bytes.White,
				pressedTextColor = RGBA_Bytes.White,
				AllowThemeToAdjustImage = false,
				borderWidth = 1,
				FixedHeight = 52 * GuiWidget.DeviceScale,
				fontSize = 14,
				normalBorderColor = new RGBA_Bytes(255, 255, 255, 100),
				hoverBorderColor = new RGBA_Bytes(255, 255, 255, 100)
			};

			this.queueDataView = queueDataView;

			AddChildElements();

			// Add Handlers
			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
			ProfileManager.ProfilesListChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
		}

		protected void AddChildElements()
		{
			addButton = textImageButtonFactory.GenerateTooltipButton("Add".Localize(), StaticData.Instance.LoadIcon("icon_circle_plus.png",32,32).InvertLightness());
			addButton.ToolTipText = "Add a file to be printed".Localize();
			addButton.Margin = new BorderDouble(6, 6, 6, 3);
			addButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(AddButtonOnIdle);
			};

			startButton = textImageButtonFactory.GenerateTooltipButton("Print".Localize(), StaticData.Instance.LoadIcon("icon_play_32x32.png",32,32).InvertLightness());
			startButton.Name = "Start Print Button";
			startButton.ToolTipText = "Begin printing the selected item.".Localize();
			startButton.Margin = new BorderDouble(6, 6, 6, 3);
			startButton.Click += onStartButton_Click;

			finishSetupButton = textImageButtonFactory.GenerateTooltipButton("Finish Setup...".Localize());
			finishSetupButton.Name = "Finish Setup Button";
			finishSetupButton.ToolTipText = "Run setup configuration for printer.".Localize();
			finishSetupButton.Margin = new BorderDouble(6, 6, 6, 3);
			finishSetupButton.Click += onStartButton_Click;

			touchScreenConnectButton = textImageButtonFactory.GenerateTooltipButton("Connect".Localize(), StaticData.Instance.LoadIcon("connect.png", 32,32).InvertLightness());
			touchScreenConnectButton.ToolTipText = "Connect to the printer".Localize();
			touchScreenConnectButton.Margin = new BorderDouble(6, 6, 6, 3);
			touchScreenConnectButton.Click += (s, e) =>
			{
				if (ActiveSliceSettings.Instance.PrinterSelected)
				{
#if __ANDROID__
					if (!ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.enable_network_printing)
					    && !FrostedSerialPort.HasPermissionToDevice())
					{
						// Opens the USB device permissions dialog which will call back into our UsbDevice broadcast receiver to connect
						FrostedSerialPort.RequestPermissionToDevice(RunTroubleShooting);
					}
					else
#endif
					{
						PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
						PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter(true);
					}
				}
			};

			addPrinterButton = textImageButtonFactory.GenerateTooltipButton("Add Printer".Localize());
			addPrinterButton.ToolTipText = "Select and add a new printer.".Localize();
			addPrinterButton.Margin = new BorderDouble(6, 6, 6, 3);
			addPrinterButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() => WizardWindow.ShowPrinterSetup(true));
			};

			selectPrinterButton = textImageButtonFactory.GenerateTooltipButton("Select Printer".Localize());
			selectPrinterButton.ToolTipText = "Select an existing printer.".Localize();
			selectPrinterButton.Margin = new BorderDouble(6, 6, 6, 3);
			selectPrinterButton.Click += (s, e) =>
			{
				WizardWindow.Show<SetupOptionsPage>("/SetupOptions", "Setup Wizard");
			};

			resetConnectionButton = textImageButtonFactory.GenerateTooltipButton("Reset".Localize(), StaticData.Instance.LoadIcon("e_stop4.png", 32,32).InvertLightness());
			resetConnectionButton.ToolTipText = "Reboots the firmware on the controller".Localize();
			resetConnectionButton.Margin = new BorderDouble(6, 6, 6, 3);
			resetConnectionButton.Click += (s, e) => UiThread.RunOnIdle(PrinterConnectionAndCommunication.Instance.RebootBoard);

			skipButton = makeButton("Skip".Localize(), "Skip the current item and move to the next in queue".Localize());
			skipButton.Click += onSkipButton_Click;

			removeButton = makeButton("Remove".Localize(), "Remove current item from queue".Localize());
			removeButton.Click += onRemoveButton_Click;

			pauseButton = makeButton("Pause".Localize(), "Pause the current print".Localize());
			pauseButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(PrinterConnectionAndCommunication.Instance.RequestPause);
				pauseButton.Enabled = false;
			};
			this.AddChild(pauseButton);
			allPrintButtons.Add(pauseButton);

			cancelConnectButton = makeButton("Cancel Connect".Localize(), "Stop trying to connect to the printer.".Localize());
			cancelConnectButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ConditionalCancelPrint();
				UiThread.RunOnIdle(SetButtonStates);
			});
			

			cancelButton = makeButton("Cancel".Localize(), "Stop the current print".Localize());
			cancelButton.Name = "Cancel Print Button";
			cancelButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ConditionalCancelPrint();
				SetButtonStates();
			});

			resumeButton = makeButton("Resume".Localize(), "Resume the current print".Localize());
			resumeButton.Name = "Resume Button";
			resumeButton.Click += (s, e) =>
			{
				if (PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
				{
					PrinterConnectionAndCommunication.Instance.Resume();
				}
				pauseButton.Enabled = true;
			};

			this.AddChild(resumeButton);
			allPrintButtons.Add(resumeButton);

			reprintButton = makeButton("Print Again".Localize(), "Print current item again".Localize());
			reprintButton.Name = "Print Again Button";
			reprintButton.Click += onReprintButton_Click;

			doneWithCurrentPartButton = makeButton("Done".Localize(), "Move to next print in queue".Localize());
			doneWithCurrentPartButton.Name = "Done Button";
			doneWithCurrentPartButton.Click += (s,e) => UiThread.RunOnIdle(() =>
			{
				PrinterConnectionAndCommunication.Instance.ResetToReadyState();
				QueueData.Instance.RemoveAt(QueueData.Instance.SelectedIndex);
				// We don't have to change the selected index because we should be on the next one as we deleted the one
				// we were on.
			});

			this.Margin = new BorderDouble(0, 0, 10, 0);
			this.HAnchor = HAnchor.FitToChildren;

			this.AddChild(touchScreenConnectButton);
			allPrintButtons.Add(touchScreenConnectButton);

			this.AddChild(addPrinterButton);
			allPrintButtons.Add(addPrinterButton);

			this.AddChild(selectPrinterButton);
			allPrintButtons.Add(selectPrinterButton);

			this.AddChild(addButton);
			allPrintButtons.Add(addButton);

			this.AddChild(startButton);
			allPrintButtons.Add(startButton);

			this.AddChild(finishSetupButton);
			allPrintButtons.Add(finishSetupButton);

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

			this.AddChild(resetConnectionButton);
			allPrintButtons.Add(resetConnectionButton);

			SetButtonStates();

			PrinterSettings.PrintLevelingEnabledChanged.RegisterEvent((s, e) => SetButtonStates(), ref unregisterEvents);
		}

		protected void DisableActiveButtons()
		{
			foreach (Button button in this.activePrintButtons)
			{
				button.Enabled = false;
			}
		}

		protected void EnableActiveButtons()
		{
			foreach (Button button in this.activePrintButtons)
			{
				button.Enabled = true;
			}
		}

		protected Button makeButton(string buttonText, string buttonToolTip = "")
		{
			Button button = textImageButtonFactory.GenerateTooltipButton(buttonText);
			button.ToolTipText = buttonToolTip;
			button.Margin = new BorderDouble(0, 6, 6, 3);
			return button;
		}

		//Set the states of the buttons based on the status of PrinterCommunication
		protected void SetButtonStates()
		{
			this.activePrintButtons.Clear();
			if (!PrinterConnectionAndCommunication.Instance.PrinterIsConnected
				&& PrinterConnectionAndCommunication.Instance.CommunicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect)
			{
				if (!ProfileManager.Instance.ActiveProfiles.Any())
				{
					this.activePrintButtons.Add(addPrinterButton);
				}
				else if (UserSettings.Instance.IsTouchScreen)
				{
					// only on touch screen because desktop has a printer list and a connect button
					if (ActiveSliceSettings.Instance.PrinterSelected)
					{
						this.activePrintButtons.Add(touchScreenConnectButton);
					}
					else // no printer selected
					{
						this.activePrintButtons.Add(selectPrinterButton);
					}
				}

				ShowActiveButtons();
				EnableActiveButtons();
			}
			else if (PrinterConnectionAndCommunication.Instance.ActivePrintItem == null)
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
						PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
						if (levelingData != null && ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
							&& !levelingData.HasBeenRunAndEnabled())
						{
							this.activePrintButtons.Add(finishSetupButton);
						}
						else
						{
							this.activePrintButtons.Add(startButton);
							//Show 'skip' button if there are more items in queue
							if (QueueData.Instance.ItemCount > 1)
							{
								this.activePrintButtons.Add(skipButton);
							}

							this.activePrintButtons.Add(removeButton);
						}

						EnableActiveButtons();
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint:
						this.activePrintButtons.Add(cancelButton);
						EnableActiveButtons();
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.PrintingFromSd:
					case PrinterConnectionAndCommunication.CommunicationStates.Printing:
						if (!PrinterConnectionAndCommunication.Instance.PrintWasCanceled)
						{
							this.activePrintButtons.Add(pauseButton);
							this.activePrintButtons.Add(cancelButton);
						}
						else if (UserSettings.Instance.IsTouchScreen)
						{
							this.activePrintButtons.Add(resetConnectionButton);
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

			if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected
				&& ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.show_reset_connection)
				&& UserSettings.Instance.IsTouchScreen)
			{
				this.activePrintButtons.Add(resetConnectionButton);
				ShowActiveButtons();
				EnableActiveButtons();
			}
			ShowActiveButtons();
		}

		protected void ShowActiveButtons()
		{
			foreach (Button button in this.allPrintButtons)
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

		private void AddButtonOnIdle()
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

		void RunTroubleShooting()
		{
			WizardWindow.Show<SetupWizardTroubleshooting>("TroubleShooting", "Trouble Shooting");
		}

		private void onRemoveButton_Click(object sender, EventArgs mouseEvent)
		{
			QueueData.Instance.RemoveAt(QueueData.Instance.SelectedIndex);
		}

		private void onReprintButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() => PrinterConnectionAndCommunication.Instance.PrintActivePartIfPossible());
		}

		private void onSkipButton_Click(object sender, EventArgs mouseEvent)
		{
			if (QueueData.Instance.ItemCount > 1)
			{
				QueueData.Instance.MoveToNext();
			}
		}

		string unsavedChangesCaption = "Unsaved Changes";
		string unsavedChangesMessage = "You have unsaved changes to your part. Are you sure you want to start this print?";
		private void onStartButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() =>
				{
					var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
					var view3D = systemWindow.ChildrenRecursive<View3DWidget>().FirstOrDefault();

					if (view3D != null && false)
						//&& view3D.ShouldBeSaved)
					{
						StyledMessageBox.ShowMessageBox((bool startPrint) =>
						{
							if (startPrint)
							{
								PrinterConnectionAndCommunication.Instance.PrintActivePartIfPossible();
							}

						}, unsavedChangesMessage, unsavedChangesCaption, StyledMessageBox.MessageType.YES_NO, "Start Print", "Cancel");
					}
					else
					{
						PrinterConnectionAndCommunication.Instance.PrintActivePartIfPossible();
					}
				}
			);
		}

		private void onStateChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(SetButtonStates);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}