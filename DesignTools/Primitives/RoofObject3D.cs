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
using System.ComponentModel;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class RoofObject3D : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public RoofObject3D()
		{
		}

		public static RoofObject3D Create()
		{
			var item = new RoofObject3D();

			item.Rebuild();
			return item;
		}

		public double Width { get; set; } = 20;
		public double Depth { get; set; } = 20;
		public double Height { get; set; } = 10;

		public void Rebuild()
		{
			var aabb = this.GetAxisAlignedBoundingBox();

			var path = new VertexStorage();
			path.MoveTo(0, 0);
			path.LineTo(Width, 0);
			path.LineTo(Height, Height);

			var mesh = VertexSourceToMesh.Extrude(path, Depth);
			mesh.Transform(Matrix4X4.CreateRotationX(MathHelper.Tau / 4));
			Mesh = mesh;
			if (aabb.ZSize > 0)
			{
				// If the part was already created and at a height, maintain the height.
				PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
			}
		}
	}
}