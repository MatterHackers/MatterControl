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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.ComponentModel;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools
{
	[ShowUpdateButton]
	public class SupportControls : Object3D
	{
		public SupportControls()
		{
			Name = "Support Controls".Localize();
			Color = Color.Yellow;
		}

		public SupportControls(double width, double depth, double height)
			: this()
		{
			Rebuild(null);
		}

		//[JsonConverter(typeof(StringEnumConverter))]
		//public enum SupportTypes { Solid, Pillars, Tree }

		//[Description("Sets the type of support will be added to the scene and output by the slicing engine.")]
		//public SupportTypes SupportType { get; set; } = SupportTypes.Solid;

		[Description("The angle of the faces that need to be supported.")]
		public double MaxOverHangAngle { get; set; } = 45;

		// Clear All Supports // Remove all the supports that are currently in the scene
		// Group All Supports // Make supports into a sigle grouped object
		// Generate Supports // anywhere we need support and there is not currently support there, add support

		public static SupportControls Create()
		{
			var item = new SupportControls();

			item.Mesh = PlatonicSolids.CreateCube(20, 20, 20);

			PlatingHelper.PlaceMeshAtHeight(item, 0);

			return item;
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType == InvalidateType.Properties
				&& invalidateType.Source == this)
			{
				Rebuild(null);
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		private void Rebuild(UndoBuffer undoBuffer)
		{
			var parent = this.Parent;

			// Find all the other objects of our parent
			var peers = parent.Children.Where(i => i != this).ToArray();

			// eventually we will not remove any support that is already in the scene
			// but for now, remove all the stuff that is there first
			var existingSupports = peers.Where(i => i.OutputType == PrintOutputTypes.Support);

			parent.Children.Modify((list) =>
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
			foreach(var item in supportCandidates)
			{
				var matrix = item.WorldMatrix(parent);
				foreach(var face in item.Mesh.Faces)
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