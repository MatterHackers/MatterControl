/*
Copyright (c) 2014, Kevin Pope
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
	internal class TemperatureWidgetBed : TemperatureWidgetBase
	{
		//Not currently hooked up to anything
		public TemperatureWidgetBed()
			: base("150.3°")
		{
			temperatureTypeName.Text = "Print Bed";
			setToCurrentTemperature();
			ToolTipText = "Current bed temperature".Localize();
			preheatButton.ToolTipText = "Preheat the Bed".Localize();
			PrinterConnectionAndCommunication.Instance.BedTemperatureRead.RegisterEvent(onTemperatureRead, ref unregisterEvents);
		}

		private EventHandler unregisterEvents;

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private void setToCurrentTemperature()
		{
			string tempDirectionIndicator = "";
			if (PrinterConnectionAndCommunication.Instance.TargetBedTemperature > 0)
			{
				if ((int)(PrinterConnectionAndCommunication.Instance.TargetBedTemperature + 0.5) < (int)(PrinterConnectionAndCommunication.Instance.ActualBedTemperature + 0.5))
				{
					tempDirectionIndicator = "↓";
				}
				else if ((int)(PrinterConnectionAndCommunication.Instance.TargetBedTemperature + 0.5) > (int)(PrinterConnectionAndCommunication.Instance.ActualBedTemperature + 0.5))
				{
					tempDirectionIndicator = "↑";
				}
			}
			this.IndicatorValue = string.Format(" {0:0.#}°{1}", PrinterConnectionAndCommunication.Instance.ActualBedTemperature, tempDirectionIndicator);
		}

		private void onTemperatureRead(Object sender, EventArgs e)
		{
			setToCurrentTemperature();
		}

		private string sliceSettingsNote = "Note: Slice Settings are applied before the print actually starts. Changes while printing will not effect the active print.".Localize();
		private string waitingForBedToHeatMessage = "The bed is currently heating and its target temperature cannot be changed until it reaches {0}°C.\n\nYou can set the starting bed temperature in SETTINGS -> Filament -> Temperatures.\n\n{1}".Localize();
		private string waitingForBedToHeatTitle = "Waiting For Bed To Heat".Localize();

		protected override void SetTargetTemperature()
		{
			double targetTemp = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.bed_temperature);
			if (targetTemp != 0)
			{
				double goalTemp = (int)(targetTemp + .5);
				if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting
					&& PrinterConnectionAndCommunication.Instance.PrintingState == PrinterConnectionAndCommunication.DetailedPrintingState.HeatingBed
					&& goalTemp != PrinterConnectionAndCommunication.Instance.TargetBedTemperature)
				{
					string message = string.Format(waitingForBedToHeatMessage, PrinterConnectionAndCommunication.Instance.TargetBedTemperature, sliceSettingsNote);
					StyledMessageBox.ShowMessageBox(null, message, waitingForBedToHeatTitle);
				}
				else
				{
					PrinterConnectionAndCommunication.Instance.TargetBedTemperature = (int)(targetTemp + .5);
				}
			}
		}
	}
}