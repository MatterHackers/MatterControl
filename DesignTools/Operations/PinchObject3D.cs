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

using System.ComponentModel;
using System.Linq;
using System.Threading;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class PinchObject3D : MeshWrapperObject3D, IPublicPropertyObject
	{
		[DisplayName("Back Ratio")]
		public double PinchRatio { get; set; } = 1;

		public PinchObject3D()
		{
		}

		public override void Rebuild(UndoBuffer undoBuffer)
		{
			Rebuilding = true;
			ResetMeshWrappers(Object3DPropertyFlags.All);

			// remember the current matrix then clear it so the parts will rotate at the original wrapped position
			var currentMatrix = Matrix;
			Matrix = Matrix4X4.Identity;

			var meshWrapper = this.Descendants()
				.Where((obj) => obj.OwnerID == this.ID).ToList();

			// reset the positions before we take the aabb
			foreach (var items in meshWrapper.Select((mw) => (Original: mw.Children.First(),
				 Pinched: mw)))
			{
				var pinchedMesh = items.Pinched.Mesh;
				var originalMesh = items.Original.Mesh;

				for (int i = 0; i < pinchedMesh.Vertices.Count; i++)
				{
					pinchedMesh.Vertices[i].Position = originalMesh.Vertices[i].Position;
				}

				pinchedMesh.MarkAsChanged();
			}

			var aabb = this.GetAxisAlignedBoundingBox();

			foreach (var items in meshWrapper.Select((mw) => (Original: mw.Children.First(),
				 Transformed: mw)))
			{
				var transformedMesh = items.Transformed.Mesh;
				var originalMesh = items.Original.Mesh;
				var itemMatrix = items.Original.WorldMatrix(this);
				var invItemMatrix = itemMatrix;
				invItemMatrix.Invert();
				// make sure we are working with a copy
				if (transformedMesh == originalMesh)
				{
					// Make sure the mesh we are going to copy is in a good state to be copied (so we maintain vertex count)
					originalMesh.CleanAndMergeMesh(CancellationToken.None);
					transformedMesh = Mesh.Copy(originalMesh, CancellationToken.None);
					items.Transformed.Mesh = transformedMesh;
				}
				for (int i = 0; i < originalMesh.Vertices.Count; i++)
				{
					var pos = originalMesh.Vertices[i].Position;
					pos = Vector3.Transform(pos, itemMatrix);

					var ratioToApply = PinchRatio;

					var distFromCenter = pos.X - aabb.Center.X;
					var distanceToPinch = distFromCenter * (1 - PinchRatio);
					var delta = (aabb.Center.X + distFromCenter * ratioToApply) - pos.X;

					// find out how much to pinch based on y position
					var amountOfRatio = (pos.Y - aabb.minXYZ.Y) / aabb.YSize;

					var newPos = new Vector3(pos.X + delta * amountOfRatio, pos.Y, pos.Z);
					newPos = Vector3.Transform(newPos, invItemMatrix);

					transformedMesh.Vertices[i].Position = newPos;
				}

				transformedMesh.MarkAsChanged();
				transformedMesh.CalculateNormals();
				
				// set the matrix back
				Matrix = currentMatrix;

				Rebuilding = false;
			}
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix)
				&& invalidateType.Source != this
				&& !Rebuilding)
			{
				Rebuild(null);
			}
			base.OnInvalidate(invalidateType);
		}
	}
}