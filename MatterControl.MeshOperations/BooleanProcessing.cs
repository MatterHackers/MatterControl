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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using g3;
using MatterHackers.Agg;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public enum CsgModes
	{
		Union,
		Subtract,
		Intersect
	}

	public static class BooleanProcessing
	{
		private const string BooleanAssembly = "609_Boolean_bin.dll";

		[DllImport(BooleanAssembly, CallingConvention = CallingConvention.Cdecl)]
		public static extern int DeleteDouble(ref IntPtr handle);

		[DllImport(BooleanAssembly, CallingConvention = CallingConvention.Cdecl)]
		public static extern int DeleteInt(ref IntPtr handle);

		[DllImport(BooleanAssembly, CallingConvention = CallingConvention.Cdecl)]
		public static extern void DoBooleanOperation(double[] va, int vaCount, int[] fa, int faCount, double[] vb, int vbCount, int[] fb, int fbCount, int operation, out IntPtr pVc, out int vcCount, out IntPtr pVf, out int vfCount);

		public static Mesh DoArray(System.Collections.Generic.IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			IProgress<ProgressStatus> reporter,
			CancellationToken cancellationToken)
		{
			var progressStatus = new ProgressStatus();
			var totalOperations = items.Count() - 1;
			double amountPerOperation = 1.0 / totalOperations;
			double percentCompleted = 0;

			var first = items.First();
			var resultsMesh = first.mesh;
			var firstWorldMatrix = first.matrix;

			foreach (var item in items)
			{
				if (item != first)
				{
					var itemWorldMatrix = item.matrix;
					resultsMesh = BooleanProcessing.Do(item.mesh,
						itemWorldMatrix,
						// other mesh
						resultsMesh,
						firstWorldMatrix,
						// operation
						operation,
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
			// reporting
			IProgress<ProgressStatus> reporter,
			double amountPerOperation,
			double percentCompleted,
			ProgressStatus progressStatus,
			CancellationToken cancellationToken)
		{
			// process using marching cubes
			{
				var meshA2 = inMeshA.Copy(CancellationToken.None);
				meshA2.Transform(matrixA);

				var meshB2 = inMeshB.Copy(CancellationToken.None);
				meshB2.Transform(matrixB);

				// Convert to DMesh3
				var meshA3 = meshA2.ToDMesh3();
				var meshB3 = meshB2.ToDMesh3();

				var xxx = new MeshBoolean();
				xxx.Target = meshA3;
				xxx.Tool = meshB3;

				return xxx.Result.ToMesh();
			}

			bool externalAssemblyExists = File.Exists(BooleanAssembly);
			if (externalAssemblyExists
				&& IntPtr.Size == 8) // only try to run the improved booleans if we are 64 bit and it is there
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
			}

			var meshA = inMeshA.Copy(CancellationToken.None);
			meshA.Transform(matrixA);

			var meshB = inMeshB.Copy(CancellationToken.None);
			meshB.Transform(matrixB);

			switch (operation)
			{
				case CsgModes.Union:
					return PolygonMesh.Csg.CsgOperations.Union(meshA,
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
					return PolygonMesh.Csg.CsgOperations.Subtract(meshA,
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

				case CsgModes.Intersect:
					return PolygonMesh.Csg.CsgOperations.Intersect(meshA,
						meshB,
						(status, progress0To1) =>
						{
							// Abort if flagged
							cancellationToken.ThrowIfCancellationRequested();

							progressStatus.Status = status;
							progressStatus.Progress0To1 = percentCompleted + (amountPerOperation * progress0To1);
							reporter.Report(progressStatus);
						},
						cancellationToken);
			}

			return null;
		}
	}
}