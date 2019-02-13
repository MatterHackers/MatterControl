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


/*********************************************************************/
/**************************** OBSOLETE! ******************************/
/************************ USE NEWER VERSION **************************/
/*********************************************************************/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	[Obsolete("Use SubtractAndReplaceObject3D_2 instead", false)]
	[ShowUpdateButton]
	public class SubtractAndReplaceObject3D : MeshWrapperObject3D, ISelectableChildContainer
	{
		public SubtractAndReplaceObject3D()
		{
			Name = "Subtract and Replace";
		}

		public SelectedChildren ItemsToSubtract { get; set; } = new SelectedChildren();

		public SelectedChildren SelectedChildren => ItemsToSubtract;

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

		public void SubtractAndReplace()
		{
			SubtractAndReplace(CancellationToken.None, null);
		}

		public void SubtractAndReplace(CancellationToken cancellationToken, IProgress<ProgressStatus> reporter)
		{
			ResetMeshWrapperMeshes(Object3DPropertyFlags.All, cancellationToken);

			var paintObjects = this.Children
				.Where((i) => ItemsToSubtract.Contains(i.ID))
				.SelectMany((h) => h.DescendantsAndSelf())
				.Where((c) => c.OwnerID == this.ID).ToList();
			var keepObjects = this.Children
				.Where((i) => !ItemsToSubtract.Contains(i.ID))
				.SelectMany((h) => h.DescendantsAndSelf())
				.Where((c) => c.OwnerID == this.ID).ToList();

			if (paintObjects.Any()
				&& keepObjects.Any())
			{
				var totalOperations = paintObjects.Count * keepObjects.Count;
				double amountPerOperation = 1.0 / totalOperations;
				double percentCompleted = 0;

				foreach (var paint in paintObjects.Select((r) => (obj3D: r, matrix: r.WorldMatrix())).ToList())
				{
					var transformedPaint = paint.obj3D.Mesh.Copy(cancellationToken);
					transformedPaint.Transform(paint.matrix);
					var inverseRemove = paint.matrix.Inverted;
					Mesh paintMesh = null;

					var progressStatus = new ProgressStatus();
					foreach (var keep in keepObjects.Select((r) => (obj3D: r, matrix: r.WorldMatrix())).ToList())
					{
						var transformedKeep = keep.obj3D.Mesh.Copy(cancellationToken);
						transformedKeep.Transform(keep.matrix);

						// remove the paint from the original
						var subtract = BooleanProcessing.Do(keep.obj3D.Mesh, keep.matrix,
							paint.obj3D.Mesh, paint.matrix, 1, reporter, amountPerOperation, percentCompleted, progressStatus, cancellationToken);
						var intersect = BooleanProcessing.Do(keep.obj3D.Mesh, keep.matrix,
							paint.obj3D.Mesh, paint.matrix, 2, reporter, amountPerOperation, percentCompleted, progressStatus, cancellationToken);

						var inverseKeep = keep.matrix.Inverted;
						subtract.Transform(inverseKeep);
						using (keep.obj3D.RebuildLock())
						{
							keep.obj3D.Mesh = subtract;
						}

						// keep all the intersections together
						if (paintMesh == null)
						{
							paintMesh = intersect;
						}
						else // union into the current paint
						{
							paintMesh = BooleanProcessing.Do(paintMesh, Matrix4X4.Identity,
								intersect, Matrix4X4.Identity, 0, reporter, amountPerOperation, percentCompleted, progressStatus, cancellationToken);
						}

						if (cancellationToken.IsCancellationRequested)
						{
							break;
						}
					}

					// move the paint mesh back to its original coordinates
					paintMesh.Transform(inverseRemove);

					using (paint.obj3D.RebuildLock())
					{
						paint.obj3D.Mesh = paintMesh;
					}

					paint.obj3D.Color = paint.obj3D.WorldColor().WithContrast(keepObjects.First().WorldColor(), 2).ToColor();
				}
			}
		}

		public override Task Rebuild()
		{
			var rebuildLocks = this.RebuilLockAll();

			// spin up a task to calculate the paint
			return ApplicationController.Instance.Tasks.Execute("Replacing".Localize(), null, (reporter, cancellationToken) =>
			{
				try
				{
					SubtractAndReplace(cancellationToken, reporter);
				}
				catch
				{
				}

				rebuildLocks.Dispose();
				Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
				return Task.CompletedTask;
			});
		}
	}
}