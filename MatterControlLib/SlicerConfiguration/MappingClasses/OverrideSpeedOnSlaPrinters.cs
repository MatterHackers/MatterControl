/*
Copyright (c) 2016, Lars Brubaker
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

namespace MatterHackers.MatterControl.SlicerConfiguration.MappingClasses
{
	public class OverrideSpeedOnSlaPrinters : AsPercentOfReferenceOrDirect
	{
		public OverrideSpeedOnSlaPrinters(PrinterConfig printer, string canonicalSettingsName, string exportedName, string originalReference, double scale = 1)
			: base(printer, canonicalSettingsName, exportedName, originalReference, scale)
		{
		}

		public override string Value
		{
			get
			{
				if (printer.Settings.GetValue<bool>(SettingsKey.sla_printer))
				{
					// return the speed based on the layer height
					var speedAt025 = printer.Settings.GetValue<double>(SettingsKey.laser_speed_025);
					var speedAt100 = printer.Settings.GetValue<double>(SettingsKey.laser_speed_100);
					var deltaSpeed = speedAt100 - speedAt025;

					var layerHeight = printer.Settings.GetValue<double>(SettingsKey.layer_height);
					var deltaHeight = .1 - .025;
					var heightRatio = (layerHeight - .025) / deltaHeight;
					var ajustedSpeed = speedAt025 + deltaSpeed * heightRatio;
					return ajustedSpeed.ToString();
				}
				else
				{
					return base.Value;
				}
			}
		}
	}
}