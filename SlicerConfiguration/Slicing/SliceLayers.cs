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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using MatterHackers.RayTracer;

namespace MatterHackers.MatterControl.SlicerConfiguration.Slicing
{
	public class SliceLayer
	{
		internal struct Segment
		{
			Vector2 start;
			Vector2 end;

			internal Segment(Vector2 start, Vector2 end)
			{
				this.start = start;
				this.end = end;
			}
		}

		double zHeight;
		public double ZHeight { get { return zHeight; } } 
		List<Segment> unorderedSegments;
		internal List<Segment> UnorderedSegments { get { return unorderedSegments; } }
		List<PathStorage> perimeters;

		public SliceLayer(double zHeight)
		{
			this.zHeight = zHeight;
		}
	}

	public class SliceLayers
	{
		List<SliceLayer> allLayers = new List<SliceLayer>();

		public SliceLayers()
		{
		}

		public List<SliceLayer> GetAllLayers(Mesh meshToSlice, double firstLayerHeight, double otherLayerHeights, double bottomClip)
		{
			AxisAlignedBoundingBox meshBounds = meshToSlice.GetAxisAlignedBoundingBox();
			double heightWithoutFirstLayer = meshBounds.ZSize - firstLayerHeight - bottomClip;
			int layerCount = (int)((heightWithoutFirstLayer / otherLayerHeights) + .5);
			double currentZ = otherLayerHeights;
			if (firstLayerHeight > 0)
			{
				layerCount++;
				currentZ = firstLayerHeight;
			}

			for (int i = 0; i < layerCount; i++)
			{
				allLayers.Add(new SliceLayer(currentZ));
				currentZ += otherLayerHeights;
			}

			foreach (Face face in meshToSlice.Faces)
			{
				double minZ = double.MaxValue;
				double maxZ = double.MinValue;
				foreach(FaceEdge faceEdge in face.FaceEdges())
				{
					minZ = Math.Min(minZ, faceEdge.firstVertex.Position.z);
					maxZ = Math.Max(maxZ, faceEdge.firstVertex.Position.z);
				}

				for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
				{
					SliceLayer layer = allLayers[layerIndex];
					double zHeight = layer.ZHeight;
					if (zHeight < minZ || zHeight > maxZ)
					{
						continue;
					}
					Plane cutPlane = new Plane(Vector3.UnitZ, zHeight);

					Vector3 start;
					Vector3 end;
					if(face.GetCutLine(cutPlane, out start, out end))
					{
						layer.UnorderedSegments.Add(new SliceLayer.Segment(new Vector2(start.x, start.y), new Vector2(end.x, end.y)));
					}
				}
			}

			throw new NotImplementedException();
		}

		public SliceLayer GetPerimetersAtHeight(Mesh meshToSlice, double zHeight)
		{
			throw new NotImplementedException();
		}
	}
}
