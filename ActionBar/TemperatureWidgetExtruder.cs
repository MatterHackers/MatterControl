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
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ActionBar
{
	internal class ControlContentExtruder : FlowLayoutWidget
	{
		private int moveAmount = 1;
		private PrinterConfig printer;

		internal ControlContentExtruder(PrinterConfig printer, int extruderIndex, TextImageButtonFactory buttonFactory)
			: base(FlowDirection.TopToBottom)
		{
			HAnchor = HAnchor.Stretch;

			this.printer = printer;

			// add in any macros for this extruder
			var macroButtons = GetExtruderMacros(extruderIndex, buttonFactory);
			if (macroButtons != null)
			{
				this.AddChild(new SettingsItem("Fillament".Localize(), macroButtons, enforceGutter: false));
			}

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
				printer.Connection.MoveExtruderRelative(moveAmount * -1, printer.Settings.EFeedRate(extruderIndex), extruderIndex);
			};
			buttonContainer.AddChild(retractButton);

			var extrudeButton = buttonFactory.Generate("Extrude".Localize());
			extrudeButton.Name = "Extrude Button";
			extrudeButton.ToolTipText = "Extrude filament".Localize();
			extrudeButton.Margin = 0;
			extrudeButton.Click += (s, e) =>
			{
				printer.Connection.MoveExtruderRelative(moveAmount, printer.Settings.EFeedRate(extruderIndex), extruderIndex);
			};
			buttonContainer.AddChild(extrudeButton);

			this.AddChild(new SettingsItem(
				macroButtons == null ? "Filament".Localize() : "", // Don't put the name if we put in a macro button (it hase the name)
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

			this.AddChild(new SettingsItem("Distance".Localize(), moveButtonsContainer, enforceGutter: false));
		}

		private GuiWidget GetExtruderMacros(int extruderIndex, TextImageButtonFactory buttonFactory)
		{
			MacroUiLocation extruderUiMacros;
			if (Enum.TryParse($"Extruder_{extruderIndex + 1}", out extruderUiMacros))
			{
				var macros = printer.Settings.GetMacros(extruderUiMacros);
				if (macros.Any())
				{
					var row = new FlowLayoutWidget();
					foreach (GCodeMacro macro in macros)
					{
						Button macroButton = buttonFactory.Generate(GCodeMacro.FixMacroName(macro.Name));
						macroButton.Margin = new BorderDouble(left: 5);
						macroButton.Click += (s, e) => macro.Run(printer.Connection);

						row.AddChild(macroButton);
					}
					return row;
				}
			}

			return null;
		}
	}

	internal class TemperatureWidgetHotend : TemperatureWidgetBase
	{
		private TextImageButtonFactory buttonFactory;
		private int hotendIndex = -1;

		private EditableNumberDisplay settingsTemperature;
		private string sliceSettingsNote = "Note: Slice Settings are applied before the print actually starts. Changes while printing will not effect the active print.".Localize();
		private string waitingForExtruderToHeatMessage = "The extruder is currently heating and its target temperature cannot be changed until it reaches {0}°C.\n\nYou can set the starting extruder temperature in 'Slice Settings' -> 'Filament'.\n\n{1}".Localize();

		public TemperatureWidgetHotend(PrinterConfig printer, int hotendIndex, TextImageButtonFactory buttonFactory)
			: base(printer, "150.3°")
		{
			this.Name = $"Hotend {hotendIndex}";
			this.hotendIndex = hotendIndex;
			this.buttonFactory = buttonFactory;
			this.DisplayCurrentTemperature();
			this.ToolTipText = "Current extruder temperature".Localize();

			this.PopupContent = this.GetPopupContent();

			printer.Connection.HotendTemperatureRead.RegisterEvent((s, e) => DisplayCurrentTemperature(), ref unregisterEvents);
		}

		protected override int ActualTemperature => (int)printer.Connection.GetActualHotendTemperature(hotendIndex);
		protected override int TargetTemperature => (int)printer.Connection.GetTargetHotendTemperature(hotendIndex);

		public override void OnClosed(ClosedEventArgs e)
		{
			ActiveSliceSettings.MaterialPresetChanged -= ActiveSliceSettings_MaterialPresetChanged;
			base.OnClosed(e);
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

			GuiWidget hotendRow;
			container.AddChild(hotendRow = new SettingsItem(
				string.Format("{0} {1}", "Hotend".Localize(), hotendIndex + 1),
				new SettingsItem.ToggleSwitchConfig()
				{
					Checked = false,
					ToggleAction = (itemChecked) =>
					{
						if (itemChecked)
						{
							// Set to goal temp
							SetTargetTemperature(settingsTemperature.Value);
						}
						else
						{
							// Turn off extruder
							printer.Connection.SetTargetHotendTemperature(hotendIndex, 0);
						}
					}
				},
				enforceGutter: false));

			CheckBox heatToggle = hotendRow.ChildrenRecursive<CheckBox>().FirstOrDefault();
			heatToggle.Name = "Toggle Heater";

			// put in the temp control
			settingsTemperature = new EditableNumberDisplay(printer.Settings.GetValue<double>(SettingsKey.temperature), "000")
			{
				TextColor = RGBA_Bytes.Black,
				BorderColor = RGBA_Bytes.Black,
				Name = "Temperature Input"
			};
			settingsTemperature.ValueChanged += (s, e) =>
			{
				if (heatToggle.Checked)
				{
					SetTargetTemperature(settingsTemperature.Value);
					if (settingsTemperature.Value == 0)
					{
						heatToggle.Checked = false;
					}
				}
			};
			container.AddChild(new SettingsItem(
				"Temperature".Localize(),
				settingsTemperature, enforceGutter: false));

			// add in the temp graph
			Action fillGraph = null;
			var graph = new DataViewGraph()
			{
				DynamiclyScaleRange = false,
				MinValue = 0,
				ShowGoal = true,
				GoalColor = ActiveTheme.Instance.PrimaryAccentColor,
				GoalValue = settingsTemperature.Value,
				MaxValue = 280, // could come from some profile value in the future
				Width = widget.Width - 20,
				Height = 35, // this works better if it is a common multiple of the Width
			};
			settingsTemperature.ValueChanged += (s, e) =>
			{
				graph.GoalValue = settingsTemperature.Value;
			};
			fillGraph = () =>
			{
				graph.AddData(this.ActualTemperature);
				if (!this.HasBeenClosed)
				{
					UiThread.RunOnIdle(fillGraph, 1);
				}
			};

			UiThread.RunOnIdle(fillGraph);
			container.AddChild(graph);

			// put in the material selector
			var presetsSelector = new PresetSelectorWidget(string.Format($"{"Material".Localize()} {hotendIndex + 1}"), RGBA_Bytes.Transparent, NamedSettingsLayers.Material, hotendIndex, true)
			{
				Margin = 0,
				BackgroundColor = RGBA_Bytes.Transparent,
				HAnchor = HAnchor.Absolute,
				Width = 150
			};

			presetsSelector.DropDownList.SelectionChanged += (s, e) =>
			{
				// delay this for an update so the slice setting can get updated first
				UiThread.RunOnIdle(() =>
				{
					settingsTemperature.Value = printer.Settings.GetValue<double>(SettingsKey.temperature);
				});
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
				dropList.TextColor = RGBA_Bytes.Black;
			}

			container.AddChild(new SettingsItem("Material".Localize(), presetsSelector, enforceGutter: false));

			// put in the actual extruder controls
			bool shareTemp = printer.Settings.GetValue<bool>(SettingsKey.extruders_share_temperature);
			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			if (shareTemp && extruderCount > 1)
			{
				for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
				{
					container.AddChild(new HorizontalLine()
					{
						Margin = new BorderDouble(0, 5, 0, 0)
					});

					container.AddChild(new TextWidget("Extruder".Localize() + " " + (extruderIndex + 1).ToString())
					{
						AutoExpandBoundsToText = true,
						TextColor = RGBA_Bytes.Black,
						HAnchor = HAnchor.Left,
					});
					container.AddChild(new ControlContentExtruder(printer, extruderIndex, buttonFactory));
				}
			}
			else
			{
				container.AddChild(new ControlContentExtruder(printer, hotendIndex, buttonFactory));
			}

			ActiveSliceSettings.MaterialPresetChanged += ActiveSliceSettings_MaterialPresetChanged;

			return widget;
		}

		protected override void SetTargetTemperature(double targetTemp)
		{
			double goalTemp = (int)(targetTemp + .5);
			if (printer.Connection.PrinterIsPrinting
				&& printer.Connection.DetailedPrintingState == DetailedPrintingState.HeatingExtruder
				&& goalTemp != printer.Connection.GetTargetHotendTemperature(hotendIndex))
			{
				string message = string.Format(waitingForExtruderToHeatMessage, printer.Connection.GetTargetHotendTemperature(hotendIndex), sliceSettingsNote);
				StyledMessageBox.ShowMessageBox(null, message, "Waiting For Extruder To Heat".Localize());
			}
			else
			{
				printer.Connection.SetTargetHotendTemperature(hotendIndex, (int)(targetTemp + .5));
			}
		}

		private void ActiveSliceSettings_MaterialPresetChanged(object sender, EventArgs e)
		{
			if (settingsTemperature != null && printer.Settings != null)
			{
				settingsTemperature.Text = printer.Settings.GetValue(SettingsKey.temperature);
			}
		}
	}
}