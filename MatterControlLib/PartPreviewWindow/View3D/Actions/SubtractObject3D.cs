/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public static class BooleanProcessing
	{
		[DllImport("609_Boolean_bin.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int DeleteDouble(ref IntPtr handle);

		[DllImport("609_Boolean_bin.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int DeleteInt(ref IntPtr handle);

		public static Mesh Do(Mesh inMeshA, Matrix4X4 matrixA, 
			Mesh inMeshB, Matrix4X4 matrixB, 
			int opperation, 
			IProgress<ProgressStatus> reporter, double amountPerOperation, double percentCompleted, ProgressStatus progressStatus, CancellationToken cancellationToken)
		{
			var libiglExe = "libigl_boolean.exe";
			if (File.Exists(libiglExe)
				&& IntPtr.Size == 8) // only try to run the improved booleans if we are 64 bit and it is there
			{
				IntPtr pVc = IntPtr.Zero;
				IntPtr pFc = IntPtr.Zero;
				try
				{
					double[] va;
					int[] fa;
					inMeshA.ToVerticesAndFaces(matrixA, out va, out fa);
					double[] vb;
					int[] fb;
					inMeshB.ToVerticesAndFaces(matrixB, out vb, out fb);

					int vcCount;
					int fcCount;
					DoBooleanOpperation(va, va.Length, fa, fa.Length,
						vb, vb.Length, fb, fb.Length,
						opperation,
						out pVc, out vcCount, out pFc, out fcCount);

					var vcArray = new double[vcCount];
					Marshal.Copy(pVc, vcArray, 0, vcCount);

					var fcArray = new int[fcCount];
					Marshal.Copy(pFc, fcArray, 0, fcCount);

					return new Mesh(vcArray, fcArray);
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

			var meshA = inMeshA.Copy(CancellationToken.None);
			meshA.Transform(matrixA);

			var meshB = inMeshB.Copy(CancellationToken.None);
			meshB.Transform(matrixB);

			switch (opperation)
			{
				case 0:
					return PolygonMesh.Csg.CsgOperations.Union(meshA, meshB, (status, progress0To1) =>
					{
						// Abort if flagged
						cancellationToken.ThrowIfCancellationRequested();

						progressStatus.Status = status;
						progressStatus.Progress0To1 = percentCompleted + (amountPerOperation * progress0To1);
						reporter.Report(progressStatus);
					}, cancellationToken);

				case 1:
					return PolygonMesh.Csg.CsgOperations.Subtract(meshA, meshB, (status, progress0To1) =>
					{
						// Abort if flagged
						cancellationToken.ThrowIfCancellationRequested();

						progressStatus.Status = status;
						progressStatus.Progress0To1 = percentCompleted + (amountPerOperation * progress0To1);
						reporter?.Report(progressStatus);
					}, cancellationToken);

				case 2:
					return PolygonMesh.Csg.CsgOperations.Intersect(meshA, meshB, (status, progress0To1) =>
					{
						// Abort if flagged
						cancellationToken.ThrowIfCancellationRequested();

						progressStatus.Status = status;
						progressStatus.Progress0To1 = percentCompleted + (amountPerOperation * progress0To1);
						reporter.Report(progressStatus);
					}, cancellationToken);
			}

			return null;
		}

		[DllImport("609_Boolean_bin.dll", CallingConvention = CallingConvention.Cdecl)]
		public extern static void DoBooleanOpperation(
			double[] va, int vaCount, int[] fa, int faCount,
			double[] vb, int vbCount, int[] fb, int fbCount,
			int opperation,
			out IntPtr pVc, out int vcCount, out IntPtr pVf, out int vfCount);
	}

	[ShowUpdateButton]
	public class SubtractObject3D : MeshWrapperObject3D
	{
		public SubtractObject3D()
		{
			Name = "Subtract";
		}

		public ChildrenSelector ItemsToSubtract { get; set; } = new ChildrenSelector();

		public static void Subtract(List<IObject3D> keepObjects, List<IObject3D> removeObjects)
		{
			Subtract(keepObjects, removeObjects, CancellationToken.None, null);
		}

		public static void Subtract(List<IObject3D> keepObjects, List<IObject3D> removeObjects, CancellationToken cancellationToken, IProgress<ProgressStatus> reporter)
		{
			if (removeObjects.Any()
				&& keepObjects.Any())
			{
				var totalOperations = removeObjects.Count * keepObjects.Count;
				double amountPerOperation = 1.0 / totalOperations;
				double percentCompleted = 0;

				ProgressStatus progressStatus = new ProgressStatus();
				foreach (var remove in removeObjects.Select((r) => (obj3D: r, matrix: r.WorldMatrix())).ToList())
				{
					foreach (var keep in keepObjects.Select((r) => (obj3D: r, matrix: r.WorldMatrix())).ToList())
					{
						progressStatus.Status = "Copy Remove";
						reporter?.Report(progressStatus);

						progressStatus.Status = "Copy Keep";
						reporter?.Report(progressStatus);

						progressStatus.Status = "Do CSG";
						reporter?.Report(progressStatus);
						var result = BooleanProcessing.Do(keep.obj3D.Mesh, keep.matrix,
							remove.obj3D.Mesh, remove.matrix, 1, reporter, amountPerOperation, percentCompleted, progressStatus, cancellationToken);
						var inverse = keep.matrix.Inverted;
						result.Transform(inverse);

						using (keep.obj3D.RebuildLock())
						{
							keep.obj3D.Mesh = result;
						}

						percentCompleted += amountPerOperation;
						progressStatus.Progress0To1 = percentCompleted;
						reporter?.Report(progressStatus);
					}

					remove.obj3D.Visible = false;
				}
			}
		}

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
			var rebuildLock = RebuildLock();
			ResetMeshWrapperMeshes(Object3DPropertyFlags.All, CancellationToken.None);

			// spin up a task to remove holes from the objects in the group
			ApplicationController.Instance.Tasks.Execute(
				"Subtract".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var progressStatus = new ProgressStatus();
					reporter.Report(progressStatus);

					var removeObjects = this.Children
						.Where((i) => ItemsToSubtract.Contains(i.ID))
						.SelectMany((h) => h.DescendantsAndSelf())
						.Where((c) => c.OwnerID == this.ID).ToList();
					var keepObjects = this.Children
						.Where((i) => !ItemsToSubtract.Contains(i.ID))
						.SelectMany((h) => h.DescendantsAndSelf())
						.Where((c) => c.OwnerID == this.ID).ToList();

					try
					{
						Subtract(keepObjects, removeObjects, cancellationToken, reporter);
					}
					catch
					{
					}

					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						base.Invalidate(new InvalidateArgs(this, InvalidateType.Content));
					});

					return Task.CompletedTask;
				});
		}
	}
}