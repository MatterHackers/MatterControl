/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
	public class AsPercentOfReferenceOrDirect : ValueConverter
	{
		private readonly bool change0ToReference;
		private readonly double scale;

		public AsPercentOfReferenceOrDirect(string referencedSetting, double scale = 1, bool change0ToReference = true)
		{
			this.change0ToReference = change0ToReference;
			this.scale = scale;
			this.ReferencedSetting = referencedSetting;
		}

		public string ReferencedSetting { get; }

		public override string Convert(string value, PrinterSettings settings)
		{
			double finalValue = 0;

			if (value.Contains("%"))
			{
				string withoutPercent = value.Replace("%", "");
				double ratio = ParseDouble(withoutPercent) / 100.0;
				string originalReferenceString = settings.GetValue(this.ReferencedSetting);
				double valueToModify = ParseDouble(originalReferenceString);
				finalValue = valueToModify * ratio;
			}
			else
			{
				finalValue = ParseDouble(value);
			}

			if (change0ToReference
				&& finalValue == 0)
			{
				finalValue = ParseDouble(settings.GetValue(ReferencedSetting));
			}

			finalValue *= scale;

			return finalValue.ToString();
		}
	}
}