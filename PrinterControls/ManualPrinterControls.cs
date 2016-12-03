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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl
{
	public class ManualPrinterControls : ScrollableWidget
	{
		static public RootedObjectEventHandler AddPluginControls = new RootedObjectEventHandler();

		private static bool pluginsQueuedToAdd = false;

		private DisableableWidget fanControlsContainer;

		private DisableableWidget macroControlsContainer;

		private MovementControls movementControlsContainer;

		private TemperatureControls temperatureControlsContainer;

		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		private DisableableWidget tuningAdjustmentControlsContainer;

		public ManualPrinterControls()
		{
			ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			AnchorAll();
			AutoScroll = true;

			SetDisplayAttributes();

			FlowLayoutWidget controlsTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
			controlsTopToBottomLayout.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			controlsTopToBottomLayout.VAnchor = Agg.UI.VAnchor.FitToChildren;
			controlsTopToBottomLayout.Name = "ManualPrinterControls.ControlsContainer";
			controlsTopToBottomLayout.Margin = new BorderDouble(0);

			AddMacroControls(controlsTopToBottomLayout);

			AddTemperatureControls(controlsTopToBottomLayout);
			AddMovementControls(controlsTopToBottomLayout);

			FlowLayoutWidget linearPanel = new FlowLayoutWidget();
			linearPanel.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			controlsTopToBottomLayout.AddChild(linearPanel);

			AddFanControls(linearPanel);
			AddAtxPowerControls(linearPanel);

			AddAdjustmentControls(controlsTopToBottomLayout);

			AddChild(controlsTopToBottomLayout);
			AddHandlers();
			SetVisibleControls();

			if (!pluginsQueuedToAdd && ActiveSliceSettings.Instance.GetValue("include_firmware_updater") == "Simple Arduino")
			{
				UiThread.RunOnIdle(AddPlugins);
				pluginsQueuedToAdd = true;
			}
		}

		private event EventHandler unregisterEvents;
		public void AddPlugins()
		{
			AddPluginControls.CallEvents(this, null);
			pluginsQueuedToAdd = false;
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void AddAdjustmentControls(FlowLayoutWidget controlsTopToBottomLayout)
		{
			tuningAdjustmentControlsContainer = new AdjustmentControls();
			controlsTopToBottomLayout.AddChild(tuningAdjustmentControlsContainer);
		}

		private void AddFanControls(FlowLayoutWidget controlsTopToBottomLayout)
		{
			fanControlsContainer = new FanControls();
			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_fan))
			{
				controlsTopToBottomLayout.AddChild(fanControlsContainer);
			}
		}

		private void AddAtxPowerControls(FlowLayoutWidget controlsTopToBottomLayout)
		{
			controlsTopToBottomLayout.AddChild(new PowerControls());
		}

		private void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
		}

		private void AddMacroControls(FlowLayoutWidget controlsTopToBottomLayout)
		{
			macroControlsContainer = new MacroControls();
			controlsTopToBottomLayout.AddChild(macroControlsContainer);
		}

		private void AddMovementControls(FlowLayoutWidget controlsTopToBottomLayout)
		{
			movementControlsContainer = new MovementControls();
			controlsTopToBottomLayout.AddChild(movementControlsContainer);
		}

		private void AddTemperatureControls(FlowLayoutWidget controlsTopToBottomLayout)
		{
			temperatureControlsContainer = new TemperatureControls();
			controlsTopToBottomLayout.AddChild(temperatureControlsContainer);
		}
		private void invalidateWidget()
		{
			this.Invalidate();
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			SetVisibleControls();
			UiThread.RunOnIdle(invalidateWidget);
		}

		private void SetDisplayAttributes()
		{
			HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			VAnchor = Agg.UI.VAnchor.FitToChildren;
		}

		private void SetVisibleControls()
		{
			if (!ActiveSliceSettings.Instance.PrinterSelected)
			{
				// no printer selected
				foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
				{
					extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				}
				temperatureControlsContainer.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				macroControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
			}
			else // we at least have a printer selected
			{
				switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
				{
					case PrinterConnectionAndCommunication.CommunicationStates.Disconnecting:
					case PrinterConnectionAndCommunication.CommunicationStates.ConnectionLost:
					case PrinterConnectionAndCommunication.CommunicationStates.Disconnected:
					case PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect:
					case PrinterConnectionAndCommunication.CommunicationStates.FailedToConnect:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						}
						temperatureControlsContainer.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						macroControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer.jogControls.EnableBabystepping(false);
						movementControlsContainer.OffsetStreamChanged(null, null);

						break;

					case PrinterConnectionAndCommunication.CommunicationStates.FinishedPrint:
					case PrinterConnectionAndCommunication.CommunicationStates.Connected:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						temperatureControlsContainer.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer.jogControls.EnableBabystepping(false);
						movementControlsContainer.OffsetStreamChanged(null, null);
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.PrintingFromSd:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						temperatureControlsContainer.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint:
					case PrinterConnectionAndCommunication.CommunicationStates.Printing:
						switch (PrinterConnectionAndCommunication.Instance.PrintingState)
						{
							case PrinterConnectionAndCommunication.DetailedPrintingState.HomingAxis:
							case PrinterConnectionAndCommunication.DetailedPrintingState.HeatingBed:
							case PrinterConnectionAndCommunication.DetailedPrintingState.HeatingExtruder:
							case PrinterConnectionAndCommunication.DetailedPrintingState.Printing:
								foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
								{
									extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								}
								temperatureControlsContainer.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								//movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
								fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								macroControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
								tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);

								foreach(var widget in movementControlsContainer.DisableableWidgets)
								{
									widget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
								}

								movementControlsContainer.jogControls.EnableBabystepping(true);
								movementControlsContainer.OffsetStreamChanged(null, null);
								break;

							default:
								throw new NotImplementedException();
						}
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.Paused:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						temperatureControlsContainer.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer.jogControls.EnableBabystepping(false);
						movementControlsContainer.OffsetStreamChanged(null, null);

						break;

					default:
						throw new NotImplementedException();
				}
			}
		}
	}
}