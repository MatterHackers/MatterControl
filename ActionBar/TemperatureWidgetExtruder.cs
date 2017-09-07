/*
Copyright (c) 2017, Kevin Pope, John Lewin
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

namespace MatterHackers.MatterControl.ActionBar
{
	internal class TemperatureWidgetExtruder : GuiWidget
	{

	}

	internal class TemperatureWidgetHotend : TemperatureWidgetBase
	{
		private int extruderIndex = -1;
		private int moveAmount = 1;

		private string sliceSettingsNote = "Note: Slice Settings are applied before the print actually starts. Changes while printing will not effect the active print.".Localize();
		private string waitingForExtruderToHeatMessage = "The extruder is currently heating and its target temperature cannot be changed until it reaches {0}°C.\n\nYou can set the starting extruder temperature in 'Slice Settings' -> 'Filament'.\n\n{1}".Localize();

		private TextImageButtonFactory buttonFactory;

		private EditableNumberDisplay settingsTemperature;

		public TemperatureWidgetHotend(PrinterConnection printerConnection, int extruderIndex, TextImageButtonFactory buttonFactory)
			: base(printerConnection, "150.3°")
		{
			this.extruderIndex = extruderIndex;
			this.buttonFactory = buttonFactory;
			this.DisplayCurrentTemperature();
			this.ToolTipText = "Current extruder temperature".Localize();

			this.PopupContent = this.GetPopupContent();

			printerConnection.ExtruderTemperatureRead.RegisterEvent((s, e) => DisplayCurrentTemperature(), ref unregisterEvents);
		}

		protected override int TargetTemperature => (int)printerConnection.GetTargetExtruderTemperature(extruderIndex);

		protected override int ActualTemperature => (int)printerConnection.GetActualExtruderTemperature(extruderIndex);

		protected override void SetTargetTemperature()
		{
			double targetTemp;
			if (double.TryParse(printerConnection.PrinterSettings.GetValue(SettingsKey.temperature), out targetTemp))
			{
				double goalTemp = (int)(targetTemp + .5);
				if (printerConnection.PrinterIsPrinting
					&& printerConnection.DetailedPrintingState == DetailedPrintingState.HeatingExtruder
					&& goalTemp != printerConnection.GetTargetExtruderTemperature(extruderIndex))
				{
					string message = string.Format(waitingForExtruderToHeatMessage, printerConnection.GetTargetExtruderTemperature(extruderIndex), sliceSettingsNote);
					StyledMessageBox.ShowMessageBox(null, message, "Waiting For Extruder To Heat".Localize());
				}
				else
				{
					printerConnection.SetTargetExtruderTemperature(extruderIndex, (int)(targetTemp + .5));
				}
			}
		}

		protected override GuiWidget GetPopupContent()
		{
			var widget = new IgnoredPopupWidget()
			{
				Width = 300,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
				BackgroundColor = RGBA_Bytes.White,
				Padding = new BorderDouble(12, 5, 12, 0)
			};

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				BackgroundColor = RGBA_Bytes.White
			};
			widget.AddChild(container);

			container.AddChild(new SettingsItem(
				string.Format("{0} {1}", "Hot End".Localize(), extruderIndex + 1),
				new SettingsItem.ToggleSwitchConfig()
				{
					Checked = false,
					ToggleAction = (itemChecked) =>
					{
						if (itemChecked)
						{
							// Set to goal temp
							SetTargetTemperature();
						}
						else
						{
							// Turn off extruder
							printerConnection.SetTargetExtruderTemperature(extruderIndex, 0);
						}
					}
				},
				enforceGutter: false));

			// put in the temp control
			settingsTemperature = new EditableNumberDisplay(printerConnection.PrinterSettings.GetValue(SettingsKey.temperature), "000");
			container.AddChild(new SettingsItem(
				"Temperature".Localize(),
				settingsTemperature, enforceGutter: false));

			// add in the temp graph
			Action fillGraph = null;
			var graph = new DataViewGraph()
			{
				Width = widget.Width - 20,
				Height = 20,
			};
			fillGraph = () =>
			{
				graph.AddData(this.ActualTemperature);
				if (!graph.HasBeenClosed)
				{
					UiThread.RunOnIdle(fillGraph, 1);
				}
			};

			UiThread.RunOnIdle(fillGraph);

			container.AddChild(graph);

			// put in the material selector
			var presetsSelector = new PresetSelectorWidget(printerConnection, string.Format($"{"Material".Localize()} {extruderIndex + 1}"), RGBA_Bytes.Transparent, NamedSettingsLayers.Material, extruderIndex)
			{
				Margin = 0,
				BackgroundColor = RGBA_Bytes.Transparent,
				HAnchor = HAnchor.Absolute,
				Width = 150
			};

			this.Width = 150;

			// HACK: remove undesired item
			var label = presetsSelector.Children<TextWidget>().FirstOrDefault();
			label.Close();

			var pulldownContainer = presetsSelector.FindNamedChildRecursive("Preset Pulldown Container");
			if (pulldownContainer != null)
			{
				pulldownContainer.Padding = 0;
			}

			var dropList = presetsSelector.FindNamedChildRecursive("Material") as DropDownList;
			if (dropList != null)
			{
				dropList.TextColor = buttonFactory.normalTextColor;
			}

			container.AddChild(new SettingsItem("Material".Localize(), presetsSelector, enforceGutter: false));

			// add in any macros for this extruder
			container.AddChild(new SettingsItem("Change".Localize(), AddExtruderMacros(extruderIndex), enforceGutter: false));

			// Add the Extrude buttons
			var moveButtonFactory = ApplicationController.Instance.Theme.MicroButtonMenu;

			var buttonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};

			var retractButton = buttonFactory.Generate("Retract".Localize());
			retractButton.ToolTipText = "Retract filament".Localize();
			retractButton.Margin = new BorderDouble(8, 0);
			retractButton.Click += (s, e) =>
			{
				printerConnection.MoveExtruderRelative(moveAmount * -1, printerConnection.PrinterSettings.EFeedRate(extruderIndex), extruderIndex);
			};
			buttonContainer.AddChild(retractButton);

			var extrudeButton = buttonFactory.Generate("Extrude".Localize());
			extrudeButton.ToolTipText = "Extrude filament".Localize();
			extrudeButton.Margin = 0;
			extrudeButton.Click += (s, e) =>
			{
				printerConnection.MoveExtruderRelative(moveAmount, printerConnection.PrinterSettings.EFeedRate(extruderIndex), extruderIndex);
			};
			buttonContainer.AddChild(extrudeButton);

			container.AddChild(new SettingsItem(
				"Extrude".Localize(),
				buttonContainer, 
				enforceGutter: false));

			var moveButtonsContainer = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Fit,
				Margin = new BorderDouble(0, 3)
			};

			RadioButton oneButton = moveButtonFactory.GenerateRadioButton("1");
			oneButton.VAnchor = VAnchor.Center;
			oneButton.CheckedStateChanged += (s, e) =>
			{
				if (oneButton.Checked)
				{
					moveAmount = 1;
				}
			};
			moveButtonsContainer.AddChild(oneButton);

			RadioButton tenButton = moveButtonFactory.GenerateRadioButton("10");
			tenButton.VAnchor = VAnchor.Center;
			tenButton.CheckedStateChanged += (s, e) =>
			{
				if (tenButton.Checked)
				{
					moveAmount = 10;
				}
			};
			moveButtonsContainer.AddChild(tenButton);

			RadioButton oneHundredButton = moveButtonFactory.GenerateRadioButton("100");
			oneHundredButton.VAnchor = VAnchor.Center;
			oneHundredButton.CheckedStateChanged += (s, e) =>
			{
				if (oneHundredButton.Checked)
				{
					moveAmount = 100;
				}
			};
			moveButtonsContainer.AddChild(oneHundredButton);

			tenButton.Checked = true;

			moveButtonsContainer.AddChild(new TextWidget("mm", textColor: buttonFactory.normalTextColor, pointSize: 8)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(3, 0)
			});

			container.AddChild(new SettingsItem("Distance".Localize(), moveButtonsContainer, enforceGutter: false));

			ActiveSliceSettings.MaterialPresetChanged += ActiveSliceSettings_MaterialPresetChanged;

			return widget;
		}

		private GuiWidget AddExtruderMacros(int extruderIndex)
		{
			var row = new FlowLayoutWidget();

			MacroUiLocation extruderUiMacros;
			if (Enum.TryParse($"Extruder_{extruderIndex+1}", out extruderUiMacros))
			{
				foreach (GCodeMacro macro in printerConnection.PrinterSettings.GetMacros(extruderUiMacros))
				{
					Button macroButton = buttonFactory.Generate(GCodeMacro.FixMacroName(macro.Name));
					macroButton.Margin = new BorderDouble(left: 5);
					macroButton.Click += (s, e) => macro.Run(printerConnection);

					row.AddChild(macroButton);
				}
			}

			return row;
		}

		private void ActiveSliceSettings_MaterialPresetChanged(object sender, EventArgs e)
		{
			if (settingsTemperature != null && printerConnection.PrinterSettings != null)
			{
				settingsTemperature.Text = printerConnection.PrinterSettings.GetValue(SettingsKey.temperature);
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			ActiveSliceSettings.MaterialPresetChanged -= ActiveSliceSettings_MaterialPresetChanged;
			base.OnClosed(e);
		}
	}
}