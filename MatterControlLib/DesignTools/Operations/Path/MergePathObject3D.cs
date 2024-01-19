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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterControlLib.DesignTools.Operations.Path;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    public class MergePathObject3D : OperationSourceContainerObject3D, IEditorDraw, IObject3DControlsProvider, IPrimaryOperationsSpecifier, IPathProvider
    {
        private ClipperLib.ClipType clipType;
        private string operationName;

        public MergePathObject3D(string name, ClipperLib.ClipType clipType)
        {
            this.operationName = name;
            this.clipType = clipType;
            Name = name;
        }

        public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
        {
            this.DrawPath();
        }

        public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
        {
            return this.GetWorldspaceAabbOfDrawPath();
        }

        public override bool CanApply => true;

        public bool MeshIsSolidObject => false;

        public VertexStorage VertexStorage { get; set; }

        public override void Apply(UndoBuffer undoBuffer)
        {
            this.FlattenToPathObject(undoBuffer);
        }

        public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
        {
            object3DControlsLayer.AddControls(ControlTypes.Standard2D);
        }

        public override Task Rebuild()
        {
            this.DebugDepth("Rebuild");

            var rebuildLocks = this.RebuilLockAll();

            return ApplicationController.Instance.Tasks.Execute(
                operationName,
                null,
                (reporter, cancellationTokenSource) =>
                {
                    try
                    {
                        Merge(reporter, cancellationTokenSource.Token);
                    }
                    catch
                    {
                    }

                    // set the mesh to show the path
                    this.Mesh = this.GetRawPath().Extrude(Constants.PathPolygonsHeight);

                    UiThread.RunOnIdle(() =>
                    {
                        rebuildLocks.Dispose();
                        this.DoRebuildComplete();
                        Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
                    });
                    return Task.CompletedTask;
                });
        }

        private void Merge(Action<double, string> reporter, CancellationToken cancellationToken)
        {
            SourceContainer.Visible = true;
            RemoveAllButSource();

            var participants = SourceContainer.VisiblePathProviders();
            var first = participants.First();
            var firstObject3D = first as Object3D;
            if (participants.Count() < 2)
            {
                if (participants.Count() == 1)
                {
                    var newMesh = new Object3D();
                    newMesh.CopyProperties(firstObject3D, Object3DPropertyFlags.All);
                    newMesh.Mesh = firstObject3D.Mesh;
                    this.Children.Add(newMesh);
                    SourceContainer.Visible = false;
                }

                return;
            }

            var resultsVertexSource = first.GetTransformedPath(this);

            var totalOperations = participants.Count() - 1;
            double amountPerOperation = 1.0 / totalOperations;
            double ratioCompleted = 0;

            foreach (var item in participants)
            {
                IVertexSource itemPath = item.GetTransformedPath(this);
                if (item != first
                    && itemPath != null)
                {
                    var itemObject3D = item as Object3D;

                    this.CopyProperties(firstObject3D, Object3DPropertyFlags.Color);

                    resultsVertexSource = resultsVertexSource.MergePaths(itemPath, clipType);

                    ratioCompleted += amountPerOperation;
                    reporter?.Invoke(ratioCompleted, null);
                }
            }

            this.VertexStorage = new VertexStorage(resultsVertexSource);

            SourceContainer.Visible = false;
        }

        public IEnumerable<SceneOperation> GetOperations()
        {
            return PathObject3DAbstract.GetOperations(this.GetType());
        }

        public IVertexSource GetRawPath()
        {
            return VertexStorage;
        }
    }
}
