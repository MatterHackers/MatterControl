/*
Copyright (c) 2023, Lars Brubaker, John Lewin
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
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
    [ShowUpdateButton]
    public class SubtractAndReplaceObject3D_2 : OperationSourceContainerObject3D, ISelectableChildContainer, ICustomEditorDraw, IPropertyGridModifier
    {
        public SubtractAndReplaceObject3D_2()
        {
            Name = "Subtract and Replace";
        }

        public bool DoEditorDraw(bool isSelected)
        {
            return isSelected;
        }

        [HideFromEditor]
        public SelectedChildren ComputedChildren { get; set; } = new SelectedChildren();

        [DisplayName("Part(s) to Subtract and Replace")]
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

        public void AddEditorTransparents(Object3DControlsLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e)
        {
            if (layer.Scene.SelectedItem != null
                && layer.Scene.SelectedItem == this)
            {
                var parentOfSubtractTargets = this.SourceContainer.FirstWithMultipleChildrenDescendantsAndSelf();

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

        public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
        {
            return;
        }

        public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
        {
            return AxisAlignedBoundingBox.Empty();
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
            var rebuildLocks = this.RebuilLockAll();

            // spin up a task to calculate the paint
            return ApplicationController.Instance.Tasks.Execute("Replacing".Localize(),
                null,
                (reporter, cancellationTokenSource) =>
                {
                    this.cancellationToken = cancellationTokenSource as CancellationTokenSource;
                    try
                    {
                        SubtractAndReplace(cancellationTokenSource.Token, reporter);
                        var newComputedChildren = new SelectedChildren();

                        foreach (var id in SelectedChildren)
                        {
                            newComputedChildren.Add(id);
                        }

                        ComputedChildren = newComputedChildren;
                    }
                    catch
                    {
                    }

                    this.cancellationToken = null;
                    UiThread.RunOnIdle(() =>
                    {
                        rebuildLocks.Dispose();
                        this.DoRebuildComplete();
                        Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
                    });

                    return Task.CompletedTask;
                });
        }

        public void SubtractAndReplace()
        {
            SubtractAndReplace(CancellationToken.None, null);
        }

        private void SubtractAndReplace(CancellationToken cancellationToken, Action<double, string> reporter)
        {
            SourceContainer.Visible = true;
            RemoveAllButSource();

            var parentOfPaintTargets = SourceContainer.FirstWithMultipleChildrenDescendantsAndSelf();

            if (parentOfPaintTargets.Children.Count() < 2)
            {
                if (parentOfPaintTargets.Children.Count() == 1)
                {
                    this.Children.Add(SourceContainer.DeepCopy());
                    SourceContainer.Visible = false;
                }

                return;
            }

            SubtractObject3D_2.CleanUpSelectedChildrenIDs(this);

            var paintObjects = parentOfPaintTargets.Children
                .Where((i) => SelectedChildren
                .Contains(i.ID))
                .SelectMany(c => c.VisibleMeshes())
                .ToList();

            var keepItems = parentOfPaintTargets.Children
                .Where((i) => !SelectedChildren
                .Contains(i.ID));

            var keepVisibleItems = keepItems.SelectMany(c => c.VisibleMeshes()).ToList();

            if (paintObjects.Any()
                && keepVisibleItems.Any())
            {
                var totalOperations = paintObjects.Count * keepVisibleItems.Count * 2;
                double amountPerOperation = 1.0 / totalOperations;
                double ratioCompleted = 0;

                foreach (var keep in keepVisibleItems)
                {
                    var keepResultsMesh = keep.Mesh;
                    var keepWorldMatrix = keep.WorldMatrix(SourceContainer);

                    foreach (var paint in paintObjects)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            SourceContainer.Visible = true;
                            RemoveAllButSource();
                            return;
                        }

                        Mesh paintMesh = BooleanProcessing.Do(keepResultsMesh,
                            keepWorldMatrix,
                            // paint data
                            paint.Mesh,
                            paint.WorldMatrix(SourceContainer),
                            // operation type
                            CsgModes.Intersect,
                            Processing,
                            InputResolution,
                            OutputResolution,
                            // reporting data
                            reporter,
                            amountPerOperation,
                            ratioCompleted,
                            cancellationToken);

                        ratioCompleted += amountPerOperation;

                        keepResultsMesh = BooleanProcessing.Do(keepResultsMesh,
                            keepWorldMatrix,
                            // point data
                            paint.Mesh,
                            paint.WorldMatrix(SourceContainer),
                            // operation type
                            CsgModes.Subtract,
                            Processing,
                            InputResolution,
                            OutputResolution,
                            // reporting data
                            reporter,
                            amountPerOperation,
                            ratioCompleted,
                            cancellationToken);

                        // after the first time we get a result the results mesh is in the right coordinate space
                        keepWorldMatrix = Matrix4X4.Identity;

                        // store our intersection (paint) results mesh
                        var paintResultsItem = new Object3D()
                        {
                            Mesh = paintMesh,
                            Visible = false,
                            OwnerID = paint.ID
                        };
                        // copy all the properties but the matrix
                        paintResultsItem.CopyWorldProperties(paint, SourceContainer, Object3DPropertyFlags.All & (~(Object3DPropertyFlags.Matrix | Object3DPropertyFlags.Visible)));
                        // and add it to this
                        this.Children.Add(paintResultsItem);

                        // report our progress
                        ratioCompleted += amountPerOperation;
                        reporter?.Invoke(ratioCompleted, "Do CSG".Localize());
                    }

                    // store our results mesh
                    var keepResultsItem = new Object3D()
                    {
                        Mesh = keepResultsMesh,
                        Visible = false,
                        OwnerID = keep.ID
                    };
                    // copy all the properties but the matrix
                    keepResultsItem.CopyWorldProperties(keep, SourceContainer, Object3DPropertyFlags.All & (~(Object3DPropertyFlags.Matrix | Object3DPropertyFlags.Visible)));
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

        public void UpdateControls(PublicPropertyChange change)
        {
            change.SetRowVisible(nameof(InputResolution), () => Processing != ProcessingModes.Polygons);
            change.SetRowVisible(nameof(OutputResolution), () => Processing != ProcessingModes.Polygons);
            change.SetRowVisible(nameof(MeshAnalysis), () => Processing != ProcessingModes.Polygons);
            change.SetRowVisible(nameof(InputResolution), () => Processing != ProcessingModes.Polygons && MeshAnalysis == IplicitSurfaceMethod.Grid);
        }
    }
}