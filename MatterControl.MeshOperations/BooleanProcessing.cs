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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DualContouring;
using g3;
using gs;
using MatterHackers.Agg;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public static class BooleanProcessing
	{
		public enum CsgModes
		{
			Union,
			Subtract,
			Intersect
		}

		public enum IplicitSurfaceMesh
		{
			[Description("Faster but less accurate")]
			Grid,
			[Description("Slower but more accurate")]
			Exact
		};

		public enum ProcessingModes
		{
			Exact,
			Volume_64,
			Volume_128,
			Volume_256,
		}

		private const string BooleanAssembly = "609_Boolean_bin.dll";

		[DllImport(BooleanAssembly, CallingConvention = CallingConvention.Cdecl)]
		public static extern int DeleteDouble(ref IntPtr handle);

		[DllImport(BooleanAssembly, CallingConvention = CallingConvention.Cdecl)]
		public static extern int DeleteInt(ref IntPtr handle);

		[DllImport(BooleanAssembly, CallingConvention = CallingConvention.Cdecl)]
		public static extern void DoBooleanOperation(double[] va, int vaCount, int[] fa, int faCount, double[] vb, int vbCount, int[] fb, int fbCount, int operation, out IntPtr pVc, out int vcCount, out IntPtr pVf, out int vfCount);

		public static Mesh DoArray(IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			bool exactSurface,
			ProcessingModes processingMode,
			IProgress<ProgressStatus> reporter,
			CancellationToken cancellationToken)
		{
			if (processingMode == ProcessingModes.Exact)
			{
				var progressStatus = new ProgressStatus();
				var totalOperations = items.Count() - 1;
				double amountPerOperation = 1.0 / totalOperations;
				double percentCompleted = 0;

				var first = true;
				var resultsMesh = items.First().mesh;
				var firstWorldMatrix = items.First().matrix;

				foreach (var item in items)
				{
					if (first)
					{
						first = false;
						continue;
					}

					var itemWorldMatrix = item.matrix;
					resultsMesh = Do(item.mesh,
						itemWorldMatrix,
						// other mesh
						resultsMesh,
						firstWorldMatrix,
						// operation
						operation,
						processingMode,
						// reporting
						reporter,
						amountPerOperation,
						percentCompleted,
						progressStatus,
						cancellationToken);

					// after the first union we are working with the transformed mesh and don't need the first transform
					firstWorldMatrix = Matrix4X4.Identity;

					percentCompleted += amountPerOperation;
					progressStatus.Progress0To1 = percentCompleted;
					reporter?.Report(progressStatus);
				}

				return resultsMesh;
			}
			else
			{
				var resolution = 64;
				switch (processingMode)
				{
					case ProcessingModes.Volume_128:
						resolution = 128;
						break;

					case ProcessingModes.Volume_256:
						resolution = 256;
						break;
				}
				var marchingCells = resolution;
				var implicitCells = exactSurface ? 0 : resolution * 4;

				var implicitMeshs = new List<BoundedImplicitFunction3d>();
				foreach (var item in items)
				{
					implicitMeshs.Add(GetImplicitFromMesh(item.mesh, item.matrix, implicitCells));
				}

				DMesh3 GenerateMeshF(BoundedImplicitFunction3d root, int numCells)
				{
					var bounds = root.Bounds();

					var c = new MarchingCubesPro()
					{
						Implicit = root,
						RootMode = MarchingCubesPro.RootfindingModes.LerpSteps,      // cube-edge convergence method
						RootModeSteps = 5,                                        // number of iterations
						Bounds = bounds,
						CubeSize = bounds.MaxDim / numCells,
					};

					c.Bounds.Expand(3 * c.CubeSize);                            // leave a buffer of cells
					c.Generate();

					MeshNormals.QuickCompute(c.Mesh);                           // generate normals
					return c.Mesh;
				}

				switch (operation)
				{
					case CsgModes.Union:
						{
							var bounds = implicitMeshs.First().Bounds();
							var root = Octree.BuildOctree((pos) =>
							{
								var pos2 = new Vector3d(pos.X, pos.Y, pos.Z);
								return implicitMeshs.First().Value(ref pos2);
							}, new Vector3(bounds.Min.x, bounds.Min.y, bounds.Min.z),
							new Vector3(bounds.Width, bounds.Depth, bounds.Height),
							5,
							.001);
							return Octree.GenerateMeshFromOctree(root);

							return GenerateMeshF(new ImplicitNaryUnion3d()
							{
								Children = implicitMeshs
							}, marchingCells).ToMesh();
						}

					case CsgModes.Subtract:
						{
							var bounds = implicitMeshs.First().Bounds();
							var root = Octree.BuildOctree((pos) =>
							{
								var pos2 = new Vector3d(pos.X, pos.Y, pos.Z);
								return implicitMeshs.First().Value(ref pos2);
							}, new Vector3(bounds.Min.x, bounds.Min.y, bounds.Min.z),
							new Vector3(bounds.Width, bounds.Depth, bounds.Height),
							5,
							.001);
							return Octree.GenerateMeshFromOctree(root);

							return GenerateMeshF(new ImplicitNaryDifference3d()
							{
								A = implicitMeshs.First(),
								BSet = implicitMeshs.GetRange(0, implicitMeshs.Count - 1)
							}, marchingCells).ToMesh();
						}

					case CsgModes.Intersect:
						return GenerateMeshF(new ImplicitNaryIntersection3d()
						{
							Children = implicitMeshs
						}, marchingCells).ToMesh();
				}
			}

			return null;
		}

		public static Mesh Do(Mesh inMeshA,
			Matrix4X4 matrixA,
			// mesh B
			Mesh inMeshB,
			Matrix4X4 matrixB,
			// operation
			CsgModes operation,
			ProcessingModes processingMode,
			// reporting
			IProgress<ProgressStatus> reporter,
			double amountPerOperation,
			double percentCompleted,
			ProgressStatus progressStatus,
			CancellationToken cancellationToken)
		{
			bool externalAssemblyExists = File.Exists(BooleanAssembly);
			if (processingMode == ProcessingModes.Exact)
			{
				// only try to run the improved booleans if we are 64 bit and it is there
				if (externalAssemblyExists && IntPtr.Size == 8)
				{
					IntPtr pVc = IntPtr.Zero;
					IntPtr pFc = IntPtr.Zero;
					try
					{
						double[] va;
						int[] fa;
						va = inMeshA.Vertices.ToDoubleArray(matrixA);
						fa = inMeshA.Faces.ToIntArray();
						double[] vb;
						int[] fb;
						vb = inMeshB.Vertices.ToDoubleArray(matrixB);
						fb = inMeshB.Faces.ToIntArray();

						DoBooleanOperation(va,
							va.Length,
							fa,
							fa.Length,
							// object B
							vb,
							vb.Length,
							fb,
							fb.Length,
							// operation
							(int)operation,
							// results
							out pVc,
							out int vcCount,
							out pFc,
							out int fcCount);

						var vcArray = new double[vcCount];
						Marshal.Copy(pVc, vcArray, 0, vcCount);

						var fcArray = new int[fcCount];
						Marshal.Copy(pFc, fcArray, 0, fcCount);

						return new Mesh(vcArray, fcArray);
					}
					catch (Exception ex)
					{
						//ApplicationController.Instance.LogInfo("Error performing boolean operation: ");
						//ApplicationController.Instance.LogInfo(ex.Message);
					}
					finally
					{
						if (pVc != IntPtr.Zero)
						{
							DeleteDouble(ref pVc);
						}

						if (pFc != IntPtr.Zero)
						{
							DeleteInt(ref pFc);
						}

						if (progressStatus != null)
						{
							progressStatus.Progress0To1 = percentCompleted + amountPerOperation;
							reporter.Report(progressStatus);
						}
					}
				}
				else
				{
					Console.WriteLine($"libigl skipped - AssemblyExists: {externalAssemblyExists}; Is64Bit: {IntPtr.Size == 8};");

					var meshA = inMeshA.Copy(CancellationToken.None);
					meshA.Transform(matrixA);

					var meshB = inMeshB.Copy(CancellationToken.None);
					meshB.Transform(matrixB);

					switch (operation)
					{
						case CsgModes.Union:
							return Csg.CsgOperations.Union(meshA,
								meshB,
								(status, progress0To1) =>
								{
									// Abort if flagged
									cancellationToken.ThrowIfCancellationRequested();
									progressStatus.Status = status;
									progressStatus.Progress0To1 = percentCompleted + (amountPerOperation * progress0To1);
									reporter?.Report(progressStatus);
								},
								cancellationToken);
						
						case CsgModes.Subtract:
							return Csg.CsgOperations.Subtract(meshA,
								meshB,
								(status, progress0To1) =>
								{
									progressStatus.Status = status;
									progressStatus.Progress0To1 = percentCompleted + (amountPerOperation * progress0To1);
									reporter?.Report(progressStatus);
								},
								cancellationToken);
						
						case CsgModes.Intersect:
							return Csg.CsgOperations.Intersect(meshA,
								meshB,
								(status, progress0To1) =>
								{
									// Abort if flagged
									cancellationToken.ThrowIfCancellationRequested(); progressStatus.Status = status;
									progressStatus.Progress0To1 = percentCompleted + (amountPerOperation * progress0To1);
									reporter.Report(progressStatus);
								},
								cancellationToken);
					}
				}
			}
			else
			{
				var resolution = 64;
				switch (processingMode)
				{
					case ProcessingModes.Volume_128:
						resolution = 128;
						break;

					case ProcessingModes.Volume_256:
						resolution = 256;
						break;
				}
				var marchingCells = resolution;
				var implicitCells = resolution;
				var implicitA = GetImplicitFromMesh(inMeshA, matrixA, implicitCells);
				var implicitB = GetImplicitFromMesh(inMeshB, matrixB, implicitCells);

				DMesh3 GenerateMeshF(BoundedImplicitFunction3d root, int numCells)
				{
					var bounds = root.Bounds();

					var c = new MarchingCubes()
					{
						Implicit = root,
						RootMode = MarchingCubes.RootfindingModes.LerpSteps,      // cube-edge convergence method
						RootModeSteps = 5,                                        // number of iterations
						Bounds = bounds,
						CubeSize = bounds.MaxDim / numCells,
					};

					c.Bounds.Expand(3 * c.CubeSize);                            // leave a buffer of cells
					c.Generate();

					MeshNormals.QuickCompute(c.Mesh);                           // generate normals
					return c.Mesh;
				}

				switch (operation)
				{
					case CsgModes.Union:
						return GenerateMeshF(new ImplicitUnion3d()
						{
							A = implicitA,
							B = implicitB
						}, marchingCells).ToMesh();

					case CsgModes.Subtract:
						return GenerateMeshF(new ImplicitDifference3d()
						{
							A = implicitA,
							B = implicitB
						}, marchingCells).ToMesh();

					case CsgModes.Intersect:
						return GenerateMeshF(new ImplicitIntersection3d()
						{
							A = implicitA,
							B = implicitB
						}, marchingCells).ToMesh();
				}
			}

			return null;
		}

		class MWNImplicit : BoundedImplicitFunction3d
		{
			public DMeshAABBTree3 MeshAABBTree3;
			public AxisAlignedBox3d Bounds() { return MeshAABBTree3.Bounds; }
			public double Value(ref Vector3d pt)
			{
				return -(MeshAABBTree3.FastWindingNumber(pt) - 0.5);
			}
		}

		public static BoundedImplicitFunction3d GetImplicitFromMesh(Mesh mesh, Matrix4X4 matrix, int numCells)
		{
			var meshCopy = mesh.Copy(CancellationToken.None);
			meshCopy.Transform(matrix);

			var meshA3 = meshCopy.ToDMesh3();

			// Interesting experiment, this produces an extremely accurate surface representation but is quite slow (even though fast) compared to voxel lookups.
			if (numCells == 0)
			{
				DMeshAABBTree3 meshAABBTree3 = new DMeshAABBTree3(meshA3, true);
				meshAABBTree3.FastWindingNumber(Vector3d.Zero);   // build approximation
				return new MWNImplicit()
				{
					MeshAABBTree3 = meshAABBTree3
				};
			}
			else
			{
				double meshCellsize = meshA3.CachedBounds.MaxDim / numCells;
				var signedDistance = new MeshSignedDistanceGrid(meshA3, meshCellsize);
				signedDistance.Compute();
				return new DenseGridTrilinearImplicit(signedDistance.Grid, signedDistance.GridOrigin, signedDistance.CellSize);
			}
		}
	}
}