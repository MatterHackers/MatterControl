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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class TorusObject3D : Object3D, IRebuildable, IPropertyGridModifier
	{
		public TorusObject3D()
		{
			Color = PrimitiveColors["Torus"];
		}

		public static TorusObject3D Create()
		{
			var item = new TorusObject3D();
			item.Rebuild(null);
			return item;
		}

		public double InnerDiameter { get; set; } = 10;
		public double OuterDiameter { get; set; } = 20;
		public int Sides { get; set; } = 30;

		public bool Advanced { get; set; } = false;
		[DisplayName("Ring Sides")]
		public int RingSides { get; set; } = 15;
		public double StartingAngle { get; set; } = 0;
		public double EndingAngle { get; set; } = 360;

		public void Rebuild(UndoBuffer undoBuffer)
		{
			var ringSides = RingSides;
			var startingAngle = StartingAngle;
			var endingAngle = EndingAngle;
			if(!Advanced)
			{
				ringSides = Math.Max(3, (int)(Sides / 2));
				startingAngle = 0;
				endingAngle = 360;
			}

			var innerDiameter = Math.Min(OuterDiameter - .1, InnerDiameter);

			var aabb = this.GetAxisAlignedBoundingBox();

			var poleRadius = (OuterDiameter / 2 - innerDiameter / 2) / 2;
			var toroidRadius = innerDiameter / 2 + poleRadius;
			var path = new VertexStorage();
			var angleDelta = MathHelper.Tau / ringSides;
			var angle = 0.0;
			var circleCenter = new Vector2(toroidRadius, 0);
			path.MoveTo(circleCenter + new Vector2(poleRadius * Math.Cos(angle), poleRadius * Math.Sin(angle)));
			for (int i = 0; i < ringSides - 1; i++)
			{
				angle += angleDelta;
				path.LineTo(circleCenter + new Vector2(poleRadius * Math.Cos(angle), poleRadius * Math.Sin(angle)));
			}

			path.LineTo(circleCenter + new Vector2(poleRadius * Math.Cos(0), poleRadius * Math.Sin(0)));

			var startAngle = MathHelper.Range0ToTau(MathHelper.DegreesToRadians(startingAngle));
			var endAngle = MathHelper.Range0ToTau(MathHelper.DegreesToRadians(endingAngle));
			Mesh = VertexSourceToMesh.Revolve(path, Sides, startAngle, endAngle);

			if (aabb.ZSize > 0)
			{
				// If the part was already created and at a height, maintain the height.
				PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
			}
		}

		public void UpdateControls(PublicPropertyEditor editor)
		{
			editor.GetEditRow((this.ID, nameof(RingSides))).Visible = Advanced;
			editor.GetEditRow((this.ID, nameof(StartingAngle))).Visible = Advanced;
			editor.GetEditRow((this.ID, nameof(EndingAngle))).Visible = Advanced;
			InnerDiameter = Math.Min(OuterDiameter - .1, InnerDiameter);
		}
	}
}