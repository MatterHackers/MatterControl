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

using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh.Csg;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
    [Obsolete("Use IntersectionObject3D_2 instead", false)]
    public class IntersectionObject3D : MeshWrapperObject3D
    {
        public IntersectionObject3D()
        {
            Name = "Intersection";
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

            return ApplicationController.Instance.Tasks.Execute("Intersection".Localize(), null, (reporter, cancellationTokenSource) =>
            {
                reporter?.Invoke(0, null);

                try
                {
                    Intersect(cancellationTokenSource.Token, reporter);
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
                return base.Rebuild();
            });
        }

        private void Intersect(CancellationToken cancellationToken, Action<double, string> reporter)
        {
            ResetMeshWrapperMeshes(Object3DPropertyFlags.All, cancellationToken);

            var participants = this.DescendantsAndSelf().Where((obj) => obj.OwnerID == this.ID);

            if (participants.Count() > 1)
            {
                var first = participants.First();

                var totalOperations = participants.Count() - 1;
                double amountPerOperation = 1.0 / totalOperations;
                double ratioCompleted = 0;

                foreach (var remove in participants)
                {
                    if (remove != first)
                    {
                        var result = BooleanProcessing.Do(remove.Mesh,
                            remove.WorldMatrix(),
                            first.Mesh,
                            first.WorldMatrix(),
                            CsgModes.Intersect,
                            ProcessingModes.Polygons,
                            ProcessingResolution._64,
                            ProcessingResolution._64,
                            reporter,
                            amountPerOperation,
                            ratioCompleted,
                            cancellationToken);

                        var inverse = first.WorldMatrix();
                        inverse.Invert();
                        result.Transform(inverse);
                        using (first.RebuildLock())
                        {
                            first.Mesh = result;
                        }
                        remove.Visible = false;

                        ratioCompleted += amountPerOperation;
                        reporter?.Invoke(ratioCompleted, null);
                    }
                }
            }
        }
    }
}