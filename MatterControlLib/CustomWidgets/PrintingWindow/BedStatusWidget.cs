﻿/*
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class BedStatusWidget : TemperatureStatusWidget
	{
		public BedStatusWidget(PrinterConfig printer, bool smallScreen, ThemeConfig theme)
			: base(printer, smallScreen ? "Bed".Localize() : "Bed Temperature".Localize(), theme)
		{
			// Register listeners
			printer.Connection.BedTemperatureRead += Connection_BedTemperatureRead;
		}

		public override void UpdateTemperatures()
		{
			double targetValue = printer.Connection.TargetBedTemperature;
			double actualValue = Math.Max(0, printer.Connection.ActualBedTemperature);

			progressBar.RatioComplete = targetValue != 0 ? actualValue / targetValue : 1;

			actualTemp.Text = $"{actualValue:0}".PadLeft(3, (char)0x2007) + "°"; // put in padding spaces to make it at least 3 characters
			targetTemp.Text = $"{targetValue:0}".PadLeft(3, (char)0x2007) + "°"; // put in padding spaces to make it at least 3 characters
		}

		private void Connection_BedTemperatureRead(object s, EventArgs e)
		{
			this.UpdateTemperatures();
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.BedTemperatureRead -= Connection_BedTemperatureRead;

			base.OnClosed(e);
		}
	}
}