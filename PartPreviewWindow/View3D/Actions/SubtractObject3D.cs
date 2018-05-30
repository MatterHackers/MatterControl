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
using MatterHackers.PolygonMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class SubtractObject3D : MeshWrapperObject3D
	{
		public SubtractObject3D()
		{
			Name = "Subtract";
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix
				|| invalidateType.InvalidateType == InvalidateType.Mesh)
				&& invalidateType.Source != this
				&& !RebuildSuspended)
			{
				Rebuild(null);
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override void Rebuild(UndoBuffer undoBuffer)
		{
			this.DebugDepth("Rebuild");
			SuspendRebuild();
			ResetMeshWrapperMeshes(Object3DPropertyFlags.All & (~Object3DPropertyFlags.OutputType), CancellationToken.None);

			// spin up a task to remove holes from the objects in the group
			ApplicationController.Instance.Tasks.Execute(
				"Subtract".Localize(),
				(reporter, cancellationToken) =>
				{
					var progressStatus = new ProgressStatus();
					reporter.Report(progressStatus);

					var removeObjects = this.Children
						.Where((i) => i.WorldOutputType(this) == PrintOutputTypes.Hole)
						.SelectMany((h) => h.DescendantsAndSelf())
						.Where((c) => c.OwnerID == this.ID).ToList();
					var keepObjects = this.Children
						.Where((i) => i.WorldOutputType(this) != PrintOutputTypes.Hole)
						.SelectMany((h) => h.DescendantsAndSelf())
						.Where((c) => c.OwnerID == this.ID).ToList();

					Subtract(keepObjects, removeObjects, cancellationToken, reporter);

					ResumeRebuild();

					UiThread.RunOnIdle(() => base.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh)));

					return Task.CompletedTask;
				});

			base.Rebuild(null);
		}

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
				foreach (var remove in removeObjects)
				{
					foreach (var keep in keepObjects)
					{
						progressStatus.Status = "Copy Remove";
						reporter?.Report(progressStatus);
						var transformedRemove = Mesh.Copy(remove.Mesh, cancellationToken);
						transformedRemove.Transform(remove.WorldMatrix());

						progressStatus.Status = "Copy Keep";
						reporter?.Report(progressStatus);
						var transformedKeep = Mesh.Copy(keep.Mesh, cancellationToken);
						transformedKeep.Transform(keep.WorldMatrix());

						progressStatus.Status = "Do CSG";
						reporter?.Report(progressStatus);
						transformedKeep = PolygonMesh.Csg.CsgOperations.Subtract(transformedKeep, transformedRemove, (status, progress0To1) =>
						{
							// Abort if flagged
							cancellationToken.ThrowIfCancellationRequested();

							progressStatus.Status = status;
							progressStatus.Progress0To1 = percentCompleted + amountPerOperation * progress0To1;
							reporter?.Report(progressStatus);
						}, cancellationToken);
						var inverse = keep.WorldMatrix();
						inverse.Invert();
						transformedKeep.Transform(inverse);

						keep.Mesh = transformedKeep;
						// TODO: make this the subtract object when it is available
						keep.Invalidate(new InvalidateArgs(keep, InvalidateType.Content));

						percentCompleted += amountPerOperation;
						progressStatus.Progress0To1 = percentCompleted;
						reporter?.Report(progressStatus);
					}

					remove.Visible = false;
				}
			}
		}
	}
}