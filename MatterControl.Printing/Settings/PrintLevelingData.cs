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
using MatterHackers.VectorMath;
using MIConvexHull;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class PrintLevelingData
	{
		public PrintLevelingData()
		{
		}

		public List<Vector3> SampledPositions = new List<Vector3>();

		public LevelingSystem LevelingSystem { get; set; }

		public DateTime CreationDate { get; set; }

		public double BedTemperature { get; set; }

		public bool IssuedLevelingTempWarning { get; set; }

		public bool SamplesAreSame(List<Vector3> sampledPositions)
		{
			if (sampledPositions.Count == SampledPositions.Count)
			{
				for (int i = 0; i < sampledPositions.Count; i++)
				{
					if (sampledPositions[i] != SampledPositions[i])
					{
						return false;
					}
				}

				return true;
			}

			return false;
		}

        public IEnumerable<(Vector3 v0, Vector3 v1, Vector3 v2)> GetLevelingTriangles()
        {
            // get the delaunay triangulation
            var zDictionary = new Dictionary<(double, double), double>();
            var vertices = new List<DefaultVertex>();

            if (SampledPositions.Count > 2)
            {
                foreach (var sample in SampledPositions)
                {
                    vertices.Add(new DefaultVertex()
                    {
                        Position = new double[] { sample.X, sample.Y }
                    });
                    var key = (sample.X, sample.Y);
                    if (!zDictionary.ContainsKey(key))
                    {
                        zDictionary.Add(key, sample.Z);
                    }
                }
            }
            else
            {
                vertices.Add(new DefaultVertex()
                {
                    Position = new double[] { 0, 0 }
                });
                zDictionary.Add((0, 0), 0);

                vertices.Add(new DefaultVertex()
                {
                    Position = new double[] { 200, 0 }
                });
                zDictionary.Add((200, 0), 0);

                vertices.Add(new DefaultVertex()
                {
                    Position = new double[] { 100, 200 }
                });
                zDictionary.Add((100, 200), 0);
            }

            int extraXPosition = -50000;
            vertices.Add(new DefaultVertex()
            {
                Position = new double[] { extraXPosition, vertices[0].Position[1] }
            });

            var triangles = DelaunayTriangulation<DefaultVertex, DefaultTriangulationCell<DefaultVertex>>.Create(vertices, .001);

            // make all the triangle planes for these triangles
            foreach (var triangle in triangles.Cells)
            {
                var p0 = triangle.Vertices[0].Position;
                var p1 = triangle.Vertices[1].Position;
                var p2 = triangle.Vertices[2].Position;
                if (p0[0] != extraXPosition && p1[0] != extraXPosition && p2[0] != extraXPosition)
                {
                    var v0 = new Vector3(p0[0], p0[1], zDictionary[(p0[0], p0[1])]);
                    var v1 = new Vector3(p1[0], p1[1], zDictionary[(p1[0], p1[1])]);
                    var v2 = new Vector3(p2[0], p2[1], zDictionary[(p2[0], p2[1])]);
                    // add all the regions
                    yield return (v0, v1, v2);
                }
            }
        }
	}
}