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
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	[ShowUpdateButton]
	public class SubtractAndReplaceObject3D_2 : OperationSourceContainerObject3D, ISelectableChildContainer, IEditorDraw
	{
		public SubtractAndReplaceObject3D_2()
		{
			Name = "Subtract and Replace";
		}

		public SelectedChildren SelectedChildren { get; set; } = new SelectedChildren();

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			if (layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem == this)
			{
				suppressNormalDraw = true;

				var removeObjects = this.SourceContainer.VisibleMeshes()
					.Where((i) => SelectedChildren.Contains(i.Name)).ToList();
				var keepObjects = this.SourceContainer.VisibleMeshes()
					.Where((i) => !SelectedChildren.Contains(i.Name)).ToList();

				foreach (var item in removeObjects)
				{
					transparentMeshes.Add(new Object3DView(item, new Color(item.WorldColor(SourceContainer), 128)));
				}

				foreach (var item in keepObjects)
				{
					var subtractChild = this.Children.Where(i => i.Name == item.Name).FirstOrDefault();
					if (subtractChild != null)
					{
						GLHelper.Render(subtractChild.Mesh,
							subtractChild.Color,
							subtractChild.WorldMatrix(),
							RenderTypes.Outlines,
							subtractChild.WorldMatrix() * layer.World.ModelviewMatrix);
					}
					else
					{
						GLHelper.Render(item.Mesh,
							item.WorldColor(SourceContainer),
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
				Invalidate(InvalidateType.Children);
				return Task.CompletedTask;
			});
		}

		public void SubtractAndReplace()
		{
			SubtractAndReplace(CancellationToken.None, null);
		}

		private void SubtractAndReplace(CancellationToken cancellationToken, IProgress<ProgressStatus> reporter)
		{
			SourceContainer.Visible = true;
			RemoveAllButSource();

			var participants = SourceContainer.VisibleMeshes();
			if (participants.Count() < 2)
			{
				if (participants.Count() == 1)
				{
					var newMesh = new Object3D();
					newMesh.CopyProperties(participants.First(), Object3DPropertyFlags.All);
					newMesh.Mesh = participants.First().Mesh;
					this.Children.Add(newMesh);
					SourceContainer.Visible = false;
				}
				return;
			}

			SubtractObject3D_2.CleanUpSelectedChildrenNames(this);

			var paintObjects = this.SourceContainer.VisibleMeshes()
				.Where((i) => SelectedChildren.Contains(i.Name)).ToList();
			var keepObjects = this.SourceContainer.VisibleMeshes()
				.Where((i) => !SelectedChildren.Contains(i.Name)).ToList();

			if (paintObjects.Any()
				&& keepObjects.Any())
			{
				var totalOperations = paintObjects.Count * keepObjects.Count;
				double amountPerOperation = 1.0 / totalOperations;
				double percentCompleted = 0;

				ProgressStatus progressStatus = new ProgressStatus();
				progressStatus.Status = "Do CSG";
				foreach (var keep in keepObjects)
				{
					var keepResultsMesh = keep.Mesh;
					var keepWorldMatrix = keep.WorldMatrix(SourceContainer);

					foreach (var paint in paintObjects)
					{
						Mesh paintMesh = BooleanProcessing.Do(keepResultsMesh, keepWorldMatrix,
							paint.Mesh, paint.WorldMatrix(SourceContainer),
							2, reporter, amountPerOperation, percentCompleted, progressStatus, cancellationToken);

						keepResultsMesh = BooleanProcessing.Do(keepResultsMesh, keepWorldMatrix,
							paint.Mesh, paint.WorldMatrix(SourceContainer),
							1, reporter, amountPerOperation, percentCompleted, progressStatus, cancellationToken);

						// after the first time we get a result the results mesh is in the right coordinate space
						keepWorldMatrix = Matrix4X4.Identity;

						// store our intersection (paint) results mesh
						var paintResultsItem = new Object3D()
						{
							Mesh = paintMesh,
							Visible = false
						};
						// copy all the properties but the matrix
						paintResultsItem.CopyProperties(paint, Object3DPropertyFlags.All & (~(Object3DPropertyFlags.Matrix | Object3DPropertyFlags.Visible)));
						// and add it to this
						this.Children.Add(paintResultsItem);

						// report our progress
						percentCompleted += amountPerOperation;
						progressStatus.Progress0To1 = percentCompleted;
						reporter?.Report(progressStatus);
					}

					// store our results mesh
					var keepResultsItem = new Object3D()
					{
						Mesh = keepResultsMesh,
						Visible = false
					};
					// copy all the properties but the matrix
					keepResultsItem.CopyProperties(keep, Object3DPropertyFlags.All & (~(Object3DPropertyFlags.Matrix | Object3DPropertyFlags.Visible)));
					// and add it to this
					this.Children.Add(keepResultsItem);
				}

				foreach (var child in Children)
				{
					child.Visible = true;
				}
				SourceContainer.Visible = false;
			}
		}
	}
}