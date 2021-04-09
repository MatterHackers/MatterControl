/*
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
using System.Collections.Generic;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LevelWizardMesh : LevelingPlan
	{
		private int gridWidth;
		private int gridHeight;

		public LevelWizardMesh(PrinterConfig printer, int width, int height)
			: base(printer)
		{
			this.gridWidth = width;
			this.gridHeight = height;
		}

		public override int ProbeCount => gridWidth * gridHeight;

		public override IEnumerable<Vector2> GetPositionsToSample(Vector3 startPosition)
		{
			AxisAlignedBoundingBox aabb = printer.Bed.Aabb;

			aabb.Expand(aabb.XSize * -.1, aabb.YSize * -.1, 0);

			if (printer.Settings.GetValue<BedShape>(SettingsKey.bed_shape) == BedShape.Circular)
			{
				// reduce the bed size by the ratio of the radius (square root of 2) so that the sample positions will fit on a circular bed
				aabb.Expand(aabb.XSize * .5 * (1 - Math.Sqrt(2)),
					aabb.YSize * .5 * (1 - Math.Sqrt(2)),
					0);
			}

			if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe))
			{
				var probeOffset = printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset);
				if (probeOffset.X < 0)
				{
					// add the negative offset as the place we are going to probe cannot be the right edge (the nozzle can't get there)
					aabb.MaxXYZ.X += probeOffset.X;
				}
				else
				{
					// Where are we going to try to probe (this position will be offset with the probe later)?
					// We adjust the left side as we cannot put the probe on the left edge as it would require the nozzle to be too far left.
					aabb.MinXYZ.X += probeOffset.X;
				}

				if (probeOffset.Y < 0)
				{
					aabb.MaxXYZ.Y += probeOffset.Y;
				}
				else
				{
					aabb.MinXYZ.Y += probeOffset.Y;
				}
			}

			double xStep = aabb.XSize / (gridWidth - 1);
			double yStep = aabb.YSize / (gridHeight - 1);

			var allPositions = new List<Vector2>(gridWidth * gridHeight);
			for (int y = 0; y < gridHeight; y++)
			{
				// make it such that every other line is printed from right to left
				for (int x = 0; x < gridWidth; x++)
				{
					allPositions.Add(new Vector2
					{
						X = aabb.MinXYZ.X + x * xStep,
						Y = aabb.MinXYZ.Y + y * yStep
					});
				}
			}

			var currentPosition = new Vector2(startPosition);
			Vector2 ClosestPosition()
			{
				var closestIndex = 0;
				var closestDist = double.PositiveInfinity;
				for(int i=0; i<allPositions.Count; i++)
				{
					var position = allPositions[i];
					var dist = (currentPosition - position).Length;
					if (dist < closestDist)
					{
						closestDist = dist;
						closestIndex = i;
					}
				}

				currentPosition = allPositions[closestIndex];
				allPositions.RemoveAt(closestIndex);
				return currentPosition;
			}

			while (allPositions.Count > 0)
			{
				yield return ClosestPosition();
			}
		}
	}
}