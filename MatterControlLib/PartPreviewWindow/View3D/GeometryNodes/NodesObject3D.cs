/*
Copyright (c) 2023, Lars Brubaker
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

using MatterControlLib.PartPreviewWindow.View3D.GeometryNodes.Nodes;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MatterControlLib.PartPreviewWindow.View3D.GeometryNodes
{
    public class NodesObject3D : OperationSourceContainerObject3D
    {
        public List<BaseNode> Nodes = new List<BaseNode>();

        private CancellationTokenSource cancellationToken;

        public NodesObject3D()
        {
            Name = "Geometry Nodes".Localize();
        }

        public override void WrapSelectedItemAndSelect(InteractiveScene scene)
        {
            base.WrapSelectedItemAndSelect(scene);

            // foreach child add a new node
            foreach (var child in Children)
            {
                Nodes.Add(new InputObject3DNode(child));
            }
        }

        public override Task Rebuild()
        {
            this.DebugDepth("Rebuild");

            var rebuildLocks = this.RebuilLockAll();

            return ApplicationController.Instance.Tasks.Execute(
                "Curve".Localize(),
                null,
                (reporter, cancellationTokenSource) =>
                {
                    cancellationToken = cancellationTokenSource;
                    var sourceAabb = SourceContainer.GetAxisAlignedBoundingBox();

                    // If this is the first build (the only child is the source container), fix the aabb.
                    var firstBuild = Children.Count == 1;
                    var initialAabb = this.GetAxisAlignedBoundingBox();

                    var processedChildren = new List<IObject3D>();

                    foreach (var sourceItem in SourceContainer.VisibleMeshes())
                    {
                        var originalMesh = sourceItem.Mesh;
                        reporter?.Invoke(0, "Copy Mesh".Localize());
                        var processedMesh = originalMesh.Copy(CancellationToken.None);

                        var newChild = new Object3D()
                        {
                            Mesh = processedMesh
                        };

                        newChild.CopyWorldProperties(sourceItem, SourceContainer, Object3DPropertyFlags.All, false);

                        processedChildren.Add(newChild);
                    }

                    RemoveAllButSource();
                    SourceContainer.Visible = false;

                    Children.Modify((list) =>
                    {
                        list.AddRange(processedChildren);
                    });

                    ApplyHoles(reporter, cancellationToken.Token);

                    cancellationToken = null;
                    UiThread.RunOnIdle(() =>
                    {
                        rebuildLocks.Dispose();
                        Invalidate(InvalidateType.DisplayValues);
                        this.CancelAllParentBuilding();
                        Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
                    });

                    return Task.CompletedTask;
                });
        }
    }
}