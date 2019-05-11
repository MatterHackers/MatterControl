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

/*********************************************************************/
/**************************** OBSOLETE! ******************************/
/************************ USE NEWER VERSION **************************/
/*********************************************************************/

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools
{
	[Obsolete("Use PinchObject3D_2 instead", false)]
	public class PinchObject3D : MeshWrapperObject3D
	{
		public PinchObject3D()
		{
			Name = "Pinch".Localize();
		}

		[DisplayName("Back Ratio")]
		public double PinchRatio { get; set; } = .5;

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override Task Rebuild()
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
						var pos = originalMesh.Vertices[i];
						pos = pos.Transform(itemMatrix);

						var ratioToApply = PinchRatio;

						var distFromCenter = pos.X - aabb.Center.X;
						var distanceToPinch = distFromCenter * (1 - PinchRatio);
						var delta = (aabb.Center.X + distFromCenter * ratioToApply) - pos.X;

						// find out how much to pinch based on y position
						var amountOfRatio = (pos.Y - aabb.MinXYZ.Y) / aabb.YSize;

						var newPos = new Vector3Float(pos.X + delta * amountOfRatio, pos.Y, pos.Z);

						transformedMesh.Vertices[i] = newPos.Transform(invItemMatrix);
					}

					transformedMesh.CalculateNormals();
				}

				// set the matrix back
				Matrix = currentMatrix;
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
			return Task.CompletedTask;
		}
	}
}