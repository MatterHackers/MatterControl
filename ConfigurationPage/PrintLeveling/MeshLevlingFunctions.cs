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
using System.Text;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class MeshLevlingFunctions : IDisposable
	{
		private Vector3 lastDestinationWithLevelingApplied = new Vector3();

		PrinterSettings printerSettings;

		public MeshLevlingFunctions(PrinterSettings printerSettings, int gridWidth, int gridHeight, PrintLevelingData levelingData)
		{
			this.printerSettings = printerSettings;
			this.SampledPositions = new List<Vector3>(levelingData.SampledPositions);

			for (int y = 0; y < gridHeight - 1; y++)
			{
				for (int x = 0; x < gridWidth - 1; x++)
				{
					// add all the regions
					Regions.Add(new Region()
					{
						LeftBottom = levelingData.SampledPositions[y * gridWidth + x],
						RightBottom = levelingData.SampledPositions[y * gridWidth + x + 1],
						LeftTop = levelingData.SampledPositions[(y + 1) * gridWidth + x],
						RightTop = levelingData.SampledPositions[(y + 1) * gridWidth + x + 1],
					});
				}
			}
		}

		// you can only set this on construction
		public List<Vector3> SampledPositions { get; private set; }

		public List<Region> Regions { get; private set; } = new List<Region>();

		public void Dispose()
		{
		}

		public string DoApplyLeveling(string lineBeingSent, Vector3 currentDestination)
		{
			double extruderDelta = 0;
			GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref extruderDelta);
			double feedRate = 0;
			GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref feedRate);

			StringBuilder newLine = new StringBuilder("G1 ");

			if (lineBeingSent.Contains("X") || lineBeingSent.Contains("Y") || lineBeingSent.Contains("Z"))
			{
				Vector3 outPosition = GetPositionWithZOffset(currentDestination);

				lastDestinationWithLevelingApplied = outPosition;

				newLine = newLine.Append(String.Format("X{0:0.##} Y{1:0.##} Z{2:0.###}", outPosition.X, outPosition.Y, outPosition.Z));
			}

			if (extruderDelta != 0)
			{
				newLine = newLine.Append(String.Format(" E{0:0.###}", extruderDelta));
			}

			if (feedRate != 0)
			{
				newLine = newLine.Append(String.Format(" F{0:0.##}", feedRate));
			}

			lineBeingSent = newLine.ToString();

			return lineBeingSent;
		}

		public Vector3 GetPositionWithZOffset(Vector3 currentDestination)
		{
			Region region = GetCorrectRegion(currentDestination);

			return region.GetPositionWithZOffset(currentDestination);
		}

		public Vector2 GetPrintLevelPositionToSample(int index, int gridWidth, int gridHeight)
		{
			Vector2 bedSize = printerSettings.GetValue<Vector2>(SettingsKey.bed_size);
			Vector2 printCenter = printerSettings.GetValue<Vector2>(SettingsKey.print_center);

			switch (printerSettings.GetValue<BedShape>(SettingsKey.bed_shape))
			{
				case BedShape.Circular:
					Vector2 firstPosition = new Vector2(printCenter.X, printCenter.Y + (bedSize.Y / 2) * .5);
					switch (index)
					{
						case 0:
							return firstPosition;

						case 1:
							return Vector2.Rotate(firstPosition, MathHelper.Tau / 3);

						case 2:
							return Vector2.Rotate(firstPosition, MathHelper.Tau * 2 / 3);

						default:
							throw new IndexOutOfRangeException();
					}

				case BedShape.Rectangular:
				default:
					switch (index)
					{
						case 0:
							return new Vector2(printCenter.X, printCenter.Y + (bedSize.Y / 2) * .8);

						case 1:
							return new Vector2(printCenter.X - (bedSize.X / 2) * .8, printCenter.Y - (bedSize.Y / 2) * .8);

						case 2:
							return new Vector2(printCenter.X + (bedSize.X / 2) * .8, printCenter.Y - (bedSize.Y / 2) * .8);

						default:
							throw new IndexOutOfRangeException();
					}
			}
		}

		private Region GetCorrectRegion(Vector3 currentDestination)
		{
			int bestIndex = 0;
			double bestDist = double.PositiveInfinity;

			currentDestination.Z = 0;
			for (int regionIndex = 0; regionIndex < Regions.Count; regionIndex++)
			{
				var dist = (Regions[regionIndex].Center - currentDestination).LengthSquared;
				if(dist < bestDist)
				{
					bestIndex = regionIndex;
					bestDist = dist;
				}
			}

			return Regions[bestIndex];
		}

		public class Region
		{
			public Vector3 LeftBottom { get; set; }
			public Vector3 LeftTop { get; set; }
			public Vector3 RightBottom { get; set; }
			public Vector3 RightTop { get; set; }

			internal Vector3 Center { get; private set; }
			internal Vector3 LeftBottomCenter { get; private set; }
			internal Vector3 RightTopCenter { get; private set; }

			internal Plane LeftBottomPlane { get; private set; }
			internal Plane RightTopPlane { get; private set; }

			internal Vector3 GetPositionWithZOffset(Vector3 currentDestination)
			{
				if (LeftBottomPlane.PlaneNormal == Vector3.Zero)
				{
					InitializePlanes();
				}

				var destinationAtZ0 = new Vector3(currentDestination.X, currentDestination.Y, 0);

				// which triangle to check (distance to the centers)
				if ((LeftBottomCenter - destinationAtZ0).LengthSquared < (RightTopCenter - destinationAtZ0).LengthSquared)
				{
					double hitDistance = LeftBottomPlane.GetDistanceToIntersection(destinationAtZ0, Vector3.UnitZ);
					currentDestination.Z += hitDistance;
				}
				else
				{
					double hitDistance = RightTopPlane.GetDistanceToIntersection(destinationAtZ0, Vector3.UnitZ);
					currentDestination.Z += hitDistance;
				}

				return currentDestination;
			}

			private void InitializePlanes()
			{
				LeftBottomPlane = new Plane(LeftBottom, RightBottom, LeftTop);
				LeftBottomCenter = (LeftBottom + RightBottom + LeftTop) / 3;

				RightTopPlane = new Plane(RightBottom, RightTop, LeftTop);
				RightTopCenter = (RightBottom + RightTop + LeftTop) / 3;

				Center = (LeftBottomCenter + RightTopCenter) / 2;
			}
		}
	}
}