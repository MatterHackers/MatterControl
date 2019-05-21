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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	[ShowUpdateButton]
	public class SubtractObject3D_2 : OperationSourceContainerObject3D, ISelectableChildContainer, IEditorDraw
	{
		public SubtractObject3D_2()
		{
			Name = "Subtract";
		}

		public SelectedChildren SelectedChildren { get; set; } = new SelectedChildren();

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			if (layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem == this)
			{
				suppressNormalDraw = true;

				var parentOfSubtractTargets = this.SourceContainer.DescendantsAndSelfMultipleChildrenFirstOrSelf();

				var removeObjects = parentOfSubtractTargets.Children
					.Where(i => SelectedChildren.Contains(i.ID))
					.SelectMany(c => c.VisibleMeshes())
					.ToList();

				foreach (var item in removeObjects)
				{
					transparentMeshes.Add(new Object3DView(item, new Color(item.WorldColor(this.SourceContainer), 80)));
				}

				var keepItems = parentOfSubtractTargets.Children
					.Where(i => !SelectedChildren.Contains(i.ID))
					.ToList();

				foreach (var keepItem in keepItems)
				{
					var drawItem = keepItem;

					var keepItemResult = this.Children.Where(i => i.OwnerID == keepItem.ID).FirstOrDefault();
					drawItem = keepItemResult != null ? keepItemResult : drawItem;

					foreach (var item in drawItem.VisibleMeshes())
					{
						GLHelper.Render(item.Mesh,
							item.WorldColor(),
							item.WorldMatrix(),
							RenderTypes.Outlines,
							item.WorldMatrix() * layer.World.ModelviewMatrix);
					}
				}
			}
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

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

			return ApplicationController.Instance.Tasks.Execute(
				"Subtract".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var progressStatus = new ProgressStatus();
					reporter.Report(progressStatus);

					try
					{
						Subtract(cancellationToken, reporter);
					}
					catch
					{
					}

					rebuildLocks.Dispose();
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
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

			var parentOfSubtractTargets = SourceContainer.DescendantsAndSelfMultipleChildrenFirstOrSelf();

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
				.SelectMany(c => c.VisibleMeshes())
				.ToList();

			var keepItems = parentOfSubtractTargets.Children
				.Where((i) => !SelectedChildren
				.Contains(i.ID));

			var keepVisibleItems = keepItems.SelectMany(c => c.VisibleMeshes()).ToList();

			if (removeVisibleItems.Any()
				&& keepVisibleItems.Any())
			{
				var totalOperations = removeVisibleItems.Count * keepVisibleItems.Count;
				double amountPerOperation = 1.0 / totalOperations;
				double percentCompleted = 0;

				var progressStatus = new ProgressStatus
				{
					Status = "Do CSG"
				};
				foreach (var keep in keepVisibleItems)
				{
					var resultsMesh = keep.Mesh;
					var keepWorldMatrix = keep.WorldMatrix(SourceContainer);

					foreach (var remove in removeVisibleItems)
					{
						resultsMesh = BooleanProcessing.Do(resultsMesh,
							keepWorldMatrix,
							// other mesh
							remove.Mesh,
							remove.WorldMatrix(SourceContainer),
							// operation type
							1,
							// reporting
							reporter,
							amountPerOperation,
							percentCompleted,
							progressStatus,
							cancellationToken);

						// after the first time we get a result the results mesh is in the right coordinate space
						keepWorldMatrix = Matrix4X4.Identity;

						// report our progress
						percentCompleted += amountPerOperation;
						progressStatus.Progress0To1 = percentCompleted;
						reporter?.Report(progressStatus);
					}

					// store our results mesh
					var resultsItem = new Object3D()
					{
						Mesh = resultsMesh,
						Visible = false,
						OwnerID = keep.ID
					};

					// copy all the properties but the matrix
					resultsItem.CopyWorldProperties(keep, SourceContainer, Object3DPropertyFlags.All & (~(Object3DPropertyFlags.Matrix | Object3DPropertyFlags.Visible)));
					// and add it to this
					this.Children.Add(resultsItem);
				}

				bool first = true;
				foreach (var child in Children)
				{
					if (first)
					{
						// hid the source item
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
				var parentOfSubtractTargets = item.DescendantsAndSelfMultipleChildrenFirstOrSelf();

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