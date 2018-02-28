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
using System.Threading;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class RingObject3D : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public RingObject3D()
		{
		}

		public RingObject3D(double outerDiameter, double innerDiameter, double height, int sides)
		{
			this.OuterDiameter = outerDiameter;
			this.InnerDiameter = innerDiameter;
			this.Height = height;
			this.Sides = sides;

			Rebuild();
		}

		public static RingObject3D Create()
		{
			var item = new RingObject3D();

			item.Rebuild();
			return item;
		}

		[DisplayName("Outer Diameter")]
		public double OuterDiameter { get; set; } = 20;
		[DisplayName("Inner Diameter")]
		public double InnerDiameter { get; set; } = 25;
		public double Height { get; set; } = 20;
		public int Sides { get; set; } = 30;

		public void Rebuild()
		{
			var aabb = this.GetAxisAlignedBoundingBox();

			var path = new VertexStorage();
			path.MoveTo(OuterDiameter / 2, 0);

			for (int i = 1; i < Sides; i++)
			{
				var angle = MathHelper.Tau * i / (Sides - 1);
				path.LineTo(Math.Cos(angle) * OuterDiameter / 2, Math.Sin(angle) * OuterDiameter / 2);
			}

			path.MoveTo(InnerDiameter / 2, 0);

			for (int i = 1; i < Sides; i++)
			{
				var angle = -MathHelper.Tau * i / (Sides - 1);
				path.LineTo(Math.Cos(angle) * InnerDiameter / 2, Math.Sin(angle) * InnerDiameter / 2);
			}

			Mesh = VertexSourceToMesh.Extrude(path, Height);

			if (aabb.ZSize > 0)
			{
				// If the part was already created and at a height, maintain the height.
				PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
			}
		}
	}
}