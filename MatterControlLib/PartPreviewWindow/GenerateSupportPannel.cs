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
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.Library;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GenerateSupportPannel : GuiWidget
	{
		private ThemeConfig theme;
		private InteractiveScene scene;

		public GenerateSupportPannel(ThemeConfig theme, InteractiveScene scene)
		{
			this.theme = theme;
			this.scene = scene;
		}

		public double MaxOverHangAngle { get; private set; }

		private void Rebuild()
		{
			// Find all the other objects of our parent
			var peers = scene.Children.Where(i => i != this).ToArray();

			// eventually we will not remove any support that is already in the scene
			// but for now, remove all the stuff that is there first
			var existingSupports = peers.Where(i => i.OutputType == PrintOutputTypes.Support);

			scene.Children.Modify((list) =>
			{
				foreach (var item in existingSupports)
				{
					list.Remove(item);
				}
			});

			// Get visible meshes for each of them
			var visibleMeshes = peers.SelectMany(i => i.VisibleMeshes());

			var supportCandidates = visibleMeshes.Where(i => i.OutputType != PrintOutputTypes.Support);

			// find all the faces that are candidates for support
			var verts = new Vector3List();
			var faces = new FaceList();
			foreach (var item in supportCandidates)
			{
				var matrix = item.WorldMatrix(scene);
				foreach (var face in item.Mesh.Faces)
				{
					var face0Normal = Vector3.TransformVector(face.Normal, matrix).GetNormal();
					var angle = MathHelper.RadiansToDegrees(Math.Acos(Vector3.Dot(Vector3.UnitZ, face0Normal)));

					if (angle < MaxOverHangAngle)
					{
						foreach (var triangle in face.AsTriangles())
						{
							faces.Add(new int[] { verts.Count, verts.Count + 1, verts.Count + 2 });
							verts.Add(Vector3.Transform(triangle.p0, matrix));
							verts.Add(Vector3.Transform(triangle.p0, matrix));
							verts.Add(Vector3.Transform(triangle.p0, matrix));
						}
					}
				}
			}
			// separate the faces into face patch groups (these are the new support tops)
			// project all the vertecies of each patch group down until they hit an up face in the scene (or 0)
			// make a new patch group at the z of the hit (thes will bthe bottoms)
			// find the outline of the patch groups (these will be the walls of the top and bottom patches
			// make a new mesh object with the top, bottom and walls, add it to the scene and mark it as support
		}
	}
}