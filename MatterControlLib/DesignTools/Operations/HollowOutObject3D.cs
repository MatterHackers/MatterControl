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

using g3;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools
{
	public class HollowOutObject3D : OperationSourceContainerObject3D
	{
		public HollowOutObject3D()
		{
			Name = "Hollow Out".Localize();
		}

		[DisplayName("Back Ratio")]
		public double PinchRatio { get; set; } = .5;

		public void HollowOut()
		{

		}

		/// <summary>
		/// Good for reducing a mesh.
		/// http://www.gradientspace.com/tutorials/2017/8/30/mesh-simplification
		/// </summary>
		/// <param name="mesh"></param>
		/// <returns></returns>
		public Mesh Reduce(Mesh inMesh)
		{
			DMesh3 mesh = MeshToDMesh3(inMesh);

			var maintainSurface = true;

			MeshProjectionTarget target = null;

			if (maintainSurface)
			{
				var tree = new DMeshAABBTree3(new DMesh3(mesh));
				tree.Build();
				target = new MeshProjectionTarget(tree.Mesh, tree);
			}

			Reducer reducer = new Reducer(mesh);
			var preserveBoundries = true;
			if (preserveBoundries)
			{
				reducer.SetExternalConstraints(new MeshConstraints());
				MeshConstraintUtil.FixAllBoundaryEdges(reducer.Constraints, mesh);
			}

			if (target != null)
			{
				reducer.SetProjectionTarget(target);
				reducer.ProjectionMode = Reducer.TargetProjectionMode.Inline;
			}

			reducer.ReduceToTriangleCount(500);

			return DMesh3ToMesh(reducer.Mesh);
		}

		private DMesh3 MeshToDMesh3(Mesh inMesh)
		{
			throw new NotImplementedException();
		}

		private Mesh DMesh3ToMesh(DMesh3 mesh)
		{
			throw new NotImplementedException();
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

			return ApplicationController.Instance.Tasks.Execute(
				"Pinch".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					SourceContainer.Visible = true;
					RemoveAllButSource();

					// remember the current matrix then clear it so the parts will rotate at the original wrapped position
					var currentMatrix = Matrix;
					Matrix = Matrix4X4.Identity;

					var aabb = SourceContainer.GetAxisAlignedBoundingBox();

					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var originalMesh = sourceItem.Mesh;
						var transformedMesh = originalMesh.Copy(CancellationToken.None);
						var itemMatrix = sourceItem.WorldMatrix(SourceContainer);
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

						var newMesh = new Object3D()
						{
							Mesh = transformedMesh
						};
						newMesh.CopyWorldProperties(sourceItem, this, Object3DPropertyFlags.All);
						this.Children.Add(newMesh);
					}

					// set the matrix back
					Matrix = currentMatrix;
					SourceContainer.Visible = false;
					rebuildLocks.Dispose();
					Invalidate(InvalidateType.Children);
					return Task.CompletedTask;
				});
		}
	}
}