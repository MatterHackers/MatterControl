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
using ClipperLib;
using DualContouring;
using g3;
using gs;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public static class BooleanProcessing
	{
		public enum CsgModes
		{
			Union,
			Subtract,
			Intersect
		}

		public enum IplicitSurfaceMethod
		{
			[Description("Faster but less accurate")]
			Grid,
			[Description("Slower but more accurate")]
			Exact
		};

		public enum ProcessingModes
		{
			[Description("Default CSG processing")]
			Polygons,
			[Description("Use libigl (windows only)")]
			libigl,
			[Description("Experimental Marching Cubes")]
			Marching_Cubes,
			[Description("Experimental Dual Contouring")]
			Dual_Contouring,
		}

		public enum ProcessingResolution
		{
			_64 = 6,
			_128 = 7,
			_256 = 8,
			_512 = 9,
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
			ProcessingModes processingMode,
			ProcessingResolution inputResolution,
			ProcessingResolution outputResolution,
			IProgress<ProgressStatus> reporter,
			CancellationToken cancellationToken,
			double amountPerOperation = 1,
			double percentCompleted = 0)
		{
			if (processingMode == ProcessingModes.Polygons)
			{
				return ExactBySlicing(items, operation, reporter, cancellationToken, amountPerOperation, percentCompleted);
			}
			else if (processingMode == ProcessingModes.libigl)
			{
				return ExactLegacy(items, operation, processingMode, inputResolution, outputResolution, reporter, cancellationToken);
			}
			else
			{
				return AsImplicitMeshes(items, operation, processingMode, inputResolution, outputResolution);
			}
		}

		private static Mesh AsImplicitMeshes(IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items, CsgModes operation, ProcessingModes processingMode, ProcessingResolution inputResolution, ProcessingResolution outputResolution)
		{
			Mesh implicitResult = null;

			var implicitMeshs = new List<BoundedImplicitFunction3d>();
			foreach (var (mesh, matrix) in items)
			{
				var meshCopy = mesh.Copy(CancellationToken.None);
				meshCopy.Transform(matrix);

				implicitMeshs.Add(GetImplicitFunction(meshCopy, processingMode == ProcessingModes.Polygons, 1 << (int)inputResolution));
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
					if (processingMode == ProcessingModes.Dual_Contouring)
					{
						var union = new ImplicitNaryUnion3d()
						{
							Children = implicitMeshs
						};
						var bounds = union.Bounds();
						var size = bounds.Max - bounds.Min;
						var root = Octree.BuildOctree((pos) =>
						{
							var pos2 = new Vector3d(pos.X, pos.Y, pos.Z);
							return union.Value(ref pos2);
						}, new Vector3(bounds.Min.x, bounds.Min.y, bounds.Min.z),
						new Vector3(size.x, size.y, size.z),
						(int)outputResolution,
						.001);
						implicitResult = Octree.GenerateMeshFromOctree(root);
					}
					else
					{
						implicitResult = GenerateMeshF(new ImplicitNaryUnion3d()
						{
							Children = implicitMeshs
						}, 1 << (int)outputResolution).ToMesh();
					}
					break;

				case CsgModes.Subtract:
					{
						if (processingMode == ProcessingModes.Dual_Contouring)
						{
							var subtract = new ImplicitNaryIntersection3d()
							{
								Children = implicitMeshs
							};
							var bounds = subtract.Bounds();
							var root = Octree.BuildOctree((pos) =>
							{
								var pos2 = new Vector3d(pos.X, pos.Y, pos.Z);
								return subtract.Value(ref pos2);
							}, new Vector3(bounds.Min.x, bounds.Min.y, bounds.Min.z),
							new Vector3(bounds.Width, bounds.Depth, bounds.Height),
							(int)outputResolution,
							.001);
							implicitResult = Octree.GenerateMeshFromOctree(root);
						}
						else
						{
							implicitResult = GenerateMeshF(new ImplicitNaryDifference3d()
							{
								A = implicitMeshs.First(),
								BSet = implicitMeshs.GetRange(0, implicitMeshs.Count - 1)
							}, 1 << (int)outputResolution).ToMesh();
						}
					}
					break;

				case CsgModes.Intersect:
					if (processingMode == ProcessingModes.Dual_Contouring)
					{
						var intersect = new ImplicitNaryIntersection3d()
						{
							Children = implicitMeshs
						};
						var bounds = intersect.Bounds();
						var root = Octree.BuildOctree((pos) =>
						{
							var pos2 = new Vector3d(pos.X, pos.Y, pos.Z);
							return intersect.Value(ref pos2);
						}, new Vector3(bounds.Min.x, bounds.Min.y, bounds.Min.z),
						new Vector3(bounds.Width, bounds.Depth, bounds.Height),
						(int)outputResolution,
						.001);
						implicitResult = Octree.GenerateMeshFromOctree(root);
					}
					else
					{
						implicitResult = GenerateMeshF(new ImplicitNaryIntersection3d()
						{
							Children = implicitMeshs
						}, 1 << (int)outputResolution).ToMesh();
					}
					break;
			}

			return implicitResult;
		}

		private static Mesh ExactBySlicing(IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items2,
			CsgModes operation,
			IProgress<ProgressStatus> reporter,
			CancellationToken cancellationToken,
			double amountPerOperationIn = 1,
			double percentCompletedIn = 0)
		{
			var progressStatus = new ProgressStatus();
			var totalOperations = 0;
			var transformedMeshes = new List<Mesh>();
			foreach (var (mesh, matrix) in items2)
			{
				totalOperations += mesh.Faces.Count;
				transformedMeshes.Add(mesh.Copy(CancellationToken.None));
				transformedMeshes.Last().Transform(matrix);
			}

			double amountPerOperation = 1.0 / totalOperations * amountPerOperationIn;
			double percentCompleted = percentCompletedIn;

			var resultsMesh = new Mesh();

			// keep track of all the faces that added by their plane
			// the internal dictionary is sourceMeshIndex, sourceFaceIndex, destFaceIndex
			var coPlanarFaces = new CoPlanarFaces();

			for (var mesh1Index = 0; mesh1Index < transformedMeshes.Count; mesh1Index++)
			{
				var mesh1 = transformedMeshes[mesh1Index];

				for (int faceIndex = 0; faceIndex < mesh1.Faces.Count; faceIndex++)
				{
					var face = mesh1.Faces[faceIndex];

					var cutPlane = new Plane(mesh1.Vertices[face.v0].AsVector3(), mesh1.Vertices[face.v1].AsVector3(), mesh1.Vertices[face.v2].AsVector3());
					var flattenedMatrix = CoPlanarFaces.GetFlattenedMatrix(cutPlane);
					var flattenedMatrixInverted = flattenedMatrix.Inverted;
					var totalSlice = new Polygons();
					var firstSlice = true;

					for (var sliceMeshIndex = 0; sliceMeshIndex < transformedMeshes.Count; sliceMeshIndex++)
					{
						if (mesh1Index == sliceMeshIndex)
						{
							continue;
						}

						var mesh2 = transformedMeshes[sliceMeshIndex];
						// calculate and add the PWN face from the loops
						var slice = SliceLayer.CreateSlice(mesh2, cutPlane);
						if (firstSlice)
						{
							totalSlice = slice;
							firstSlice = false;
						}
						else
						{
							totalSlice = totalSlice.Union(slice);
						}
					}

					// now we have the total loops that this polygon can intersect from the other meshes
					// make a polygon for this face
					var facePolygon = CoPlanarFaces.GetFacePolygon(mesh1, faceIndex, cutPlane, flattenedMatrix);

					var polygonShape = new Polygons();
					// clip against the slice based on the parameters
					var clipper = new Clipper();
					clipper.AddPath(facePolygon, PolyType.ptSubject, true);
					clipper.AddPaths(totalSlice, PolyType.ptClip, true);
					var expectedFaceNormal = face.normal;

					switch (operation)
					{
						case CsgModes.Union:
							clipper.Execute(ClipType.ctDifference, polygonShape);
							break;

						case CsgModes.Subtract:
							if (mesh1Index == 0)
							{
								clipper.Execute(ClipType.ctDifference, polygonShape);
							}
							else
							{
								expectedFaceNormal *= -1;
								clipper.Execute(ClipType.ctIntersection, polygonShape);
							}

							break;

						case CsgModes.Intersect:
							clipper.Execute(ClipType.ctIntersection, polygonShape);
							break;
					}

					var faceCountPreAdd = resultsMesh.Faces.Count;

					if (polygonShape.Count == 1
						&& polygonShape[0].Count == 3
						&& facePolygon.Contains(polygonShape[0][0])
						&& facePolygon.Contains(polygonShape[0][1])
						&& facePolygon.Contains(polygonShape[0][2]))
					{
						resultsMesh.AddFaceCopy(mesh1, faceIndex);
					}
					else
					{
						// mesh the new polygon and add it to the resultsMesh
						polygonShape.Vertices().TriangulateFaces(null, resultsMesh, 0, flattenedMatrixInverted);
					}

					if (resultsMesh.Faces.Count - faceCountPreAdd > 0)
					{
						// keep track of the adds so we can process the coplanar faces after
						for (int i = faceCountPreAdd; i < resultsMesh.Faces.Count; i++)
						{
							coPlanarFaces.StoreFaceAdd(cutPlane, mesh1Index, faceIndex, i);
							// make sure our added faces are the right direction
							if (resultsMesh.Faces[i].normal.Dot(expectedFaceNormal) < 0)
							{
								resultsMesh.FlipFace(i);
							}
						}
					}
					else // we did not add any faces but we will still keep track of this polygons plan
					{
						coPlanarFaces.StoreFaceAdd(cutPlane, mesh1Index, faceIndex, -1);
					}

					percentCompleted += amountPerOperation;
					progressStatus.Progress0To1 = percentCompleted;
					reporter?.Report(progressStatus);

					if (cancellationToken.IsCancellationRequested)
					{
						return null;
					}
				}
			}

			var faceIndicesToRemove = new HashSet<int>();
			foreach (var plane in coPlanarFaces.Planes)
			{
				var meshIndices = coPlanarFaces.MeshIndicesForPlane(plane);
				if (meshIndices.Count() > 1)
				{
					// check if more than one mesh has this polygons on this plan
					var flattenedMatrix = CoPlanarFaces.GetFlattenedMatrix(plane);

					// depending on the opperation add or remove polygons that are planar
					switch (operation)
					{
						case CsgModes.Union:
							coPlanarFaces.UnionFaces(plane, transformedMeshes, resultsMesh, flattenedMatrix);
                            break;

						case CsgModes.Subtract:
							coPlanarFaces.SubtractFaces(plane, transformedMeshes, resultsMesh, flattenedMatrix, faceIndicesToRemove);
                            break;

						case CsgModes.Intersect:
							coPlanarFaces.IntersectFaces(plane, transformedMeshes, resultsMesh, flattenedMatrix, faceIndicesToRemove);
							break;
					}

				}
			}

			// now rebuild the face list without the remove polygons
			if (faceIndicesToRemove.Count > 0)
			{
				var newFaces = new FaceList();
				for (int i = 0; i < resultsMesh.Faces.Count; i++)
				{
					// if the face is NOT in the remove faces
					if (!faceIndicesToRemove.Contains(i))
					{
						var face = resultsMesh.Faces[i];
						newFaces.Add(face);
					}
				}

				resultsMesh.Faces = newFaces;
			}

			resultsMesh.CleanAndMerge();
			return resultsMesh;
		}

        private static Mesh ExactLegacy(IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items, CsgModes operation, ProcessingModes processingMode, ProcessingResolution inputResolution, ProcessingResolution outputResolution, IProgress<ProgressStatus> reporter, CancellationToken cancellationToken)
		{
			var progressStatus = new ProgressStatus();
			var totalOperations = items.Count() - 1;
			double amountPerOperation = 1.0 / totalOperations;
			double percentCompleted = 0;

			var first = true;
			var resultsMesh = items.First().mesh;
			var firstWorldMatrix = items.First().matrix;

			foreach (var (mesh, matrix) in items)
			{
				if (first)
				{
					first = false;
					continue;
				}

				var itemWorldMatrix = matrix;
				resultsMesh = Do(
					// other mesh
					resultsMesh,
					firstWorldMatrix,
					mesh,
					itemWorldMatrix,
					// operation
					operation,
					processingMode,
					inputResolution,
					outputResolution,
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

		public static Mesh Do(Mesh inMeshA,
			Matrix4X4 matrixA,
			// mesh B
			Mesh inMeshB,
			Matrix4X4 matrixB,
			// operation
			CsgModes operation,
			ProcessingModes processingMode,
			ProcessingResolution inputResolution,
			ProcessingResolution outputResolution,
			// reporting
			IProgress<ProgressStatus> reporter,
			double amountPerOperation,
			double percentCompleted,
			ProgressStatus progressStatus,
			CancellationToken cancellationToken)
		{
			bool externalAssemblyExists = File.Exists(BooleanAssembly);
			if (processingMode == ProcessingModes.Polygons)
			{
				return BooleanProcessing.DoArray(new (Mesh, Matrix4X4)[] { (inMeshA, matrixA), (inMeshB, matrixB) },
					operation,
					processingMode,
					inputResolution,
					outputResolution,
					reporter,
					cancellationToken,
					amountPerOperation,
					percentCompleted);
			}
			else if (processingMode == ProcessingModes.libigl)
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
						// ApplicationController.Instance.LogInfo("Error performing boolean operation: ");
						// ApplicationController.Instance.LogInfo(ex.Message);
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
				var meshA = inMeshA.Copy(CancellationToken.None);
				meshA.Transform(matrixA);

				var meshB = inMeshB.Copy(CancellationToken.None);
				meshB.Transform(matrixB);

				if (meshA.Faces.Count < 4)
				{
					return meshB;
				}
				else if (meshB.Faces.Count < 4)
				{
					return meshA;
				}

				var implicitA = GetImplicitFunction(meshA, processingMode == ProcessingModes.Polygons, (int)inputResolution);
				var implicitB = GetImplicitFunction(meshB, processingMode == ProcessingModes.Polygons, (int)inputResolution);

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

				var marchingCells = 1 << (int)outputResolution;
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


		public static BoundedImplicitFunction3d GetImplicitFunction(Mesh mesh, bool exact, int numCells)
		{
			var meshA3 = mesh.ToDMesh3();

			// Interesting experiment, this produces an extremely accurate surface representation but is quite slow (even though fast) compared to voxel lookups.
			if (exact)
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