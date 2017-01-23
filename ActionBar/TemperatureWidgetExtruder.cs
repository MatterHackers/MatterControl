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

using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.ActionBar
{
	internal class TemperatureWidgetExtruder : TemperatureWidgetBase
	{
		private int extruderNumber = 1;
		private EventHandler unregisterEvents;

		private string sliceSettingsNote = "Note: Slice Settings are applied before the print actually starts. Changes while printing will not effect the active print.".Localize();
		private string waitingForExtruderToHeatMessage = "The extruder is currently heating and its target temperature cannot be changed until it reaches {0}°C.\n\nYou can set the starting extruder temperature in 'Slice Settings' -> 'Filament'.\n\n{1}".Localize();

		public TemperatureWidgetExtruder()
			: base("150.3°")
		{
			temperatureTypeName.Text = "Extruder";
			DisplayCurrentTemperature();

			ToolTipText = "Current extruder temperature".Localize();
			preheatButton.ToolTipText = "Preheat the Extruder".Localize();

			PrinterConnectionAndCommunication.Instance.ExtruderTemperatureRead.RegisterEvent((s, e) => DisplayCurrentTemperature(), ref unregisterEvents);
		}

		private void DisplayCurrentTemperature()
		{
			string tempDirectionIndicator = "";
			if (PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderNumber - 1) > 0)
			{
				if ((int)(PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderNumber - 1) + 0.5) < (int)(PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(extruderNumber - 1) + 0.5))
				{
					tempDirectionIndicator = "↓";
				}
				else if ((int)(PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderNumber - 1) + 0.5) > (int)(PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(extruderNumber - 1) + 0.5))
				{
					tempDirectionIndicator = "↑";
				}
			}
			this.IndicatorValue = string.Format(" {0:0.#}°{1}", PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(extruderNumber - 1), tempDirectionIndicator);
		}

		protected override void SetTargetTemperature()
		{
			double targetTemp;
			if (double.TryParse(ActiveSliceSettings.Instance.GetValue(SettingsKey.temperature), out targetTemp))
			{
				double goalTemp = (int)(targetTemp + .5);
				if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting
					&& PrinterConnectionAndCommunication.Instance.PrintingState == PrinterConnectionAndCommunication.DetailedPrintingState.HeatingExtruder
					&& goalTemp != PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderNumber - 1))
				{
					string message = string.Format(waitingForExtruderToHeatMessage, PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderNumber - 1), sliceSettingsNote);
					StyledMessageBox.ShowMessageBox(null, message, "Waiting For Extruder To Heat".Localize());
				}
				else
				{
					PrinterConnectionAndCommunication.Instance.SetTargetExtruderTemperature(extruderNumber - 1, (int)(targetTemp + .5));
				}
			}
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}