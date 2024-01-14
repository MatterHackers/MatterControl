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

using DataConverters3D.Object3D.Nodes;
using MatterControlLib.PartPreviewWindow.View3D.GeometryNodes.Nodes;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MatterControlLib.PartPreviewWindow.View3D.GeometryNodes
{
    [HideMeterialAndColor]
    public class NodesObject3D : Object3D
    {
        private CancellationTokenSource cancellationToken;
        public NodesObject3D()
        {
            Name = "Geometry Nodes".Localize();
        }

        public SafeList<INodeObject> Nodes { get; set; } = new SafeList<INodeObject>();

        public SafeList<NodeConnection> Connections { get; set; } = new SafeList<NodeConnection>();

        [HideFromEditor]
        public Vector2 UnscaledRenderOffset { get; set; } = Vector2.Zero;

        [HideFromEditor]
        public double Scale { get; set; } = 1;

        public override Task Rebuild()
        {
            this.DebugDepth("Rebuild");

            var rebuildLocks = this.RebuilLockAll();

            return ApplicationController.Instance.Tasks.Execute(
                "Building Nodes".Localize(),
                null,
                (reporter, cancellationTokenSource) =>
                {
                    cancellationToken = cancellationTokenSource;

                    // start the rebuild process
                    // find all the OutputMeshNodes
                    List<(OutputMeshNode node, int index)> nodesAndIndices = new List<(OutputMeshNode outputNodes, int index)>();
                    for(int i=0; i<Nodes.Count; i++)
                    {
                        if (Nodes[i] is OutputMeshNode)
                        {
                            nodesAndIndices.Add(((OutputMeshNode)Nodes[i], i));
                        }
                    }

                    foreach(var nodeAndIndex in nodesAndIndices)
                    {
                        // find the connections to this node
                        var connections = Connections.Where(c => c.OutputNodeIndex == nodeAndIndex.index).ToList();

#if DEBUG
                        // there should only be one or none throw if in the debugger and more than 1 connection
                        if(connections.Count > 1)
                        {
                            throw new Exception("There should only be one connection to an output node");
                        }
#endif

                    }

                    // once complete find all the output nodes and add them as children

                    Children.Modify((list) =>
                    {
                        list.Add(new Object3D()
                        {
                            Mesh = PlatonicSolids.CreateCube(20, 20, 20),
                        });
                    });

                    cancellationToken = null;
                    UiThread.RunOnIdle(() =>
                    {
                        rebuildLocks.Dispose();
                        this.CancelAllParentBuilding();
                        Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
                    });

                    return Task.CompletedTask;
                });
        }

        public async Task ConvertChildrenToNodes(InteractiveScene scene)
        {
            using (RebuildLock())
            {
                var selectedItems = scene.GetSelectedItems();

                if (selectedItems.Count > 0)
                {
                    // clear the selected item
                    scene.SelectedItem = null;

                    using (RebuildLock())
                    {
                        // foreach child add a new node
                        foreach (var child in selectedItems)
                        {
                            var clone = child.DeepCopy();
                            Nodes.Add(new InputMeshNode(clone)
                            {
                                WindowPosition = new Vector2(-300, 0)
                            });
                            Nodes.Add(new OutputMeshNode()
                            {
                                WindowPosition = new Vector2(200, 0)
                            });

                            Connections.Add(new NodeConnection()
                            {
                                InputNodeIndex = Nodes.Count - 2,
                                OutputNodeIndex = Nodes.Count - 1,
                                InputIndex = 0,
                                OutputIndex = 0                                
                            });
                        }
                    }

                    scene.UndoBuffer.AddAndDo(
                        new ReplaceCommand(
                            new List<IObject3D>(selectedItems),
                            new List<IObject3D> { this }));

                    await this.Rebuild();

                    Name = "Node - " + selectedItems.First().Name;
                    NameOverriden = false;
                }
            }

            // and select this
            var rootItem = this.Parents().Where(i => scene.Children.Contains(i)).FirstOrDefault();
            if (rootItem != null)
            {
                scene.SelectedItem = rootItem;
            }

            scene.SelectedItem = this;

            this.Invalidate(InvalidateType.Children);
        }
    }
}