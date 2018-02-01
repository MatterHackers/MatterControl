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

using System.Collections.Generic;
using ClipperLib;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class SmoothPath : Object3D, IRebuildable
	{
		public Polygons PathData;

		public SmoothPath()
		{
		}

		public void Rebuild()
		{
		}

		public static  Polygons DoSmoothing(Polygons inputPolygons, long maxDist = 300, int interations = 3, bool closedPath = true)
		{
			Polygons outputPolygons = new Polygons();
			foreach (Polygon inputPolygon in inputPolygons)
			{
				int numVerts = inputPolygon.Count;
				long maxDistSquared = maxDist * maxDist;

				var smoothedPositions = new Polygon(numVerts);
				foreach (IntPoint inputPosition in inputPolygon)
				{
					smoothedPositions.Add(inputPosition);
				}

				for (int iteration = 0; iteration < interations; iteration++)
				{
					var positionsThisPass = new Polygon(numVerts);
					foreach (IntPoint inputPosition in smoothedPositions)
					{
						positionsThisPass.Add(inputPosition);
					}

					int startIndex = closedPath ? 0 : 1;
					int endIndex = closedPath ? numVerts : numVerts - 1;

					for (int i = startIndex; i < endIndex; i++)
					{
						// wrap back to the previous index
						IntPoint prev = positionsThisPass[(i + numVerts - 1) % numVerts];
						IntPoint cur = positionsThisPass[i];
						IntPoint next = positionsThisPass[(i + 1) % numVerts];

						IntPoint newPos = (prev + cur + next) / 3;
						IntPoint delta = newPos - inputPolygon[i];
						if (delta.LengthSquared() > maxDistSquared)
						{
							delta = delta.GetLength(maxDist);
							newPos = inputPolygon[i] + delta;
						}
						smoothedPositions[i] = newPos;
					}
				}

				outputPolygons.Add(smoothedPositions);
			}

			return outputPolygons;
		}

	}
}