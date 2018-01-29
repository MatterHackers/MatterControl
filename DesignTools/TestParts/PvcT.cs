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
using System.ComponentModel;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class PvcT : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		private int sides = 50;

		public PvcT()
		{
			Rebuild();
		}

		[DisplayName("Inner Radius")]
		public double InnerDiameter { get; set; } = 15;

		[DisplayName("Outer Radius")]
		public double OuterDiameter { get; set; } = 20;

		public double BottomReach { get; set; } = 30;

		public double FrontReach { get; set; } = 25;

		public double TopReach { get; set; } = 30;

		public void Rebuild()
		{
			IObject3D topBottomConnect = new CylinderAdvancedObject3D(OuterDiameter / 2, OuterDiameter, sides, Alignment.Y);
			IObject3D frontConnect = new CylinderAdvancedObject3D(OuterDiameter / 2, OuterDiameter / 2, sides, Alignment.X);
			frontConnect = new Align(frontConnect, Face.Right, topBottomConnect, Face.Right);

			IObject3D bottomReach = new Rotate(CreateReach(BottomReach), -MathHelper.Tau / 4);
			bottomReach = new Align(bottomReach, Face.Back, topBottomConnect, Face.Front, 0, .1);

			IObject3D topReach = new Rotate(CreateReach(TopReach), MathHelper.Tau / 4);
			topReach = new Align(topReach, Face.Front, topBottomConnect, Face.Back, 0, -.1);

			IObject3D frontReach = new Rotate(CreateReach(FrontReach), 0, -MathHelper.Tau / 4);
			frontReach = new Align(frontReach, Face.Left, topBottomConnect, Face.Right, -.1);

			// output multiple meshes for pipe connector
			this.Children.Modify(list =>
			{
				list.Clear();
				list.Add(topBottomConnect);
				list.Add(frontConnect);
				list.Add(bottomReach);
				list.Add(topReach);
				list.Add(frontReach);
			});

			this.Color = Color.Transparent;
			this.Mesh = null;
		}

		private IObject3D CreateReach(double reach)
		{
			var finWidth = 4.0;
			var finLength = InnerDiameter;

			var pattern = new VertexStorage();
			pattern.MoveTo(0, 0);
			pattern.LineTo(finLength/2, 0);
			pattern.LineTo(finLength/2, reach - finLength / 8);
			pattern.LineTo(finLength/2 - finLength / 8, reach);
			pattern.LineTo(-finLength/2 + finLength / 8, reach);
			pattern.LineTo(-finLength/2, reach - finLength / 8);
			pattern.LineTo(-finLength/2, 0);

			var fin1 = new Object3D()
			{
				Mesh = VertexSourceToMesh.Extrude(pattern, finWidth)
			};
			fin1 = new Translate(fin1, 0, 0, -finWidth / 2);
			//fin1.ChamferEdge(Face.Top | Face.Back, finLength / 8);
			//fin1.ChamferEdge(Face.Top | Face.Front, finLength / 8);
			fin1 = new Rotate(fin1, -MathHelper.Tau / 4);
			var fin2 = new SetCenter(new Rotate(fin1, 0, 0, MathHelper.Tau / 4), fin1.GetCenter());

			return new Object3D().SetChildren(new List<IObject3D>() { fin1, fin2 });
		}
	}
}