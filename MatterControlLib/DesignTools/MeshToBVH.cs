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
using MatterHackers.Agg;
using System.Linq;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;

namespace MatterHackers.DataConverters3D
{
    public static class MeshToBVH
	{
		public static ITraceable Convert(Mesh mesh, MaterialAbstract partMaterial = null)
		{
			return Convert(new Object3D()
			{
				Mesh = mesh
			});
		}

		public static ITraceable Convert(IObject3D rootItem)
		{
			var tracePrimitives = new List<ITraceable>();

			foreach (var item in rootItem.VisibleMeshes())
			{
				SolidMaterial partMaterial;
				var color = item.WorldColor(rootItem);
				if (color.alpha != 0)
				{
					partMaterial = new SolidMaterial(new ColorF(color.Red0To1, color.Green0To1, color.Blue0To1), .01, 0.0, 2.0);
				}
				else
				{
					partMaterial = new SolidMaterial(new ColorF(.9, .2, .1), .01, 0.0, 2.0);
				}

				var worldMatrix = item.WorldMatrix(rootItem);

				item.Mesh.AddTraceables(partMaterial, worldMatrix, tracePrimitives);
			}

			// return an empty collection
			return BoundingVolumeHierarchy.CreateNewHierachy(tracePrimitives);
		}

		public static ITraceable Convert(List<IObject3D> renderDatas)
		{
			var renderCollection = new List<ITraceable>();
			foreach (var renderData in renderDatas)
			{
				renderCollection.Add(Convert(renderData));
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
		}

		public static ITraceable Convert(IObject3D item, MaterialAbstract partMaterial = null)
		{
			var renderCollection = new List<ITraceable>();

			if (partMaterial == null)
			{
				partMaterial = new SolidMaterial(new ColorF(.9, .2, .1), .01, 0.0, 2.0);
			}

			int index = 0;
			var triangle = new Vector3[3];
			foreach (Mesh mesh in item.VisibleMeshes().Select(i => i.Mesh) )
			{
				throw new NotImplementedException();
				//foreach (Face face in mesh.Faces)
				//{
				//	foreach (Vertex vertex in face.Vertices())
				//	{
				//		triangle[index++] = vertex.Position;
				//		if (index == 3)
				//		{
				//			index = 0;
				//			renderCollection.Add(new TriangleShape(triangle[0], triangle[1], triangle[2], partMaterial));
				//		}
				//	}
				//}
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
		}

		public static ITraceable ConvertUnoptomized(Mesh simpleMesh)
		{
			var renderCollection = new List<ITraceable>();

			//SolidMaterial redStuff = new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0);
			var mhBlueStuff = new SolidMaterial(new ColorF(0, .32, .58), .01, 0.0, 2.0);
			int index = 0;
			var triangle = new Vector3[3];
			//Mesh simpleMesh = Processors.StlProcessing.Load("complex.stl");
			//Mesh simpleMesh = Processors.StlProcessing.Load("Spider With Base.stl");
			throw new NotImplementedException();
			//foreach (Face face in simpleMesh.Faces)
			//{
			//	foreach (Vertex vertex in face.Vertices())
			//	{
			//		triangle[index++] = vertex.Position;
			//		if (index == 3)
			//		{
			//			index = 0;
			//			renderCollection.Add(new TriangleShape(triangle[0], triangle[1], triangle[2], mhBlueStuff));
			//		}
			//	}
			//}

			//return new UnboundCollection(renderCollection);
		}
	}
}