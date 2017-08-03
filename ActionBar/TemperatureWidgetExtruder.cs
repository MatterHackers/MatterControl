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
	internal class TemperatureWidgetExtruder : TemperatureWidgetBase
	{
		// Extruder widget is hard-wired to extruder 0
		private const int extruderIndex = 0;
		private int moveAmount = 1;

		private string sliceSettingsNote = "Note: Slice Settings are applied before the print actually starts. Changes while printing will not effect the active print.".Localize();
		private string waitingForExtruderToHeatMessage = "The extruder is currently heating and its target temperature cannot be changed until it reaches {0}°C.\n\nYou can set the starting extruder temperature in 'Slice Settings' -> 'Filament'.\n\n{1}".Localize();

		private TextImageButtonFactory buttonFactory;

		private TextWidget settingsTemperature;

		public TemperatureWidgetExtruder(TextImageButtonFactory buttonFactory)
			: base("150.3°")
		{
			this.buttonFactory = buttonFactory;
			this.DisplayCurrentTemperature();
			this.ToolTipText = "Current extruder temperature".Localize();

			this.PopupContent = this.GetPopupContent();

			PrinterConnection.Instance.ExtruderTemperatureRead.RegisterEvent((s, e) => DisplayCurrentTemperature(), ref unregisterEvents);
		}

		protected override int TargetTemperature => (int)PrinterConnection.Instance.GetTargetExtruderTemperature(extruderIndex);

		protected override int ActualTemperature => (int)PrinterConnection.Instance.GetActualExtruderTemperature(extruderIndex);

		protected override void SetTargetTemperature()
		{
			double targetTemp;
			if (double.TryParse(ActiveSliceSettings.Instance.GetValue(SettingsKey.temperature), out targetTemp))
			{
				double goalTemp = (int)(targetTemp + .5);
				if (PrinterConnection.Instance.PrinterIsPrinting
					&& PrinterConnection.Instance.PrintingState == DetailedPrintingState.HeatingExtruder
					&& goalTemp != PrinterConnection.Instance.GetTargetExtruderTemperature(extruderIndex))
				{
					string message = string.Format(waitingForExtruderToHeatMessage, PrinterConnection.Instance.GetTargetExtruderTemperature(extruderIndex), sliceSettingsNote);
					StyledMessageBox.ShowMessageBox(null, message, "Waiting For Extruder To Heat".Localize());
				}
				else
				{
					PrinterConnection.Instance.SetTargetExtruderTemperature(extruderIndex, (int)(targetTemp + .5));
				}
			}
		}

		protected override GuiWidget GetPopupContent()
		{
			var widget = new IgnoredPopupWidget()
			{
				Width = 300,
				HAnchor = HAnchor.AbsolutePosition,
				VAnchor = VAnchor.FitToChildren,
				BackgroundColor = RGBA_Bytes.White,
				Padding = new BorderDouble(12, 5, 12, 0)
			};

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.FitToChildren,
				BackgroundColor = RGBA_Bytes.White
			};

			container.AddChild(new SettingsItem(
				string.Format("{0} {1}", "HotEnd".Localize(), extruderIndex + 1),
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
							PrinterConnection.Instance.SetTargetExtruderTemperature(extruderIndex, 0);
						}
					}
				}, 
				enforceGutter: false));

			var presetsSelector = new PresetSelectorWidget(string.Format($"{"Material".Localize()} {extruderIndex + 1}"), RGBA_Bytes.Transparent, NamedSettingsLayers.Material, extruderIndex)
			{
				Margin = 0,
				BackgroundColor = RGBA_Bytes.Transparent,
				HAnchor = HAnchor.AbsolutePosition,
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

			settingsTemperature = new TextWidget(ActiveSliceSettings.Instance.GetValue(SettingsKey.temperature))
			{
				AutoExpandBoundsToText = true
			};

			container.AddChild(new SettingsItem(
				"Temperature".Localize(),
				settingsTemperature,
				enforceGutter: false));

			widget.AddChild(container);

			// Extrude buttons {{

			var moveButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions()
			{
				FixedHeight = 20 * GuiWidget.DeviceScale,
				FixedWidth = 30 * GuiWidget.DeviceScale,
				FontSize = 8,
				Margin = new BorderDouble(2, 0),
				CheckedBorderColor = buttonFactory.normalTextColor,

				Normal = new ButtonOptionSection()
				{
					TextColor = buttonFactory.normalTextColor,
					FillColor = buttonFactory.normalFillColor,
				},
				Hover = new ButtonOptionSection()
				{
					FillColor = buttonFactory.hoverFillColor,
				},
				Pressed = new ButtonOptionSection()
				{
					FillColor = buttonFactory.pressedFillColor,
					TextColor = buttonFactory.pressedTextColor
				}
			});

			var buttonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.FitToChildren,
				VAnchor = VAnchor.FitToChildren
			};

			var retractButton = buttonFactory.Generate("Retract".Localize());
			retractButton.ToolTipText = "Retract filament".Localize();
			retractButton.Margin = new BorderDouble(8, 0);
			retractButton.Click += (s, e) =>
			{
				PrinterConnection.Instance.MoveExtruderRelative(moveAmount * -1, MovementControls.EFeedRate(extruderIndex), extruderIndex);
			};
			buttonContainer.AddChild(retractButton);

			var extrudeButton = buttonFactory.Generate("Extrude".Localize());
			extrudeButton.ToolTipText = "Extrude filament".Localize();
			extrudeButton.Margin = 0;
			extrudeButton.Click += (s, e) =>
			{
				PrinterConnection.Instance.MoveExtruderRelative(moveAmount, MovementControls.EFeedRate(extruderIndex), extruderIndex);
			};
			buttonContainer.AddChild(extrudeButton);

			container.AddChild(new SettingsItem(
				string.Format("{0} {1}", "Extruder".Localize(), extruderIndex + 1),
				buttonContainer, 
				enforceGutter: false));

			var moveButtonsContainer = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.FitToChildren,
				HAnchor = HAnchor.FitToChildren,
				Margin = new BorderDouble(0, 3)
			};

			RadioButton oneButton = moveButtonFactory.GenerateRadioButton("1");
			oneButton.VAnchor = VAnchor.ParentCenter;
			oneButton.CheckedStateChanged += (s, e) =>
			{
				if (oneButton.Checked)
				{
					moveAmount = 1;
				}
			};
			moveButtonsContainer.AddChild(oneButton);

			RadioButton tenButton = moveButtonFactory.GenerateRadioButton("10");
			tenButton.VAnchor = VAnchor.ParentCenter;
			tenButton.CheckedStateChanged += (s, e) =>
			{
				if (tenButton.Checked)
				{
					moveAmount = 10;
				}
			};
			moveButtonsContainer.AddChild(tenButton);

			RadioButton oneHundredButton = moveButtonFactory.GenerateRadioButton("100");
			oneHundredButton.VAnchor = VAnchor.ParentCenter;
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
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(3, 0)
			});

			container.AddChild(new SettingsItem("Distance".Localize(), moveButtonsContainer, enforceGutter: false));

			var graph = new DataViewGraph()
			{
				Width = widget.Width - 20,
				Height = 20,
			};

			Action fillGraph = null;
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

			// Extrude buttons }}

			ActiveSliceSettings.MaterialPresetChanged += ActiveSliceSettings_MaterialPresetChanged;

			return widget;
		}

		private void ActiveSliceSettings_MaterialPresetChanged(object sender, EventArgs e)
		{
			if (settingsTemperature != null && ActiveSliceSettings.Instance != null)
			{
				settingsTemperature.Text = ActiveSliceSettings.Instance.GetValue(SettingsKey.temperature);
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			ActiveSliceSettings.MaterialPresetChanged -= ActiveSliceSettings_MaterialPresetChanged;
			base.OnClosed(e);
		}
	}
}