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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
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

		public void AddPlugins()
		{
			AddPluginControls.CallEvents(this, null);
			pluginsQueuedToAdd = false;
		}

		public ManualPrinterControls()
		{
			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;
			AnchorAll();
			if (UserSettings.Instance.IsTouchScreen)
			{
				AddChild(new ManualPrinterControlsTouchScreen());
			}
			else
			{
				AddChild(new ManualPrinterControlsDesktop());
			}

			if (!pluginsQueuedToAdd && ActiveSliceSettings.Instance.GetValue("include_firmware_updater") == "Simple Arduino")
			{
				UiThread.RunOnIdle(AddPlugins);
				pluginsQueuedToAdd = true;
			}
		}
	}

	public class ManualPrinterControlsDesktop : ScrollableWidget
	{
		private DisableableWidget fanControlsContainer;

		private DisableableWidget macroControlsContainer;
		private DisableableWidget actionControlsContainer;

		private MovementControls movementControlsContainer;

		private TemperatureControls temperatureControlsContainer;

		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		private DisableableWidget tuningAdjustmentControlsContainer;

		private EventHandler unregisterEvents;

		public ManualPrinterControlsDesktop()
		{
			ScrollArea.HAnchor |= HAnchor.ParentLeftRight;
			AnchorAll();
			AutoScroll = true;

			HAnchor = HAnchor.Max_FitToChildren_ParentWidth;
			VAnchor = VAnchor.ParentBottomTop;

			var controlsTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Max_FitToChildren_ParentWidth,
				VAnchor = VAnchor.FitToChildren,
				Name = "ManualPrinterControls.ControlsContainer",
				Margin = new BorderDouble(0)
			};
			AddActionControls(controlsTopToBottomLayout);

			AddTemperatureControls(controlsTopToBottomLayout);
			AddMovementControls(controlsTopToBottomLayout);

			if (!ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_hardware_leveling))
			{
				controlsTopToBottomLayout.AddChild(new CalibrationSettingsWidget(ApplicationController.Instance.Theme.BreadCrumbButtonFactory));
			}

			AddMacroControls(controlsTopToBottomLayout);

			var linearPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight
			};
			controlsTopToBottomLayout.AddChild(linearPanel);

			AddFanControls(linearPanel);
			AddAtxPowerControls(linearPanel);

			AddAdjustmentControls(controlsTopToBottomLayout);

			AddChild(controlsTopToBottomLayout);

			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnection.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

			SetVisibleControls();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void AddAdjustmentControls(FlowLayoutWidget controlsTopToBottomLayout)
		{
			tuningAdjustmentControlsContainer = new AdjustmentControls();
			controlsTopToBottomLayout.AddChild(tuningAdjustmentControlsContainer);

			// this is a hack to make the layout engine fire again for this control
			UiThread.RunOnIdle(() => tuningAdjustmentControlsContainer.Width = tuningAdjustmentControlsContainer.Width + 1);
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
#if !__ANDROID__
			controlsTopToBottomLayout.AddChild(new PowerControls());
#endif
		}

		private void AddActionControls(FlowLayoutWidget controlsTopToBottomLayout)
		{
			actionControlsContainer = new ActionControls();
			controlsTopToBottomLayout.AddChild(actionControlsContainer);
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
		
		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			SetVisibleControls();
			UiThread.RunOnIdle(this.Invalidate);
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
				temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
			}
			else // we at least have a printer selected
			{
				switch (PrinterConnection.Instance.CommunicationState)
				{
					case CommunicationStates.Disconnecting:
					case CommunicationStates.ConnectionLost:
					case CommunicationStates.Disconnected:
					case CommunicationStates.AttemptingToConnect:
					case CommunicationStates.FailedToConnect:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						}
						temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer?.jogControls.SetEnabledLevels(false, false);

						break;

					case CommunicationStates.FinishedPrint:
					case CommunicationStates.Connected:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer?.jogControls.SetEnabledLevels(false, true);
						break;

					case CommunicationStates.PrintingFromSd:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						break;

					case CommunicationStates.PreparingToPrint:
					case CommunicationStates.Printing:
						switch (PrinterConnection.Instance.PrintingState)
						{
							case DetailedPrintingState.HomingAxis:
							case DetailedPrintingState.HeatingBed:
							case DetailedPrintingState.HeatingExtruder:
							case DetailedPrintingState.Printing:
								foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
								{
									extruderTemperatureControlWidget?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								}
								temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
								actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
								tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);

								foreach(var widget in movementControlsContainer.DisableableWidgets)
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
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);

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

	public class ManualPrinterControlsTouchScreen : TabControl
	{
		event EventHandler unregisterEvents;

		TemperatureControls temperatureControlsContainer;
		MovementControls movementControlsContainer;
		DisableableWidget fanControlsContainer;
		DisableableWidget tuningAdjustmentControlsContainer;
		DisableableWidget terminalControlsContainer;
		DisableableWidget macroControlsContainer;
		DisableableWidget actionControlsContainer;

		int TabTextSize;

		TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		public ManualPrinterControlsTouchScreen()
			: base(Orientation.Vertical)
		{
			RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;

			this.TabBar.BackgroundColor = ActiveTheme.Instance.TransparentLightOverlay;
			this.TabBar.BorderColor = RGBA_Bytes.Transparent;
			this.TabBar.Margin = new BorderDouble(0);
			this.TabBar.Padding = new BorderDouble(4, 4);

			this.AnchorAll();
			this.VAnchor |= VAnchor.FitToChildren;

			this.Margin = new BorderDouble(0);
			this.TabTextSize = 13;

			// add action tab
			{
				GuiWidget actionContainerContainer = new GuiWidget();
				actionContainerContainer.Padding = new BorderDouble(6);
				actionContainerContainer.AnchorAll();

				actionControlsContainer = new ActionControls();
				actionControlsContainer.VAnchor = VAnchor.ParentTop;
				if (ActiveSliceSettings.Instance.ActionMacros().Any())
				{
					actionContainerContainer.AddChild(actionControlsContainer);
				}

				if (ActiveSliceSettings.Instance.ActionMacros().Any())
				{
					TabPage actionTabPage = new TabPage(actionContainerContainer, "Actions".Localize().ToUpper());
					this.AddTab(new SimpleTextTabWidget(actionTabPage, "Actions Tab", TabTextSize,
						ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
				}
			}

			// add temperature tab
			{
				GuiWidget temperatureContainerContainer = new GuiWidget();
				temperatureContainerContainer.Padding = new BorderDouble(6);
				temperatureContainerContainer.AnchorAll();

				temperatureControlsContainer = new TemperatureControls();
				temperatureControlsContainer.VAnchor |= VAnchor.ParentTop;

				temperatureContainerContainer.AddChild(temperatureControlsContainer);

				TabPage temperatureTabPage = new TabPage(temperatureContainerContainer, "Temperature".Localize().ToUpper());
				this.AddTab(new SimpleTextTabWidget(temperatureTabPage, "Temperature Tab", TabTextSize,
					ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
			}

			// add movement tab
			{
				GuiWidget movementContainerContainer = new GuiWidget();
				movementContainerContainer.Padding = new BorderDouble(6);
				movementContainerContainer.AnchorAll();

				movementControlsContainer = new MovementControls();
				movementControlsContainer.VAnchor = VAnchor.ParentTop;

				movementContainerContainer.AddChild(movementControlsContainer);

				TabPage movementTabPage = new TabPage(movementContainerContainer, "Movement".Localize().ToUpper());
				this.AddTab(new SimpleTextTabWidget(movementTabPage, "Movement Tab", TabTextSize,
					ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
			}

			// add macro tab
			{
				GuiWidget macrosContainerContainer = new GuiWidget();
				macrosContainerContainer.Padding = new BorderDouble(6);
				macrosContainerContainer.AnchorAll();

				macroControlsContainer = new MacroControls();
				macroControlsContainer.VAnchor |= VAnchor.ParentTop;
				macrosContainerContainer.AddChild(macroControlsContainer);


				TabPage macrosTabPage = new TabPage(macrosContainerContainer, "Macros".Localize().ToUpper());
				this.AddTab(new SimpleTextTabWidget(macrosTabPage, "Macros Tab", TabTextSize,
					ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
			}

			if (ActiveSliceSettings.Instance.GetValue<bool>("has_fan"))
			{
				// add fan tab
				GuiWidget fanContainerContainer = new GuiWidget();
				fanContainerContainer.Padding = new BorderDouble(6);
				fanContainerContainer.AnchorAll();

				fanControlsContainer = new FanControls();
				fanControlsContainer.VAnchor = VAnchor.ParentTop;

				fanContainerContainer.AddChild(fanControlsContainer);

				TabPage fanTabPage = new TabPage(fanContainerContainer, "Fan Controls".Localize().ToUpper());
				this.AddTab(new SimpleTextTabWidget(fanTabPage, "Fan Controls Tab", TabTextSize,
						ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
			}

			// add tunning tab
			{
				GuiWidget tuningContainerContainer = new GuiWidget();
				tuningContainerContainer.Padding = new BorderDouble(6);
				tuningContainerContainer.AnchorAll();

				tuningAdjustmentControlsContainer = new AdjustmentControls();
				tuningAdjustmentControlsContainer.VAnchor = VAnchor.ParentTop;

				tuningContainerContainer.AddChild(tuningAdjustmentControlsContainer);

				TabPage tuningTabPage = new TabPage(tuningContainerContainer, "Tuning Adjust".Localize().ToUpper());
				this.AddTab(new SimpleTextTabWidget(tuningTabPage, "Tuning Tab", TabTextSize,
					ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
			}

			// add terminal tab
			{
				GuiWidget terminalContainerContainer = new GuiWidget();
				terminalContainerContainer.Padding = new BorderDouble(6);
				terminalContainerContainer.AnchorAll();

				terminalControlsContainer = new TerminalControls();
				terminalControlsContainer.VAnchor |= VAnchor.ParentBottomTop;

				terminalContainerContainer.AddChild(terminalControlsContainer);

				TabPage terminalTabPage = new TabPage(terminalContainerContainer, "Terminal".Localize().ToUpper());
				this.AddTab(new SimpleTextTabWidget(terminalTabPage, "Terminal Tab", TabTextSize,
					ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
			}

			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnection.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

			SetVisibleControls();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);

			base.OnClosed(e);
		}

		private void SetVisibleControls()
		{
			if (ActiveSliceSettings.Instance == null)
			{
				// no printer selected
				foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
				{
					extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				}
				temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);

				macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
			}
			else // we at least have a printer selected
			{
				switch (PrinterConnection.Instance.CommunicationState)
				{
					case CommunicationStates.Disconnecting:
					case CommunicationStates.ConnectionLost:
					case CommunicationStates.Disconnected:
					case CommunicationStates.AttemptingToConnect:
					case CommunicationStates.FailedToConnect:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						}
						temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer.jogControls.SetEnabledLevels(false, false);

						break;

					case CommunicationStates.FinishedPrint:
					case CommunicationStates.Connected:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);

						foreach (var widget in movementControlsContainer.DisableableWidgets)
						{
							widget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						movementControlsContainer.jogControls.SetEnabledLevels(false, true);

						break;

					case CommunicationStates.PrintingFromSd:
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
						actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
						break;

					case CommunicationStates.PreparingToPrint:
					case CommunicationStates.Printing:
						switch (PrinterConnection.Instance.PrintingState)
						{
							case DetailedPrintingState.HomingAxis:
							case DetailedPrintingState.HeatingBed:
							case DetailedPrintingState.HeatingExtruder:
							case DetailedPrintingState.Printing:
								foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
								{
									extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								}
								temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
								macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
								actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);


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
						foreach (DisableableWidget extruderTemperatureControlWidget in temperatureControlsContainer.ExtruderWidgetContainers)
						{
							extruderTemperatureControlWidget?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						}
						temperatureControlsContainer?.BedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						movementControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						fanControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						macroControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						actionControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
						tuningAdjustmentControlsContainer?.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);

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

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			SetVisibleControls();
			UiThread.RunOnIdle(this.Invalidate);
		}
	}
}
