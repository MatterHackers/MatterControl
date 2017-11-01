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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintHistory;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterActionsBar : FlowLayoutWidget
	{
		private PrinterConfig printer;
		private EventHandler unregisterEvents;
		private static EePromMarlinWindow openEePromMarlinWidget = null;
		private static EePromRepetierWindow openEePromRepetierWidget = null;
		private string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize() + "\n\n" + "You may need to wait a minute for your printer to finish initializing.".Localize();
		private string noEepromMappingTitle = "Warning - No EEProm Mapping".Localize();

		private List<Button> activePrintButtons = new List<Button>();
		private List<Button> allPrintButtons = new List<Button>();

		private Button cancelConnectButton;
		private Button resetConnectionButton;
		private Button resumeResumeButton;

		private Button startPrintButton;
		private Button pausePrintButton;
		private Button cancelPrintButton;

		private Button finishSetupButton;

		private OverflowMenu overflowMenu;

		private CancellationTokenSource gcodeLoadCancellationTokenSource;

		private PrinterTabPage printerTabPage;

		public PrinterActionsBar(PrinterConfig printer, PrinterTabPage printerTabPage, ThemeConfig theme)
		{
			this.printer = printer;
			this.printerTabPage = printerTabPage;

			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			this.AddChild(new PrinterConnectButton(printer, theme));

			BuildChildElements(theme);

			this.AddChild(new SlicePopupMenu(printer, theme, printerTabPage));

			// Add Handlers
			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				UiThread.RunOnIdle(SetButtonStates);
			}, ref unregisterEvents);

			ProfileManager.ProfilesListChanged.RegisterEvent((s, e) =>
			{
				UiThread.RunOnIdle(SetButtonStates);
			}, ref unregisterEvents);

			printer.Settings.PrintLevelingEnabledChanged.RegisterEvent((s, e) =>
			{
				SetButtonStates();
			}, ref unregisterEvents);

			// put in the detail message
			var printerConnectionDetail = new TextWidget("")
			{
				Margin = new BorderDouble(5, 0),
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				AutoExpandBoundsToText = true,
				PointSize = 8
			};
			printer.Connection.PrintingStateChanged.RegisterEvent((s, e) =>
			{
				printerConnectionDetail.Text = printer.Connection.PrinterConnectionStatus;
			}, ref unregisterEvents);
			this.AddChild(printerConnectionDetail);

			this.AddChild(new HorizontalSpacer());

			bool shareTemp = printer.Settings.GetValue<bool>(SettingsKey.extruders_share_temperature);
			int extruderCount = shareTemp ? 1 : printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
			{
				this.AddChild(new TemperatureWidgetHotend(printer, extruderIndex, theme.MenuButtonFactory)
				{
					Margin = new BorderDouble(right: 10)
				});
			}

			if (printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed))
			{
				this.AddChild(new TemperatureWidgetBed(printer));
			}

			overflowMenu = new OverflowMenu(IconColor.Theme)
			{
				AlignToRightEdge = true,
				Name = "Printer Overflow Menu",
				Margin = theme.ButtonSpacing
			};
			overflowMenu.DynamicPopupContent = GeneratePrinterOverflowMenu;

			ApplicationController.Instance.ActivePrinter.Connection.ConnectionSucceeded.RegisterEvent((s, e) =>
			{
				UiThread.RunOnIdle(PrintRecovery.CheckIfNeedToRecoverPrint);
			}, ref unregisterEvents);

			this.AddChild(overflowMenu);
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			childToAdd.VAnchor |= VAnchor.Center;
			base.AddChild(childToAdd, indexInChildrenList);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			gcodeLoadCancellationTokenSource?.Cancel();
			base.OnClosed(e);
		}

		private GuiWidget GeneratePrinterOverflowMenu()
		{
			var menuActions = new NamedAction[]
			{
				new NamedAction()
				{
					Icon = AggContext.StaticData.LoadIcon("memory_16x16.png", 16, 16),
					Title = "Configure EEProm".Localize(),
					Action = configureEePromButton_Click
				},
				new NamedAction()
				{
					Title = "Rename Printer".Localize(),
					Action = () =>
					{
						WizardWindow.Show(
							new InputBoxPage(
								"Rename Printer".Localize(),
								"Name".Localize(),
								printer.Settings.GetValue(SettingsKey.printer_name),
								"Enter New Name Here".Localize(),
								"Rename".Localize(),
								(newName) =>
								{
									if (!string.IsNullOrEmpty(newName))
									{
										printer.Settings.SetValue(SettingsKey.printer_name, newName);
									}
								}));
					}
				},
				new NamedAction() { Title = "----" },
				new NamedAction()
				{
					Title = "Delete Printer".Localize(),
					Action = () =>
					{
						StyledMessageBox.ShowMessageBox(
							(doDelete) =>
							{
								if (doDelete)
								{
									printer.Settings.Helpers.SetMarkedForDelete(true);
								}
							},
							"Are you sure you want to delete your currently selected printer?".Localize(),
							"Delete Printer?".Localize(),
							StyledMessageBox.MessageType.YES_NO,
							"Delete Printer".Localize());
					}
				}
			};

			return ApplicationController.Instance.Theme.CreatePopupMenu(menuActions);
		}

		private void configureEePromButton_Click()
		{
			UiThread.RunOnIdle(() =>
			{
#if false // This is to force the creation of the repetier window for testing when we don't have repetier firmware.
                        new MatterHackers.MatterControl.EeProm.EePromRepetierWidget();
#else
				switch (printer.Connection.FirmwareType)
				{
					case FirmwareTypes.Repetier:
						if (openEePromRepetierWidget != null)
						{
							openEePromRepetierWidget.BringToFront();
						}
						else
						{
							openEePromRepetierWidget = new EePromRepetierWindow(printer.Connection);
							openEePromRepetierWidget.Closed += (RepetierWidget, RepetierEvent) =>
							{
								openEePromRepetierWidget = null;
							};
						}
						break;

					case FirmwareTypes.Marlin:
						if (openEePromMarlinWidget != null)
						{
							openEePromMarlinWidget.BringToFront();
						}
						else
						{
							openEePromMarlinWidget = new EePromMarlinWindow(printer.Connection);
							openEePromMarlinWidget.Closed += (marlinWidget, marlinEvent) =>
							{
								openEePromMarlinWidget = null;
							};
						}
						break;

					default:
						printer.Connection.SendLineToPrinterNow("M115");
						StyledMessageBox.ShowMessageBox(noEepromMappingMessage, noEepromMappingTitle, StyledMessageBox.MessageType.OK);
						break;
				}
#endif
			});
		}

		#region From PrinterActionRow
		protected void BuildChildElements(ThemeConfig theme)
		{
			var defaultMargin = theme.ButtonSpacing;

			startPrintButton = theme.ButtonFactory.Generate("Print".Localize().ToUpper());
			startPrintButton.Name = "Start Print Button";
			startPrintButton.ToolTipText = "Begin printing the selected item.".Localize();
			startPrintButton.Margin = defaultMargin;
			startPrintButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(async () =>
				{
					await ApplicationController.Instance.PrintPart(
						printer.Bed.printItem,
						printer,
						printerTabPage.view3DWidget,
						null);
				});
			};
			this.AddChild(startPrintButton);
			allPrintButtons.Add(startPrintButton);

			finishSetupButton = theme.ButtonFactory.Generate("Finish Setup...".Localize());
			finishSetupButton.Name = "Finish Setup Button";
			finishSetupButton.ToolTipText = "Run setup configuration for printer.".Localize();
			finishSetupButton.Margin = defaultMargin;
			finishSetupButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(async () =>
				{
					await ApplicationController.Instance.PrintPart(
						printer.Bed.printItem,
						printer,
						printerTabPage.view3DWidget,
						null);
				});
			};
			this.AddChild(finishSetupButton);
			allPrintButtons.Add(finishSetupButton);

			resetConnectionButton = theme.ButtonFactory.Generate("Reset".Localize().ToUpper(), AggContext.StaticData.LoadIcon("e_stop.png", 14, 14, IconColor.Theme));
			resetConnectionButton.ToolTipText = "Reboots the firmware on the controller".Localize();
			resetConnectionButton.Margin = defaultMargin;
			resetConnectionButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(printer.Connection.RebootBoard);
			};
			this.AddChild(resetConnectionButton);
			allPrintButtons.Add(resetConnectionButton);

			pausePrintButton = theme.ButtonFactory.Generate("Pause".Localize().ToUpper());
			pausePrintButton.ToolTipText = "Pause the current print".Localize();
			pausePrintButton.Margin = defaultMargin;
			pausePrintButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(printer.Connection.RequestPause);
				pausePrintButton.Enabled = false;
			};
			this.AddChild(pausePrintButton);
			allPrintButtons.Add(pausePrintButton);

			cancelConnectButton = theme.ButtonFactory.Generate("Cancel Connect".Localize().ToUpper());
			cancelConnectButton.ToolTipText = "Stop trying to connect to the printer.".Localize();
			cancelConnectButton.Margin = defaultMargin;
			cancelConnectButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ConditionalCancelPrint();
				UiThread.RunOnIdle(SetButtonStates);
			});
			this.AddChild(cancelConnectButton);
			allPrintButtons.Add(cancelConnectButton);

			cancelPrintButton = theme.ButtonFactory.Generate("Cancel".Localize().ToUpper());
			cancelPrintButton.ToolTipText = "Stop the current print".Localize();
			cancelPrintButton.Name = "Cancel Print Button";
			cancelPrintButton.Margin = defaultMargin;
			cancelPrintButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ConditionalCancelPrint();
				SetButtonStates();
			});
			this.AddChild(cancelPrintButton);
			allPrintButtons.Add(cancelPrintButton);

			resumeResumeButton = theme.ButtonFactory.Generate("Resume".Localize().ToUpper());
			resumeResumeButton.ToolTipText = "Resume the current print".Localize();
			resumeResumeButton.Margin = defaultMargin;
			resumeResumeButton.Name = "Resume Button";
			resumeResumeButton.Click += (s, e) =>
			{
				if (printer.Connection.PrinterIsPaused)
				{
					printer.Connection.Resume();
				}
				pausePrintButton.Enabled = true;
			};
			this.AddChild(resumeResumeButton);
			allPrintButtons.Add(resumeResumeButton);

			SetButtonStates();
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

		//Set the states of the buttons based on the status of PrinterCommunication
		protected void SetButtonStates()
		{
			this.activePrintButtons.Clear();
			if (!printer.Connection.PrinterIsConnected
				&& printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect)
			{
				if (!ProfileManager.Instance.ActiveProfiles.Any())
				{
					// TODO: Possibly upsell add printer - ideally don't show printer tab, only show Plus tab
					//this.activePrintButtons.Add(addPrinterButton);
				}

				ShowActiveButtons();
				EnableActiveButtons();
			}
			else
			{
				switch (printer.Connection.CommunicationState)
				{
					case CommunicationStates.AttemptingToConnect:
						this.activePrintButtons.Add(cancelConnectButton);
						EnableActiveButtons();
						break;

					case CommunicationStates.Connected:
						PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
						if (levelingData != null && printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
							&& !levelingData.HasBeenRunAndEnabled())
						{
							this.activePrintButtons.Add(finishSetupButton);
						}
						else
						{
							this.activePrintButtons.Add(startPrintButton);
						}

						EnableActiveButtons();
						break;

					case CommunicationStates.PreparingToPrint:
						this.activePrintButtons.Add(cancelPrintButton);
						EnableActiveButtons();
						break;

					case CommunicationStates.PrintingFromSd:
					case CommunicationStates.Printing:
						if (!printer.Connection.PrintWasCanceled)
						{
							this.activePrintButtons.Add(pausePrintButton);
							this.activePrintButtons.Add(cancelPrintButton);
						}
						else if (UserSettings.Instance.IsTouchScreen)
						{
							this.activePrintButtons.Add(resetConnectionButton);
						}

						EnableActiveButtons();
						break;

					case CommunicationStates.Paused:
						this.activePrintButtons.Add(resumeResumeButton);
						this.activePrintButtons.Add(cancelPrintButton);
						EnableActiveButtons();
						break;

					case CommunicationStates.FinishedPrint:
						this.activePrintButtons.Add(startPrintButton);
						EnableActiveButtons();
						break;

					default:
						DisableActiveButtons();
						break;
				}
			}

			if (printer.Connection.PrinterIsConnected
				&& printer.Settings.GetValue<bool>(SettingsKey.show_reset_connection))
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

		#endregion
	}
}