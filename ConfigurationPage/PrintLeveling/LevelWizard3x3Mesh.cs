/*
Copyright (c) 2014, Lars Brubaker
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
using System.Collections.Generic;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LevelWizard3x3Mesh : LevelWizardBase
	{
		public LevelWizard3x3Mesh(PrinterConfig printer, LevelWizardBase.RuningState runningState)
			: base(printer, runningState)
		{
		}

		public override int ProbeCount => 9;

		public override IEnumerable<Vector2> GetPrintLevelPositionToSample()
		{
			yield return GetPosition(0, 0);
			yield return GetPosition(1, 0);
			yield return GetPosition(2, 0);

			yield return GetPosition(2, 1);
			yield return GetPosition(1, 1);
			yield return GetPosition(0, 1);

			yield return GetPosition(0, 2);
			yield return GetPosition(1, 2);
			yield return GetPosition(2, 2);
		}

		private Vector2 GetPosition(int xIndex, int yIndex)
		{
			Vector2 bedSize = printer.Settings.GetValue<Vector2>(SettingsKey.bed_size);
			Vector2 printCenter = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);

			if (printer.Settings.GetValue<BedShape>(SettingsKey.bed_shape) == BedShape.Circular)
			{
				// reduce the bed size by the ratio of the radius (square root of 2) so that the sample positions will fit on a ciclular bed
				bedSize *= 1.0 / Math.Sqrt(2);
			}

			Vector2 samplePosition = new Vector2();
			switch (xIndex)
			{
				case 0:
					samplePosition.X = printCenter.X - (bedSize.X / 2) * .8;
					break;

				case 1:
					samplePosition.X = printCenter.X;
					break;

				case 2:
					samplePosition.X = printCenter.X + (bedSize.X / 2) * .8;
					break;

				default:
					throw new IndexOutOfRangeException();
			}

			switch (yIndex)
			{
				case 0:
					samplePosition.Y = printCenter.Y - (bedSize.Y / 2) * .8;
					break;

				case 1:
					samplePosition.Y = printCenter.Y;
					break;

				case 2:
					samplePosition.Y = printCenter.Y + (bedSize.Y / 2) * .8;
					break;

				default:
					throw new IndexOutOfRangeException();
			}

			return samplePosition;
		}
	}
}