/*
Copyright (c) 2015, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl.Slicing
{
	public class SliceLayers
	{
		public SliceLayers()
		{
		}

		public List<SliceLayer> AllLayers { get; } = new List<SliceLayer>();

		public void DumpSegmentsToGcode(string filename)
		{
			StreamWriter stream = new StreamWriter(filename);
			stream.Write("; some gcode to look at the layer segments");
			int extrudeAmount = 0;
			for (int layerIndex = 0; layerIndex < AllLayers.Count; layerIndex++)
			{
				stream.Write("; LAYER:{0}\n".FormatWith(layerIndex));
				List<Segment> unorderedSegments = AllLayers[layerIndex].UnorderedSegments;
				for (int segmentIndex = 0; segmentIndex < unorderedSegments.Count; segmentIndex++)
				{
					Segment segment = unorderedSegments[segmentIndex];
					stream.Write("G1 X{0}Y{1}\n", segment.Start.X, segment.Start.Y);
					stream.Write("G1 X{0}Y{1}E{2}\n", segment.End.X, segment.End.Y, extrudeAmount++);
				}
			}
			stream.Close();
		}

		public SliceLayer GetPerimetersAtHeight(Mesh meshToSlice, double zHeight)
		{
			throw new NotImplementedException();
		}

		public void GetPerimetersForAllLayers(Mesh meshToSlice, double firstLayerHeight, double otherLayerHeights)
		{
			AllLayers.Clear();
			AxisAlignedBoundingBox meshBounds = meshToSlice.GetAxisAlignedBoundingBox();
			double heightWithoutFirstLayer = meshBounds.ZSize - firstLayerHeight;
			int layerCount = (int)((heightWithoutFirstLayer / otherLayerHeights) + .5);
			double currentZ = otherLayerHeights;
			if (firstLayerHeight > 0)
			{
				layerCount++;
				currentZ = firstLayerHeight;
			}

			for (int i = 0; i < layerCount; i++)
			{
				AllLayers.Add(new SliceLayer(new Plane(Vector3.UnitZ, i)));
				currentZ += otherLayerHeights;
			}

			throw new NotImplementedException();
			//foreach (Face face in meshToSlice.Faces)
			//{
			//	double minZ = double.MaxValue;
			//	double maxZ = double.MinValue;
			//	foreach (FaceEdge faceEdge in face.FaceEdges())
			//	{
			//		minZ = Math.Min(minZ, faceEdge.FirstVertex.Position.Z);
			//		maxZ = Math.Max(maxZ, faceEdge.FirstVertex.Position.Z);
			//	}

			//	for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
			//	{
			//		SliceLayer layer = AllLayers[layerIndex];
			//		double zHeight = layer.SlicePlane.DistanceToPlaneFromOrigin;
			//		if (zHeight < minZ)
			//		{
			//			// not up to the start of the face yet
			//			continue;
			//		}
			//		if (zHeight > maxZ)
			//		{
			//			// done with this face
			//			break;
			//		}
			//		Plane cutPlane = new Plane(Vector3.UnitZ, zHeight);

			//		var start = Vector3.Zero;
			//		var end = Vector3.Zero;
			//		if (face.GetCutLine(cutPlane, ref start, ref end))
			//		{
			//			layer.UnorderedSegments.Add(new Segment(new Vector2(start.X, start.Y), new Vector2(end.X, end.Y)));
			//		}
			//	}
			//}
		}
	}
}