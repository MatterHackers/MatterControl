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
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class HexagonPath : Object3D
	{
		IVertexSource Path { get; set; }
		public double Radius { get; set; } = 10;
		public double StrokeWidth { get; set; } = 2;

		public void Rebuild()
		{
			var path = new VertexStorage();
			path.MoveTo(Radius, 0);
			for(int i=1; i<6; i++)
			{
				var angle = MathHelper.Tau / 6 * i;
				var next = new Vector2(Math.Cos(angle), Math.Sin(angle)) * Radius;
				path.LineTo(next);
			}

			Path = new Stroke(path, StrokeWidth);
		}
	}

	public class HexGridPath : Object3D
	{
		public double EdgeLength { get; set; } = 10;
		public double StrokeWidth { get; set; } = 2;

		public int GridWidth = 3;
		public int GridHeight = 3;

		public void Rebuild()
		{
			for(int y=0; y<GridHeight; y++)
			{
				for(int x=0; x<GridWidth; x++)
				{

				}
			}

			// convert it to a clipper array and union
			// Convert back to a path
		}
	}

	public class HexGridObject3D : Object3D, IRebuildable
	{
		public HexGridObject3D()
		{
		}

		public override string ActiveEditor => "PublicPropertyEditor";

		public double EdgeLength { get; set; } = 10;
		public double StrokeWidth { get; set; } = 2;
		public double Height { get; set; } = 5;

		public int GridWidth = 3;
		public int GridHeight = 3;

		// 

		public static HexGridObject3D Create()
		{
			var item = new HexGridObject3D();
			item.Rebuild();
			return item;
		}

		public void Rebuild()
		{
			var gridPath = new HexGridPath()
			{
				GridWidth = this.GridWidth,
				GridHeight = this.GridHeight,
				StrokeWidth = this.StrokeWidth,
				EdgeLength = this.EdgeLength
			};
			gridPath.Rebuild();
			var aabb = this.GetAxisAlignedBoundingBox();

			Mesh = PlatonicSolids.CreateCube(Width, Depth, Height);
			if (aabb.ZSize > 0)
			{
				// If the part was already created and at a height, maintain the height.
				PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
			}
		}
	}
}