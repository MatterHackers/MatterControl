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

using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterControl.Printing.PrintLeveling
{
	public class LevelWizard100PointRadial : LevelingPlan
	{
		public LevelWizard100PointRadial(PrintHostConfig printer)
			: base(printer)
		{
		}

		public override int ProbeCount => 100;

		public override IEnumerable<Vector2> GetPrintLevelPositionToSample()
		{
			// the center
			foreach (var sample in GetSampleRing(1, 0, 0))
			{
				yield return sample;
			}

			int[] ringCounts = { 3, 6, 12, 26, 52 };
			double[] ringPhase = { 0, MathHelper.Tau * 2 / 3, MathHelper.Tau / 2, MathHelper.Tau / 2, MathHelper.Tau / 2 };
			double step = .9 / 5;
			// and several rings
			for (int i = 0; i < 5; i++)
			{
				foreach (var sample in GetSampleRing(ringCounts[i], step + step * i, ringPhase[i]))
				{
					yield return sample;
				}
			}
		}
	}
}