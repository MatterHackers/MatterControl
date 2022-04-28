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
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	[ShowUpdateButton]
	public class SubtractObject3D_2 : OperationSourceContainerObject3D, ISelectableChildContainer, ICustomEditorDraw, IPropertyGridModifier, IBuildsOnThread
	{
		public SubtractObject3D_2()
		{
			Name = "Subtract";
			NameOverriden = false;
		}

		[DisplayName("Part(s) to Subtract")]
		public SelectedChildren SelectedChildren { get; set; } = new SelectedChildren();

#if DEBUG
		public ProcessingModes Processing { get; set; } = ProcessingModes.Polygons;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public ProcessingResolution OutputResolution { get; set; } = ProcessingResolution._64;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public IplicitSurfaceMethod MeshAnalysis { get; set; }

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public ProcessingResolution InputResolution { get; set; } = ProcessingResolution._64;
#else
		private ProcessingModes Processing { get; set; } = ProcessingModes.Polygons;
		private ProcessingResolution OutputResolution { get; set; } = ProcessingResolution._64;
		private IplicitSurfaceMethod MeshAnalysis { get; set; }
		private ProcessingResolution InputResolution { get; set; } = ProcessingResolution._64;
#endif

		public bool RemoveSubtractObjects { get; set; } = true;

		public bool DoEditorDraw(bool isSelected)
        {
			return isSelected;
        }

		public void AddEditorTransparents(Object3DControlsLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e)
		{
			if (layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem == this)
			{
				var parentOfSubtractTargets = this.SourceContainer.DescendantsAndSelfMultipleChildrenFirstOrSelf();

				var removeObjects = parentOfSubtractTargets.Children
					.Where(i => SelectedChildren.Contains(i.ID))
					.SelectMany(c => c.VisibleMeshes())
					.ToList();

				foreach (var item in removeObjects)
				{
					var color = item.WorldColor(checkOutputType: true);
					transparentMeshes.Add(new Object3DView(item, color.WithAlpha(color.Alpha0To1 * .2)));
				}

			}
		}

        public override async void WrapSelectedItemAndSelect(InteractiveScene scene)
        {
			// this will ask the subtract to do a rebuild
            base.WrapSelectedItemAndSelect(scene);
		
			if (SelectedChildren.Count == 0)
			{
				SelectedChildren.Add(SourceContainer.DescendantsAndSelfMultipleChildrenFirstOrSelf().Children.Last().ID);
			}
		}

		public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
		{
			return;
		}

		public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
		{
			return AxisAlignedBoundingBox.Empty();
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (SheetObject3D.NeedsRebuild(this, invalidateArgs))
			{
				await Rebuild();
			}
			else if(invalidateArgs.InvalidateType.HasFlag(InvalidateType.Name)
				&& !NameOverriden)
			{
				Name = NameFromChildren();
				NameOverriden = false;
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		private CancellationTokenSource cancellationToken;

		public bool IsBuilding => this.cancellationToken != null;

		public void CancelBuild()
		{
			var threadSafe = this.cancellationToken;
			if (threadSafe != null)
			{
				threadSafe.Cancel();
			}
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
					this.cancellationToken = cancellationTokenSource;
					var progressStatus = new ProgressStatus();
					reporter.Report(progressStatus);

					try
					{
						Subtract(cancellationTokenSource.Token, reporter);
					}
					catch
					{
					}

					if (!NameOverriden)
                    {
						Name = NameFromChildren();
						NameOverriden = false;
					}

					this.cancellationToken = null;
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

		private (IEnumerable<IObject3D>, IEnumerable<IObject3D>) GetSubtractItems()
        {
			var parentOfSubtractTargets = SourceContainer.DescendantsAndSelfMultipleChildrenFirstOrSelf();

			if (parentOfSubtractTargets.Children.Count() < 2)
			{
				if (parentOfSubtractTargets.Children.Count() == 1)
				{
					this.Children.Add(SourceContainer.Clone());
					SourceContainer.Visible = false;
				}

				return (null, null);
			}

			var removeItems = parentOfSubtractTargets.Children
				.Where((i) => SelectedChildren
				.Contains(i.ID))
				.SelectMany(c => c.VisibleMeshes());

			var keepItems = parentOfSubtractTargets.Children
				.Where((i) => !SelectedChildren
				.Contains(i.ID))
				.SelectMany(c => c.VisibleMeshes());

			return (keepItems, removeItems);
		}

		private void Subtract(CancellationToken cancellationToken, IProgress<ProgressStatus> reporter)
		{
			SourceContainer.Visible = true;
			RemoveAllButSource();

			CleanUpSelectedChildrenIDs(this);

			var (keepItems, removeItems) = GetSubtractItems();
			var removeItemsCount = removeItems == null ? 0 : removeItems.Count();
			var keepItemsCount = keepItems == null ? 0 : keepItems.Count();

			if (removeItems?.Any() == true
				&& keepItems?.Any() == true)
			{
				foreach (var keep in keepItems)
				{
#if false
					var items = removeItems.Select(i => (i.Mesh, i.WorldMatrix(SourceContainer))).ToList();
					items.Insert(0, (keep.Mesh, keep.Matrix));
					var resultsMesh = BooleanProcessing.DoArray(items,
						CsgModes.Subtract,
						Processing,
						InputResolution,
						OutputResolution,
						reporter,
						cancellationToken);
#else
					var totalOperations = removeItemsCount * keepItemsCount;
					double amountPerOperation = 1.0 / totalOperations;
					double ratioCompleted = 0;

					var progressStatus = new ProgressStatus
					{
						Status = "Do CSG"
					};

					var resultsMesh = keep.Mesh;
					var keepWorldMatrix = keep.WorldMatrix(SourceContainer);

					foreach (var remove in removeItems)
					{
						resultsMesh = BooleanProcessing.Do(resultsMesh,
							keepWorldMatrix,
							// other mesh
							remove.Mesh,
							remove.WorldMatrix(SourceContainer),
							// operation type
							CsgModes.Subtract,
							Processing,
							InputResolution,
							OutputResolution,
							// reporting
							reporter,
							amountPerOperation,
							ratioCompleted,
							progressStatus,
							cancellationToken);

						// after the first time we get a result the results mesh is in the right coordinate space
						keepWorldMatrix = Matrix4X4.Identity;

						// report our progress
						ratioCompleted += amountPerOperation;
						progressStatus.Progress0To1 = ratioCompleted;
						reporter?.Report(progressStatus);
					}

#endif
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

					if (!RemoveSubtractObjects)
					{
						this.Children.Modify((list) =>
						{
							foreach (var item in removeItems)
							{
								var newObject = new Object3D()
								{
									Mesh = item.Mesh
								};

								newObject.CopyWorldProperties(item, SourceContainer, Object3DPropertyFlags.All & (~Object3DPropertyFlags.Visible));
								list.Add(newObject);
							}
						});
					}
				}

				bool first = true;
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

		public static void CleanUpSelectedChildrenIDs(OperationSourceContainerObject3D item)
		{
			if (item is ISelectableChildContainer selectableChildContainer)
			{
				var parentOfSubtractTargets = item.SourceContainer.DescendantsAndSelfMultipleChildrenFirstOrSelf();

				var allVisibleIDs = parentOfSubtractTargets.Children.Select(i => i.ID);
				// remove any names from SelectedChildren that are not a child we can select
				foreach (var id in selectableChildContainer.SelectedChildren.ToArray())
				{
					if (!allVisibleIDs.Contains(id))
					{
						selectableChildContainer.SelectedChildren.Remove(id);
					}
				}
			}
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(InputResolution), () => Processing != ProcessingModes.Polygons);
			change.SetRowVisible(nameof(OutputResolution), () => Processing != ProcessingModes.Polygons);
			change.SetRowVisible(nameof(MeshAnalysis), () => Processing != ProcessingModes.Polygons);
			change.SetRowVisible(nameof(InputResolution), () => Processing != ProcessingModes.Polygons && MeshAnalysis == IplicitSurfaceMethod.Grid);
		}

        public override string NameFromChildren()
        {
			var (keepItems, removeItems) = GetSubtractItems();
			return CalculateName(keepItems, ", ", " - ", removeItems, ", ");
		}
	}
}