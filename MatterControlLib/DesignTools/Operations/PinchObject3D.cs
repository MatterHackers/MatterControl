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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.VectorMath;
using System.ComponentModel;
using System.Threading;

namespace MatterHackers.MatterControl.DesignTools
{
	public class PinchObject3D : MeshWrapperObject3D
	{
		public PinchObject3D()
		{
			Name = "Pinch".Localize();
			// TODO: This feels like the wronge approach. I think changing the safe list should always
			// call into invalidate for every Object3D (this will cause a lot of classes to need to be fixed).
			Children.ItemsModified += Children_ItemsModified;
		}

		private void Children_ItemsModified(object sender, System.EventArgs e)
		{
			OnInvalidate(new InvalidateArgs(null, InvalidateType.Content));
		}

		[DisplayName("Back Ratio")]
		public double PinchRatio { get; set; } = .5;

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix
				|| invalidateType.InvalidateType == InvalidateType.Mesh)
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				Rebuild(null);
			}
			else if (invalidateType.InvalidateType == InvalidateType.Properties
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
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				ResetMeshWrapperMeshes(Object3DPropertyFlags.All, CancellationToken.None);

				// remember the current matrix then clear it so the parts will rotate at the original wrapped position
				var currentMatrix = Matrix;
				Matrix = Matrix4X4.Identity;

				var aabb = this.GetAxisAlignedBoundingBox();

				foreach (var items in this.WrappedObjects())
				{
					var transformedMesh = items.meshCopy.Mesh;
					var originalMesh = items.original.Mesh;
					var itemMatrix = items.original.WorldMatrix(this);
					var invItemMatrix = itemMatrix.Inverted;

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

						transformedMesh.Vertices[i].Position = Vector3.Transform(newPos, invItemMatrix);
					}

					// the vertices need to be resorted as they have moved relative to each other
					transformedMesh.Vertices.Sort();

					transformedMesh.MarkAsChanged();
					transformedMesh.CalculateNormals();
				}

				// set the matrix back
				Matrix = currentMatrix;
			}

			base.Invalidate(new InvalidateArgs(this, InvalidateType.Content));
		}
	}
}