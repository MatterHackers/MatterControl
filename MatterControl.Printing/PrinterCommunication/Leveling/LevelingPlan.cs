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

using System;
using System.Collections.Generic;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterControl.Printing.PrintLeveling
{
	public abstract class LevelingPlan
	{
		protected PrintHostConfig printer;

		public abstract IEnumerable<Vector2> GetPrintLevelPositionToSample();

		public virtual int ProbeCount { get; }

		public virtual int TotalSteps => this.ProbeCount * 3;

		public LevelingPlan(PrintHostConfig printer)
		{
			this.printer = printer;
		}

		public static Vector2 ProbeOffsetSamplePosition(PrintHostConfig printer)
		{
			if (printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution) == LevelingSystem.ProbeCustom)
			{
				return printer.Settings.GetValue<Vector2>(SettingsKey.probe_offset_sample_point);
			}

			return printer.Settings.GetValue<Vector2>(SettingsKey.print_center);
		}

		public IEnumerable<Vector2> GetSampleRing(int numberOfSamples, double ratio, double phase)
		{
			double bedRadius = Math.Min(printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).X, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).Y) / 2;
			Vector2 bedCenter = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);

			for (int i = 0; i < numberOfSamples; i++)
			{
				Vector2 position = new Vector2(bedRadius * ratio, 0);
				position.Rotate(MathHelper.Tau / numberOfSamples * i + phase);
				position += bedCenter;
				yield return position;
			}
		}
	}
}