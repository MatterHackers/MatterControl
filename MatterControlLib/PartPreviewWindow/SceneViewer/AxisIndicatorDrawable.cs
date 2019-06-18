/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class AxisIndicatorDrawable : IDrawable
	{
		private readonly double big = 10;
		private readonly double small = 1;
		protected readonly List<(Mesh mesh, Color color)> meshes = new List<(Mesh, Color)>();

		public AxisIndicatorDrawable()
		{
			meshes.Add((GetMesh(0, 1), Color.Red));

			meshes.Add((GetMesh(1, 1), Color.Green));

			meshes.Add((GetMesh(2, 1), Color.Blue));
		}

		private Mesh GetMesh(int axis, int direction)
		{
			var scale = Vector3.One;
			scale[axis] = big;
			scale[(axis + 1) % 3] = small;
			scale[(axis + 2) % 3] = small;
			Mesh mesh = PlatonicSolids.CreateCube(scale);
			var translate = Vector3.Zero;
			translate[axis] = big / 2 * direction;
			mesh.Transform(Matrix4X4.CreateTranslation(translate));

			return mesh;
		}

		public bool Enabled { get; set; }

		public string Title { get; protected set; } = "Axis Indicator";

		public string Description { get; protected set; } = "Render Axis Indicator at origin";

		public DrawStage DrawStage { get; } = DrawStage.OpaqueContent;

		public virtual void Draw(GuiWidget sender, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			foreach (var mesh in meshes)
			{
				GLHelper.Render(mesh.mesh, mesh.color);
			}
		}
	}
}