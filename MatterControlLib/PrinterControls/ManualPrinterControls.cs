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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public static class EnabledWidgetExtensions
	{
		public static void SetEnabled(this GuiWidget guiWidget, bool enabled)
		{
			guiWidget.Enabled = enabled;
		}
	}

	public class ManualPrinterControls : ScrollableWidget, ICloseableTab
	{
		public static EventHandler<ManualPrinterControls> AddPluginControls;

		private static bool pluginsQueuedToAdd = false;

		private GuiWidget fanControlsContainer;
		private GuiWidget macroControlsContainer;
		private GuiWidget tuningAdjustmentControlsContainer;
		private MovementControls movementControlsContainer;
		private GuiWidget calibrationControlsContainer;
		private ThemeConfig theme;
		private PrinterConfig printer;
		private FlowLayoutWidget column;

		public ManualPrinterControls(PrinterConfig printer, ThemeConfig theme)
		{
			this.theme = theme;
			this.printer = printer;
			this.ScrollArea.HAnchor |= HAnchor.Stretch;
			this.AnchorAll();
			this.AutoScroll = true;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Name = "ManualPrinterControls";

			int headingPointSize = theme.H1PointSize;

			column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.MaxFitOrStretch,
				VAnchor = VAnchor.Fit,
				Name = "ManualPrinterControls.ControlsContainer",
				Margin = new BorderDouble(0)
			};
			this.AddChild(column);

			movementControlsContainer = this.AddPluginWidget(MovementControls.CreateSection(printer, theme)) as MovementControls;

			if (!printer.Settings.GetValue<bool>(SettingsKey.has_hardware_leveling))
			{
				calibrationControlsContainer = this.AddPluginWidget(CalibrationControls.CreateSection(printer, theme));
			}

			macroControlsContainer = this.AddPluginWidget(MacroControls.CreateSection(printer, theme));

			if (printer.Settings.GetValue<bool>(SettingsKey.has_fan))
			{
				fanControlsContainer = this.AddPluginWidget(FanControls.CreateSection(printer, theme));
			}

#if !__ANDROID__
			this.AddPluginWidget(PowerControls.CreateSection(printer, theme));
#endif

			tuningAdjustmentControlsContainer = this.AddPluginWidget(AdjustmentControls.CreateSection(printer, theme));

			// HACK: this is a hack to make the layout engine fire again for this control
			UiThread.RunOnIdle(() => tuningAdjustmentControlsContainer.Width = tuningAdjustmentControlsContainer.Width + 1);

			// Register listeners
			printer.Connection.CommunicationStateChanged += onPrinterStatusChanged;
			printer.Connection.EnableChanged += onPrinterStatusChanged;

			SetVisibleControls();
		}

		// Public printer member for AddPluginControls plugins
		public PrinterConfig Printer => printer;

		public GuiWidget AddPluginWidget(SectionWidget sectionWidget)
		{
			// Section not active due to constraints
			if (sectionWidget == null)
			{
				return null;
			}

			theme.ApplyBoxStyle(sectionWidget);

			sectionWidget.ContentPanel.Padding = new BorderDouble(10, 10, 10, 0);

			column.AddChild(sectionWidget);

			// Disable borders on all SettingsRow children in control panels
			foreach(var settingsRow in sectionWidget.ContentPanel.Descendants<SettingsRow>())
			{
				settingsRow.BorderColor = Color.Transparent;
			}

			// Return the panel widget rather than the source sectionWidget
			return sectionWidget.ContentPanel;
		}

		public override void OnLoad(EventArgs args)
		{
			if (!pluginsQueuedToAdd && Printer.Settings.GetValue(SettingsKey.include_firmware_updater) == "Simple Arduino")
			{
				UiThread.RunOnIdle(() =>
				{
					AddPluginControls?.Invoke(this, this);
					pluginsQueuedToAdd = false;
				});
				pluginsQueuedToAdd = true;
			}

			base.OnLoad(args);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.CommunicationStateChanged -= onPrinterStatusChanged;
			printer.Connection.EnableChanged -= onPrinterStatusChanged;

			base.OnClosed(e);
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			SetVisibleControls();
			UiThread.RunOnIdle(this.Invalidate);
		}

		private void SetVisibleControls()
		{
			if (!Printer.Settings.PrinterSelected)
			{
				movementControlsContainer?.SetEnabled(false);
				fanControlsContainer?.SetEnabled(false);
				macroControlsContainer?.SetEnabled(false);
				calibrationControlsContainer?.SetEnabled(false);
				tuningAdjustmentControlsContainer?.SetEnabled(false);
			}
			else // we at least have a printer selected
			{
				switch (Printer.Connection.CommunicationState)
				{
					case CommunicationStates.Disconnecting:
					case CommunicationStates.ConnectionLost:
					case CommunicationStates.Disconnected:
					case CommunicationStates.AttemptingToConnect:
					case CommunicationStates.FailedToConnect:
						movementControlsContainer?.SetEnabled(false);
						fanControlsContainer?.SetEnabled(false);
						macroControlsContainer?.SetEnabled(false);
						tuningAdjustmentControlsContainer?.SetEnabled(false);
						calibrationControlsContainer?.SetEnabled(false);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget?.SetEnabled(true);
						}
						movementControlsContainer?.jogControls.SetEnabledLevels(false, false);

						break;

					case CommunicationStates.FinishedPrint:
					case CommunicationStates.Connected:
						movementControlsContainer?.SetEnabled(true);
						fanControlsContainer?.SetEnabled(true);
						macroControlsContainer?.SetEnabled(true);
						tuningAdjustmentControlsContainer?.SetEnabled(true);
						calibrationControlsContainer?.SetEnabled(true);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget?.SetEnabled(true);
						}
						movementControlsContainer?.jogControls.SetEnabledLevels(enableBabysteppingMode: false, enableEControls: true);
						break;

					case CommunicationStates.PrintingFromSd:
						movementControlsContainer?.SetEnabled(false);
						fanControlsContainer?.SetEnabled(true);
						macroControlsContainer?.SetEnabled(false);
						tuningAdjustmentControlsContainer?.SetEnabled(false);
						calibrationControlsContainer?.SetEnabled(false);
						break;

					case CommunicationStates.PreparingToPrint:
					case CommunicationStates.Printing:
						switch (Printer.Connection.DetailedPrintingState)
						{
							case DetailedPrintingState.HomingAxis:
							case DetailedPrintingState.HeatingBed:
							case DetailedPrintingState.HeatingExtruder:
							case DetailedPrintingState.Printing:
								fanControlsContainer?.SetEnabled(true);
								macroControlsContainer?.SetEnabled(false);
								tuningAdjustmentControlsContainer?.SetEnabled(true);
								calibrationControlsContainer?.SetEnabled(false);

								foreach (var widget in movementControlsContainer.DisableableWidgets)
								{
									widget?.SetEnabled(false);
								}

								movementControlsContainer?.jogControls.SetEnabledLevels(enableBabysteppingMode: true, enableEControls: false);
								break;

							default:
								throw new NotImplementedException();
						}
						break;

					case CommunicationStates.Paused:
						movementControlsContainer?.SetEnabled(true);
						fanControlsContainer?.SetEnabled(true);
						macroControlsContainer?.SetEnabled(true);
						tuningAdjustmentControlsContainer?.SetEnabled(true);
						calibrationControlsContainer?.SetEnabled(true);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget?.SetEnabled(true);
						}
						movementControlsContainer?.jogControls.SetEnabledLevels(enableBabysteppingMode: false, enableEControls: true);

						break;

					default:
						throw new NotImplementedException();
				}
			}
		}
	}
}
