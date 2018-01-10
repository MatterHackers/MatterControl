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
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class ManualPrinterControls : GuiWidget
	{
		static public RootedObjectEventHandler AddPluginControls = new RootedObjectEventHandler();

		private static bool pluginsQueuedToAdd = false;
		private PrinterConfig printer;

		public ManualPrinterControls(PrinterConfig printer)
		{
			this.printer = printer;
			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;
			this.AnchorAll();
			this.AddChild(new ManualPrinterControlsDesktop(printer));
		}

		public override void OnLoad(EventArgs args)
		{
			if (!pluginsQueuedToAdd && printer.Settings.GetValue(SettingsKey.include_firmware_updater) == "Simple Arduino")
			{
				UiThread.RunOnIdle(() =>
				{
					AddPluginControls.CallEvents(this, null);
					pluginsQueuedToAdd = false;
				});
				pluginsQueuedToAdd = true;
			}

			base.OnLoad(args);
		}
	}

	public class ManualPrinterControlsDesktop : ScrollableWidget
	{
		private DisableableWidget fanControlsContainer;
		private DisableableWidget macroControlsContainer;
		private DisableableWidget tuningAdjustmentControlsContainer;
		private MovementControls movementControlsContainer;
		private DisableableWidget calibrationControlsContainer;

		private EventHandler unregisterEvents;
		private PrinterConfig printer;

		public ManualPrinterControlsDesktop(PrinterConfig printer)
		{
			var theme = ApplicationController.Instance.Theme;

			this.printer = printer;
			this.ScrollArea.HAnchor |= HAnchor.Stretch;
			this.AnchorAll();
			this.AutoScroll = true;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(8, 0, theme.ToolbarPadding.Right, 6);

			int headingPointSize = theme.H1PointSize;

			var controlsTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.MaxFitOrStretch,
				VAnchor = VAnchor.Fit,
				Name = "ManualPrinterControls.ControlsContainer",
				Margin = new BorderDouble(0)
			};
			this.AddChild(controlsTopToBottomLayout);

			movementControlsContainer = new MovementControls(printer, theme);
			movementControlsContainer.Margin = new BorderDouble(top: 6);
			controlsTopToBottomLayout.AddChild(movementControlsContainer);

			if (!printer.Settings.GetValue<bool>(SettingsKey.has_hardware_leveling))
			{
				calibrationControlsContainer = new CalibrationSettingsWidget(printer, theme);
				controlsTopToBottomLayout.AddChild(calibrationControlsContainer);
			}

			macroControlsContainer = new MacroControls(printer, theme);
			controlsTopToBottomLayout.AddChild(macroControlsContainer);

			var linearPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch
			};
			controlsTopToBottomLayout.AddChild(linearPanel);

			fanControlsContainer = new FanControls(printer.Connection, theme);
			if (printer.Settings.GetValue<bool>(SettingsKey.has_fan))
			{
				controlsTopToBottomLayout.AddChild(fanControlsContainer);
			}

#if !__ANDROID__
			controlsTopToBottomLayout.AddChild(new PowerControls(printer, headingPointSize));
#endif
			tuningAdjustmentControlsContainer = new AdjustmentControls(printer, theme);
			controlsTopToBottomLayout.AddChild(tuningAdjustmentControlsContainer);

			// HACK: this is a hack to make the layout engine fire again for this control
			UiThread.RunOnIdle(() => tuningAdjustmentControlsContainer.Width = tuningAdjustmentControlsContainer.Width + 1);

			printer.Connection.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			printer.Connection.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

			SetVisibleControls();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			SetVisibleControls();
			UiThread.RunOnIdle(this.Invalidate);
		}

		private void SetVisibleControls()
		{
			if (!printer.Settings.PrinterSelected)
			{
				movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				calibrationControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
			}
			else // we at least have a printer selected
			{
				switch (printer.Connection.CommunicationState)
				{
					case CommunicationStates.Disconnecting:
					case CommunicationStates.ConnectionLost:
					case CommunicationStates.Disconnected:
					case CommunicationStates.AttemptingToConnect:
					case CommunicationStates.FailedToConnect:
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						calibrationControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer?.jogControls.SetEnabledLevels(false, false);

						break;

					case CommunicationStates.FinishedPrint:
					case CommunicationStates.Connected:
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						calibrationControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer?.jogControls.SetEnabledLevels(false, true);
						break;

					case CommunicationStates.PrintingFromSd:
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						calibrationControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						break;

					case CommunicationStates.PreparingToPrint:
					case CommunicationStates.Printing:
						switch (printer.Connection.DetailedPrintingState)
						{
							case DetailedPrintingState.HomingAxis:
							case DetailedPrintingState.HeatingBed:
							case DetailedPrintingState.HeatingExtruder:
							case DetailedPrintingState.Printing:
								fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
								tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								calibrationControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);

								foreach (var widget in movementControlsContainer.DisableableWidgets)
								{
									widget?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
								}

								movementControlsContainer?.jogControls.SetEnabledLevels(true, false);
								break;

							default:
								throw new NotImplementedException();
						}
						break;

					case CommunicationStates.Paused:
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						calibrationControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer?.jogControls.SetEnabledLevels(false, true);

						break;

					default:
						throw new NotImplementedException();
				}
			}
		}
	}
}
