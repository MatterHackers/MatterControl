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

using System.Threading;
using System.Threading.Tasks;
using g3;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.MatterControl.DesignTools
{
    public class HollowOutObject3D : OperationSourceContainerObject3D
	{
		public HollowOutObject3D()
		{
			Name = "Hollow Out".Localize();
		}

		public override bool Persistable => ApplicationController.Instance.UserHasPermission(this);

		public double Distance { get; set; } = 2;

		public int NumCells { get; set; } = 64;

		private static DMesh3 GenerateMeshF(BoundedImplicitFunction3d root, int numcells)
		{
			var bounds = root.Bounds();

			var c = new MarchingCubes()
			{
				Implicit = root,
				RootMode = MarchingCubes.RootfindingModes.LerpSteps,      // cube-edge convergence method
				RootModeSteps = 5,                                        // number of iterations
				Bounds = bounds,
				CubeSize = bounds.MaxDim / numcells,
			};

			c.Bounds.Expand(3 * c.CubeSize);                            // leave a buffer of cells
			c.Generate();

			MeshNormals.QuickCompute(c.Mesh);                           // generate normals
			return c.Mesh;
		}

		public static Mesh HollowOut(Mesh inMesh, double distance, int numCells)
		{
			// Convert to DMesh3
			var mesh = inMesh.ToDMesh3();

			// Create instance of BoundedImplicitFunction3d interface
			double meshCellsize = mesh.CachedBounds.MaxDim / numCells;

			var levelSet = new MeshSignedDistanceGrid(mesh, meshCellsize)
			{
				ExactBandWidth = (int)(distance / meshCellsize) + 1
			};
			levelSet.Compute();

			// Outer shell
			var implicitMesh = new DenseGridTrilinearImplicit(levelSet.Grid, levelSet.GridOrigin, levelSet.CellSize);

			// Offset shell
			var insetMesh = GenerateMeshF(
				new ImplicitOffset3d()
				{
					A = implicitMesh,
					Offset = -distance
				},
				numCells);

			// make sure it is a reasonable number of polygons
			// var reducer = new Reducer(insetMesh);
			// reducer.ReduceToTriangleCount(Math.Max(inMesh.Faces.Count / 2, insetMesh.TriangleCount / 10));

			// Convert to PolygonMesh and reverse faces
			var interior = insetMesh.ToMesh();
			interior.ReverseFaces();

			// Combine the original mesh with the reversed offset resulting in hollow
			var combinedMesh = inMesh.Copy(CancellationToken.None);
			combinedMesh.CopyFaces(interior);

			return combinedMesh;
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
						var newMesh = new Object3D()
						{
							Mesh = HollowOut(sourceItem.Mesh, this.Distance, this.NumCells)
						};
						newMesh.CopyProperties(sourceItem, Object3DPropertyFlags.All);
						this.Children.Add(newMesh);
					}

					SourceContainer.Visible = false;

					UiThread.RunOnIdle(() =>
					{
						rebuildLocks.Dispose();
						Invalidate(InvalidateType.DisplayValues);
						this.DoRebuildComplete();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					});

					return Task.CompletedTask;
				});
		}
	}
}