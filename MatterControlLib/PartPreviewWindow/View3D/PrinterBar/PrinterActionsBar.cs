/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Collections.ObjectModel;
using System.IO;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.MatterControl.PrintHistory;
using MatterHackers.MatterControl.SetupWizard;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterActionsBar : OverflowBar
	{
		private PrinterConfig printer;
		private static MarlinEEPromPage marlinEEPromPage = null;
		private static RepetierEEPromPage repetierEEPromPage = null;

		private PrinterTabPage printerTabPage;

		internal GuiWidget sliceButton;

		private RadioIconButton layers2DButton;
		internal RadioIconButton layers3DButton;
		internal RadioIconButton modelViewButton;

		private Dictionary<PartViewMode, RadioIconButton> viewModes = new Dictionary<PartViewMode, RadioIconButton>();

		public PrinterActionsBar(PrinterConfig printer, PrinterTabPage printerTabPage, ThemeConfig theme)
			: base(theme)
		{
			this.printer = printer;
			this.printerTabPage = printerTabPage;

			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			var defaultMargin = theme.ButtonSpacing;

			// add the reset button first (if there is one)
			if (printer.Settings.GetValue<bool>(SettingsKey.show_reset_connection))
			{
				var resetConnectionButton = new TextIconButton(
					"Reset".Localize(),
					AggContext.StaticData.LoadIcon("e_stop.png", 14, 14, theme.InvertIcons),
					theme)
				{
					ToolTipText = "Reboots the firmware on the controller".Localize(),
					Margin = defaultMargin
				};
				resetConnectionButton.Click += (s, e) =>
				{
					UiThread.RunOnIdle(printer.Connection.RebootBoard);
				};
				this.AddChild(resetConnectionButton);
			}

			this.AddChild(new PrinterConnectButton(printer, theme));

			// add the start print button
			GuiWidget startPrintButton;
			this.AddChild(startPrintButton = new PrintPopupMenu(printer, theme)
			{
				Margin = theme.ButtonSpacing
			});

			void SetPrintButtonStyle(object s, EventArgs e)
			{
				switch (printer.Connection.CommunicationState)
				{
					case CommunicationStates.FinishedPrint:
					case CommunicationStates.Connected:
						theme.ApplyPrimaryActionStyle(startPrintButton);
						break;

					default:
						theme.RemovePrimaryActionStyle(startPrintButton);
						break;
				}
			}

			// make sure the buttons state is set correctly
			printer.Connection.CommunicationStateChanged += SetPrintButtonStyle;
			startPrintButton.Closed += (s, e) => printer.Connection.CommunicationStateChanged -= SetPrintButtonStyle;

			// and set the style right now
			SetPrintButtonStyle(this, null);

			this.AddChild(new SliceButton(printer, printerTabPage, theme)
			{
				Name = "Generate Gcode Button",
				Margin = theme.ButtonSpacing,
			});

			// Add vertical separator
			this.AddChild(new ToolbarSeparator(theme)
			{
				VAnchor = VAnchor.Absolute,
				Height = theme.ButtonHeight,
			});

			var buttonGroupB = new ObservableCollection<GuiWidget>();

			var iconPath = Path.Combine("ViewTransformControls", "model.png");
			modelViewButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 16, 16, theme.InvertIcons), theme)
			{
				SiblingRadioButtonList = buttonGroupB,
				Name = "Model View Button",
				Checked = printer?.ViewState.ViewMode == PartViewMode.Model || printer == null,
				ToolTipText = "Model View".Localize(),
				Margin = theme.ButtonSpacing
			};
			modelViewButton.Click += SwitchModes_Click;
			buttonGroupB.Add(modelViewButton);
			AddChild(modelViewButton);

			viewModes.Add(PartViewMode.Model, modelViewButton);

			iconPath = Path.Combine("ViewTransformControls", "gcode_3d.png");
			layers3DButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 16, 16, theme.InvertIcons), theme)
			{
				SiblingRadioButtonList = buttonGroupB,
				Name = "Layers3D Button",
				Checked = printer?.ViewState.ViewMode == PartViewMode.Layers3D,
				ToolTipText = "3D Layer View".Localize(),
				Margin = theme.ButtonSpacing
			};
			layers3DButton.Click += SwitchModes_Click;
			buttonGroupB.Add(layers3DButton);

			viewModes.Add(PartViewMode.Layers3D, layers3DButton);

			if (!UserSettings.Instance.IsTouchScreen)
			{
				this.AddChild(layers3DButton);
			}

			iconPath = Path.Combine("ViewTransformControls", "gcode_2d.png");
			layers2DButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 16, 16, theme.InvertIcons), theme)
			{
				SiblingRadioButtonList = buttonGroupB,
				Name = "Layers2D Button",
				Checked = printer?.ViewState.ViewMode == PartViewMode.Layers2D,
				ToolTipText = "2D Layer View".Localize(),
				Margin = theme.ButtonSpacing,
			};
			layers2DButton.Click += SwitchModes_Click;
			buttonGroupB.Add(layers2DButton);
			this.AddChild(layers2DButton);

			viewModes.Add(PartViewMode.Layers2D, layers2DButton);

			this.AddChild(new HorizontalSpacer());

			int hotendCount = printer.Settings.Helpers.HotendCount();
			if (!printer.Settings.GetValue<bool>(SettingsKey.sla_printer))
			{
				for (int extruderIndex = 0; extruderIndex < hotendCount; extruderIndex++)
				{
					this.AddChild(new TemperatureWidgetHotend(printer, extruderIndex, theme, hotendCount)
					{
						Margin = new BorderDouble(right: 10)
					});
				}
			}

			if (printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed))
			{
				this.AddChild(new TemperatureWidgetBed(printer, theme));
			}

			this.OverflowButton.Name = "Printer Overflow Menu";
			this.ExtendOverflowMenu = (popupMenu) =>
			{
				this.GeneratePrinterOverflowMenu(popupMenu, ApplicationController.Instance.MenuTheme);
			};

			printer.ViewState.ViewModeChanged += (s, e) =>
			{
				if (viewModes[e.ViewMode] is RadioIconButton activeButton
					&& viewModes[e.PreviousMode] is RadioIconButton previousButton
					&& !buttonIsBeingClicked)
				{
					// Show slide to animation from previous to current, on completion update view to current by setting active.Checked
					previousButton.SlideToNewState(
						activeButton,
						this,
						() =>
						{
							activeButton.Checked = true;
						},
						theme);
				}
			};

			// Register listeners
			printer.Connection.ConnectionSucceeded += CheckForPrintRecovery;

			// if we are already connected than check if there is a print recovery right now
			if (printer.Connection.CommunicationState == CommunicationStates.Connected)
			{
				CheckForPrintRecovery(null, null);
			}
		}

		bool buttonIsBeingClicked;
		private void SwitchModes_Click(object sender, MouseEventArgs e)
		{
			buttonIsBeingClicked = true;
			if (sender is GuiWidget widget)
			{
				if (widget.Name == "Layers2D Button")
				{
					printer.ViewState.ViewMode = PartViewMode.Layers2D;
					printer.Bed.EnsureGCodeLoaded();
				}
				else if (widget.Name == "Layers3D Button")
				{
					printer.ViewState.ViewMode = PartViewMode.Layers3D;
					printer.Bed.EnsureGCodeLoaded();
				}
				else
				{
					printer.ViewState.ViewMode = PartViewMode.Model;
				}
			}
			buttonIsBeingClicked = false;
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			childToAdd.VAnchor = VAnchor.Center;
			base.AddChild(childToAdd, indexInChildrenList);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.ConnectionSucceeded -= CheckForPrintRecovery;

			base.OnClosed(e);
		}

		private void CheckForPrintRecovery(object s, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				PrintRecovery.CheckIfNeedToRecoverPrint(printer);
			});
		}

		private void GeneratePrinterOverflowMenu(PopupMenu popupMenu, ThemeConfig theme)
		{
			var menuActions = new List<NamedAction>()
			{
				new NamedAction()
				{
					Icon = AggContext.StaticData.LoadIcon("memory_16x16.png", 16, 16, theme.InvertIcons),
					Title = "Configure EEProm".Localize(),
					Action = configureEePromButton_Click,
					IsEnabled = () => printer.Connection.IsConnected
				},
				new NamedBoolAction()
				{
					Title = "Show Controls".Localize(),
					Action = () => { },
					GetIsActive = () => printer.ViewState.ControlsVisible,
					SetIsActive = (value) => printer.ViewState.ControlsVisible = value
				},
				new NamedBoolAction()
				{
					Title = "Show Terminal".Localize(),
					Action = () => { },
					GetIsActive = () => printer.ViewState.TerminalVisible,
					SetIsActive = (value) => printer.ViewState.TerminalVisible = value
				},
				new NamedBoolAction()
				{
					Title = "Configure Printer".Localize(),
					Action = () => { },
					GetIsActive = () => printer.ViewState.ConfigurePrinterVisible,
					SetIsActive = (value) => printer.ViewState.ConfigurePrinterVisible = value
				},
				new ActionSeparator(),
				new NamedAction()
				{
					Title = "Import Presets".Localize(),
					Action = () =>
					{
						AggContext.FileDialogs.OpenFileDialog(
							new OpenFileDialogParams("settings files|*.printer"),
							(dialogParams) =>
							{
								if (!string.IsNullOrEmpty(dialogParams.FileName))
								{
									DialogWindow.Show(new ImportSettingsPage(dialogParams.FileName, printer));
								}
							});
					}
				},
				new NamedAction()
				{
					Title = "Export Printer".Localize(),
					Action = () => UiThread.RunOnIdle(() =>
					{
						ApplicationController.Instance.ExportAsMatterControlConfig(printer);
					}),
					Icon = AggContext.StaticData.LoadIcon("cube_export.png", 16, 16, theme.InvertIcons),
				},
				new ActionSeparator(),

				new NamedAction()
				{
					Title = "Calibrate Printer".Localize(),
					Action = () => UiThread.RunOnIdle(() =>
					{
						UiThread.RunOnIdle(() =>
						{
							DialogWindow.Show(new PrinterCalibrationWizard(printer, theme));
						});
					}),
					Icon = AggContext.StaticData.LoadIcon("compass.png", theme.InvertIcons)
				},
				new ActionSeparator(),
				new NamedAction()
				{
					Title = "Restore Settings".Localize(),
					Action = () =>
					{

						DialogWindow.Show(new PrinterProfileHistoryPage(printer));
					}
				},
				new NamedAction()
				{
					Title = "Reset to Defaults".Localize(),
					Action = () =>
					{
						StyledMessageBox.ShowMessageBox(
							(revertSettings) =>
							{
								if (revertSettings)
								{
									printer.Settings.ClearUserOverrides();
									printer.Settings.ClearBlackList();
									// this is user driven
									printer.Settings.Save();
									printer.Settings.Helpers.PrintLevelingData.SampledPositions.Clear();

									ApplicationController.Instance.ReloadAll().ConfigureAwait(false);
								}
							},
							"Resetting to default values will remove your current overrides and restore your original printer settings.\nAre you sure you want to continue?".Localize(),
							"Revert Settings".Localize(),
							StyledMessageBox.MessageType.YES_NO);
					}
				},
				new ActionSeparator(),
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
									ProfileManager.Instance.DeletePrinter(printer.Settings.ID);
								}
							},
							"Are you sure you want to delete printer '{0}'?".Localize().FormatWith(printer.Settings.GetValue(SettingsKey.printer_name)),
							"Delete Printer?".Localize(),
							StyledMessageBox.MessageType.YES_NO,
							"Delete Printer".Localize());
					},
				}
			};

			theme.CreateMenuItems(popupMenu, menuActions);
		}

		private void configureEePromButton_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				var firmwareType = printer.Connection.FirmwareType;

				// Force Repetier firmware for testing when we don't have repetier firmware
				if (false)
				{
					firmwareType = FirmwareTypes.Repetier;
				}

				switch (firmwareType)
				{
					case FirmwareTypes.Repetier:
						if (repetierEEPromPage != null)
						{
							repetierEEPromPage.DialogWindow.BringToFront();
						}
						else
						{
							repetierEEPromPage = new RepetierEEPromPage(printer);
							repetierEEPromPage.Closed += (s, e) =>
							{
								repetierEEPromPage = null;
							};

							DialogWindow.Show(repetierEEPromPage);
						}
						break;

					case FirmwareTypes.Marlin:
						if (marlinEEPromPage != null)
						{
							marlinEEPromPage.DialogWindow.BringToFront();
						}
						else
						{
							marlinEEPromPage = new MarlinEEPromPage(printer);
							marlinEEPromPage.Closed += (s, e) =>
							{
								marlinEEPromPage = null;
							};

							DialogWindow.Show(marlinEEPromPage);
						}
						break;

					default:
						printer.Connection.QueueLine("M115");
						StyledMessageBox.ShowMessageBox(
							"Oops! There is no eeprom mapping for your printer's firmware.".Localize() + "\n\n" + "You may need to wait a minute for your printer to finish initializing.".Localize(),
							"Warning - No EEProm Mapping".Localize(),
							StyledMessageBox.MessageType.OK);
						break;
				}
			});
		}
	}
}