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

/*********************************************************************/
/**************************** OBSOLETE! ******************************/
/************************ USE NEWER VERSION **************************/
/*********************************************************************/

using System;
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
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
    [Obsolete("Use SubtractObject3D_2 instead", false)]
    [ShowUpdateButton(SuppressPropertyChangeUpdates = true)]
    public class SubtractObject3D : MeshWrapperObject3D, ISelectableChildContainer
    {
        public SubtractObject3D()
        {
            Name = "Subtract";
        }

        public SelectedChildren SelectedChildren { get; set; } = new SelectedChildren();

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

            base.OnInvalidate(invalidateType);
        }

        public override Task Rebuild()
        {
            this.DebugDepth("Rebuild");
            var rebuildLocks = this.RebuilLockAll();

            // spin up a task to remove holes from the objects in the group
            return ApplicationController.Instance.Tasks.Execute(
                "Subtract".Localize(),
                null,
                (reporter, cancellationTokenSource) =>
                {
                    try
                    {
                        Subtract(cancellationTokenSource.Token, reporter);
                    }
                    catch
                    {
                    }

                    UiThread.RunOnIdle(() =>
                    {
                        rebuildLocks.Dispose();
                        this.DoRebuildComplete();
                        Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
                    });

                    return Task.CompletedTask;
                });
        }

        public void Subtract()
        {
            Subtract(CancellationToken.None, null);
        }

        public void Subtract(CancellationToken cancellationToken, Action<double, string> reporter)
        {
            ResetMeshWrapperMeshes(Object3DPropertyFlags.All, cancellationToken);

            bool ItemInSubtractList(IObject3D item)
            {
                if (SelectedChildren.Contains(item.ID))
                {
                    return true;
                }

                // check if the wrapped item is in the subtract list
                if (item.Children.Count > 0 && SelectedChildren.Contains(item.Children.First().ID))
                {
                    return true;
                }

                return false;
            }

            var removeObjects = this.Children
                .Where(i => ItemInSubtractList(i))
                .SelectMany(h => h.DescendantsAndSelf())
                .Where(c => c.OwnerID == this.ID).ToList();
            var keepObjects = this.Children
                .Where(i => !ItemInSubtractList(i))
                .SelectMany(h => h.DescendantsAndSelf())
                .Where(c => c.OwnerID == this.ID).ToList();

            if (removeObjects.Any()
                && keepObjects.Any())
            {
                var totalOperations = removeObjects.Count * keepObjects.Count;
                double amountPerOperation = 1.0 / totalOperations;
                double ratioCompleted = 0;

                foreach (var remove in removeObjects.Select((r) => (obj3D: r, matrix: r.WorldMatrix())).ToList())
                {
                    foreach (var keep in keepObjects.Select((r) => (obj3D: r, matrix: r.WorldMatrix())).ToList())
                    {
                        reporter?.Invoke(0, "Do CSG".Localize());
                        var result = BooleanProcessing.Do(keep.obj3D.Mesh,
                            keep.matrix,
                            remove.obj3D.Mesh,
                            remove.matrix,
                            CsgModes.Subtract,
                            ProcessingModes.Polygons,
                            ProcessingResolution._64,
                            ProcessingResolution._64,
                            reporter,
                            amountPerOperation,
                            ratioCompleted,
                            cancellationToken);
                        var inverse = keep.matrix.Inverted;
                        result.Transform(inverse);

                        using (keep.obj3D.RebuildLock())
                        {
                            keep.obj3D.Mesh = result;
                        }

                        ratioCompleted += amountPerOperation;
                        reporter?.Invoke(ratioCompleted, null);
                    }

                    remove.obj3D.Visible = false;
                }
            }
        }
    }
}