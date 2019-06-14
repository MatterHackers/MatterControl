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
using System.Runtime.InteropServices;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public static class BooleanProcessing
	{
		private const string BooleanAssembly = "609_Boolean_bin.dll";

		[DllImport(BooleanAssembly, CallingConvention = CallingConvention.Cdecl)]
		public static extern int DeleteDouble(ref IntPtr handle);

		[DllImport(BooleanAssembly, CallingConvention = CallingConvention.Cdecl)]
		public static extern int DeleteInt(ref IntPtr handle);

		[DllImport(BooleanAssembly, CallingConvention = CallingConvention.Cdecl)]
		public static extern void DoBooleanOperation(double[] va, int vaCount, int[] fa, int faCount, double[] vb, int vbCount, int[] fb, int fbCount, int opperation, out IntPtr pVc, out int vcCount, out IntPtr pVf, out int vfCount);

		public static Mesh Do(Mesh inMeshA,
			Matrix4X4 matrixA,
			// mesh B
			Mesh inMeshB,
			Matrix4X4 matrixB,
			// operation
			int operation,
			// reporting
			IProgress<ProgressStatus> reporter,
			double amountPerOperation,
			double percentCompleted,
			ProgressStatus progressStatus,
			CancellationToken cancellationToken)
		{
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
						operation,
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
					ApplicationController.Instance.LogInfo("Error performing boolean operation: ");
					ApplicationController.Instance.LogInfo(ex.Message);
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

					progressStatus.Progress0To1 = percentCompleted + amountPerOperation;
					reporter.Report(progressStatus);
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
				case 0:
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

				case 1:
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

				case 2:
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