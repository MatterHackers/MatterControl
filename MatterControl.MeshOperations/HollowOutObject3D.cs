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
using System.Threading;
using System.Threading.Tasks;
using g3;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.DesignTools
{
	public class HollowOutObject3D : OperationSourceContainerObject3D
	{
		public HollowOutObject3D()
		{
			Name = "Hollow Out".Localize();
		}

		public double Distance { get; set; } = 2;

		private readonly Func<DMesh3, int, double, BoundedImplicitFunction3d> meshToImplicitF = (meshIn, numcells, max_offset) =>
		{
			double meshCellsize = meshIn.CachedBounds.MaxDim / numcells;
			var levelSet = new MeshSignedDistanceGrid(meshIn, meshCellsize);
			levelSet.ExactBandWidth = (int)(max_offset / meshCellsize) + 1;
			levelSet.Compute();
			return new DenseGridTrilinearImplicit(levelSet.Grid, levelSet.GridOrigin, levelSet.CellSize);
		};

		private readonly Func<BoundedImplicitFunction3d, int, DMesh3> generateMeshF = (root, numcells) => {
			var c = new MarchingCubes();
			c.Implicit = root;
			c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;      // cube-edge convergence method
			c.RootModeSteps = 5;                                        // number of iterations
			c.Bounds = root.Bounds();
			c.CubeSize = c.Bounds.MaxDim / numcells;
			c.Bounds.Expand(3 * c.CubeSize);                            // leave a buffer of cells
			c.Generate();
			MeshNormals.QuickCompute(c.Mesh);                           // generate normals
			return c.Mesh;
		};

		public Mesh HollowOut(Mesh inMesh)
		{
			var mesh = inMesh.ToDMesh3();

			BoundedImplicitFunction3d implicitFunction = meshToImplicitF(mesh, 64, Distance);

			var implicitMesh = generateMeshF(implicitFunction, 128);
			var insetMesh = generateMeshF(new ImplicitOffset3d() { A = implicitFunction, Offset = -Distance }, 128);

			return insetMesh.ToMesh();
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

			var valuesChanged = false;

			return TaskBuilder(
				"Hollow".Localize(),
				(reporter, cancellationToken) =>
				{
					SourceContainer.Visible = true;
					RemoveAllButSource();

					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var originalMesh = sourceItem.Mesh;
						var combinedMesh = originalMesh.Copy(CancellationToken.None);

						// get the interior mesh and reverse it
						var interior = HollowOut(originalMesh);
						interior.ReverseFaces();

						// now add all the faces to the combinedMesh
						combinedMesh.CopyFaces(interior);

						var newMesh = new Object3D()
						{
							Mesh = combinedMesh
						};
						newMesh.CopyProperties(sourceItem, Object3DPropertyFlags.All);
						this.Children.Add(newMesh);
					}

					SourceContainer.Visible = false;
					rebuildLocks.Dispose();

					if (valuesChanged)
					{
						Invalidate(InvalidateType.DisplayValues);
					}

					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));

					return Task.CompletedTask;
				});
		}
	}
}