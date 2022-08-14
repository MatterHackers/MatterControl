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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderOpenGl;

using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class SubtractPathObject3D : OperationSourceContainerObject3D, IEditorDraw, IObject3DControlsProvider
	{
		public SubtractPathObject3D()
		{
			Name = "Subtract";
		}

		[DisplayName("Part(s) to Subtract")]
		public SelectedChildren SelectedChildren { get; set; } = new SelectedChildren();

		public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
		{
			this.DrawPath();
		}

		public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
		{
			return this.GetWorldspaceAabbOfDrawPath();
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.Standard2D);
		}

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

		public override bool CanApply => true;

		public override void Apply(UndoBuffer undoBuffer)
		{
			this.FlattenToPathObject(undoBuffer);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

			return ApplicationController.Instance.Tasks.Execute(
				"Subtract".Localize(),
				null,
				(reporter, cancellationTokenSource) =>
				{
					var progressStatus = new ProgressStatus();
					reporter.Report(progressStatus);

					try
					{
						Subtract(cancellationTokenSource.Token, reporter);
					}
					catch
					{
					}

					// set the mesh to show the path
					var extrudeMesh = this.GetVertexSource().Extrude(Constants.PathPolygonsHeight);
					if(extrudeMesh.Vertices.Count() > 5)
					{
						this.Mesh = extrudeMesh;
					}
					else
					{
						this.Mesh = null;
					}

					UiThread.RunOnIdle(() =>
					{
						rebuildLocks.Dispose();
						this.CancelAllParentBuilding();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					});

					return Task.CompletedTask;
				});
		}

		public void Subtract()
		{
			Subtract(CancellationToken.None, null);
		}

		private void Subtract(CancellationToken cancellationToken, IProgress<ProgressStatus> reporter)
		{
			SourceContainer.Visible = true;
			RemoveAllButSource();

			var parentOfSubtractTargets = SourceContainer.FirstWithMultipleChildrenDescendantsAndSelf();

			if (parentOfSubtractTargets.Children.Count() < 2)
			{
				if (parentOfSubtractTargets.Children.Count() == 1)
				{
					this.Children.Add(SourceContainer.Clone());
					SourceContainer.Visible = false;
				}

				return;
			}

			CleanUpSelectedChildrenNames(this);

			var removeVisibleItems = parentOfSubtractTargets.Children
				.Where((i) => SelectedChildren
				.Contains(i.ID))
				.SelectMany(c => c.VisiblePaths())
				.ToList();

			var keepItems = parentOfSubtractTargets.Children
				.Where((i) => !SelectedChildren
				.Contains(i.ID));

			var keepVisibleItems = keepItems.SelectMany(c => c.VisiblePaths()).ToList();

			if (removeVisibleItems.Any()
				&& keepVisibleItems.Any())
			{
				var totalOperations = removeVisibleItems.Count * keepVisibleItems.Count;
				double amountPerOperation = 1.0 / totalOperations;
				double ratioCompleted = 0;

				var progressStatus = new ProgressStatus
				{
					Status = "Do Subtract"
				};

				bool first = true;
				foreach (var keep in keepVisibleItems)
				{
					var resultsVertexSource = keep.GetVertexSource().Transform(keep.Matrix);

					foreach (var remove in removeVisibleItems)
					{
						resultsVertexSource = resultsVertexSource.MergePaths(remove.GetVertexSource().Transform(remove.Matrix), ClipperLib.ClipType.ctDifference);

						// report our progress
						ratioCompleted += amountPerOperation;
						progressStatus.Progress0To1 = ratioCompleted;
						reporter?.Report(progressStatus);
					}

					if (first)
					{
						this.VertexStorage = new VertexStorage(resultsVertexSource);
						first = false;
					}
					else
					{
						this.GetVertexSource().MergePaths(resultsVertexSource, ClipperLib.ClipType.ctUnion);
					}
				}

				// this.VertexSource = this.VertexSource.Transform(Matrix.Inverted);
				first = true;
				foreach (var child in Children)
				{
					if (first)
					{
						// hide the source item
						child.Visible = false;
						first = false;
					}
					else
					{
						child.Visible = true;
					}
				}
			}
		}

		public static void CleanUpSelectedChildrenNames(OperationSourceContainerObject3D item)
		{
			if (item is ISelectableChildContainer selectableChildContainer)
			{
				var parentOfSubtractTargets = item.FirstWithMultipleChildrenDescendantsAndSelf();

				var allVisibleNames = parentOfSubtractTargets.Children.Select(i => i.ID);
				// remove any names from SelectedChildren that are not a child we can select
				foreach (var name in selectableChildContainer.SelectedChildren.ToArray())
				{
					if (!allVisibleNames.Contains(name))
					{
						selectableChildContainer.SelectedChildren.Remove(name);
					}
				}
			}
		}
	}
}