/*
Copyright (c) 2019, Lars Brubaker
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

using System.Collections.Generic;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterControl.Printing.PrintLeveling
{
	public class LevelWizardCustom : LevelingPlan
	{
		public LevelWizardCustom(PrintHostConfig printer)
			: base(printer)
		{
		}

		public static List<Vector2> ParseLevelingSamplePoints(PrintHostConfig printer)
		{
			var pointsToProbe = new List<Vector2>();
			var samples = printer.Settings.GetValue(SettingsKey.leveling_sample_points).Replace("\r", "").Replace("\n", "").Trim();
			double xPos = double.NegativeInfinity;
			foreach(var coord in samples.Split(','))
			{
				if(double.TryParse(coord, out double result))
				{
					if(xPos == double.NegativeInfinity)
					{
						// this is the first coord it is an x position
						xPos = result;
					}
					else // we have an x
					{
						pointsToProbe.Add(new Vector2(xPos, result));
						xPos = double.NegativeInfinity;
					}
				}
			}

			return pointsToProbe;
		}

		public override int ProbeCount
		{
			get
			{
				return ParseLevelingSamplePoints(printer).Count;
			}
		}

		public override IEnumerable<Vector2> GetPrintLevelPositionToSample()
		{
			foreach(var position in ParseLevelingSamplePoints(printer))
			{
				yield return position;
			}
		}
	}
}