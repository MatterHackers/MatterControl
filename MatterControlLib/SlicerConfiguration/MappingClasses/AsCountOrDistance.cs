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
	public class AsCountOrDistance : MappedSetting
	{
		private string keyToUseAsDenominatorForCount;

		public AsCountOrDistance(PrinterConfig printer, string canonicalSettingsName, string exportedName, string keyToUseAsDenominatorForCount)
			: base(printer, canonicalSettingsName, exportedName)
		{
			this.keyToUseAsDenominatorForCount = keyToUseAsDenominatorForCount;
		}

		public override string Value
		{
			get
			{
				// When the state is store in mm, determine and use the value in (counted) units i.e. round distance up to layer count
				if (base.Value.Contains("mm"))
				{
					string withoutMm = base.Value.Replace("mm", "");
					string distanceString = printer.Settings.GetValue(keyToUseAsDenominatorForCount);
					double denominator = ParseDouble(distanceString, 1);

					int layers = (int)(ParseDouble(withoutMm) / denominator + .5);
					return layers.ToString();
				}

				return base.Value;
			}
		}
	}
}