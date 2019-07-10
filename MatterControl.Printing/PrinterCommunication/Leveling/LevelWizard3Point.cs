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
	public class LevelWizard3Point : LevelingPlan
	{
		public LevelWizard3Point(PrintHostConfig printer)
			: base(printer)
		{
		}

		public override int ProbeCount => 3;

		public override IEnumerable<Vector2> GetPrintLevelPositionToSample()
		{
			Vector2 bedSize = printer.Settings.GetValue<Vector2>(SettingsKey.bed_size);
			Vector2 printCenter = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);

			if (printer.Settings.GetValue<BedShape>(SettingsKey.bed_shape) == BedShape.Circular)
			{
				Vector2 firstPosition = new Vector2(printCenter.X, printCenter.Y + (bedSize.Y / 2) * .5);
				yield return firstPosition;
				yield return Vector2.Rotate(firstPosition, MathHelper.Tau / 3);
				yield return Vector2.Rotate(firstPosition, MathHelper.Tau * 2 / 3);
			}
			else
			{
				yield return new Vector2(printCenter.X - (bedSize.X / 2) * .8, printCenter.Y - (bedSize.Y / 2) * .8);
				yield return new Vector2(printCenter.X + (bedSize.X / 2) * .8, printCenter.Y - (bedSize.Y / 2) * .8);
				yield return new Vector2(printCenter.X, printCenter.Y + (bedSize.Y / 2) * .8);
			}
		}
	}
}