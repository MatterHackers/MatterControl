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
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
    [ShowUpdateButton]
    public class IntersectionObject3D_2 : OperationSourceContainerObject3D, IPropertyGridModifier
    {
        public IntersectionObject3D_2()
        {
            Name = "Intersection";
        }

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
                "Intersection".Localize(),
                null,
                (reporter, cancellationTokenSource) =>
                {
                    this.cancellationToken = cancellationTokenSource as CancellationTokenSource;

                    try
                    {
                        Intersect(cancellationTokenSource.Token, reporter);
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
                        this.DoRebuildComplete();
                        Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
                    });

                    return Task.CompletedTask;
                });
        }

        public override string NameFromChildren()
        {
            return CalculateName(SourceContainer.Children, " & ");
        }

        public void Intersect()
        {
            Intersect(CancellationToken.None, null);
        }

        private void Intersect(CancellationToken cancellationToken, Action<double, string> reporter)
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

            var items = participants.Select(i => (i.Mesh, i.WorldMatrix(SourceContainer)));
#if false
			var resultsMesh = BooleanProcessing.DoArray(items,
				CsgModes.Intersect,
				Processing,
				InputResolution,
				OutputResolution,
				reporter,
				cancellationToken);
#else
            var totalOperations = items.Count() - 1;
            double amountPerOperation = 1.0 / totalOperations;
            double ratioCompleted = 0;

            var resultsMesh = items.First().Item1;
            var keepWorldMatrix = items.First().Item2;

            bool first = true;
            foreach (var next in items)
            {
                if (first)
                {
                    first = false;
                    continue;
                }

                resultsMesh = BooleanProcessing.Do(resultsMesh,
                    keepWorldMatrix,
                    // other mesh
                    next.Item1,
                    next.Item2,
                    // operation type
                    CsgModes.Intersect,
                    Processing,
                    InputResolution,
                    OutputResolution,
                    // reporting
                    reporter,
                    amountPerOperation,
                    ratioCompleted,
                    cancellationToken);

                // after the first time we get a result the results mesh is in the right coordinate space
                keepWorldMatrix = Matrix4X4.Identity;

                // report our progress
                ratioCompleted += amountPerOperation;
                reporter?.Invoke(ratioCompleted, null);
            }
#endif

            if (resultsMesh != null)
            {
                var resultsItem = new Object3D()
                {
                    Mesh = resultsMesh
                };
                resultsItem.CopyProperties(participants.First(), Object3DPropertyFlags.All & (~Object3DPropertyFlags.Matrix));
                this.Children.Add(resultsItem);
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
