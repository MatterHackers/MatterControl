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
using System.Threading;
using System.Threading.Tasks;
using MatterControlLib.DesignTools.Operations.Path;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    public class LinearExtrudeObject3D : PathObject3DAbstract, IPrimaryOperationsSpecifier, IPropertyGridModifier
    {
        [Description("The height of the extrusion")]
        [Slider(.1, 50, Easing.EaseType.Quadratic, useSnappingGrid: true)]
        [MaxDecimalPlaces(2)]
        public DoubleOrExpression Height { get; set; } = 5;

        [Description("Bevel the top of the extrusion")]
        public bool BevelTop { get; set; } = false;

        [EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
        public ExpandStyles Style { get; set; } = ExpandStyles.Sharp;

        [Slider(0, 20, Easing.EaseType.Quadratic, snapDistance: .1)]
        public DoubleOrExpression Radius { get; set; } = 3;

        [Slider(1, 20, Easing.EaseType.Quadratic, snapDistance: 1)]
        public IntOrExpression Segments { get; set; } = 9;

        public override bool CanApply => true;

        public override bool MeshIsSolidObject => true;

        public override void Apply(UndoBuffer undoBuffer)
        {
            if (Mesh == null)
            {
                Cancel(undoBuffer);
            }
            else
            {
                // only keep the mesh and get rid of everything else
                using (RebuildLock())
                {
                    var meshOnlyItem = new Object3D()
                    {
                        Mesh = this.Mesh.Copy(CancellationToken.None)
                    };

                    meshOnlyItem.CopyProperties(this, Object3DPropertyFlags.All);

                    // and replace us with the children
                    undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, new[] { meshOnlyItem }));
                }

                Invalidate(InvalidateType.Children);
            }
        }

        public LinearExtrudeObject3D()
        {
            Name = "Linear Extrude".Localize();
        }

        public override async void OnInvalidate(InvalidateArgs invalidateArgs)
        {
            if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Path)
                    || invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children))
                && invalidateArgs.Source != this
                && !RebuildLocked)
            {
                await Rebuild();
            }
            else if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties)
                && invalidateArgs.Source == this)
            {
                await Rebuild();
            }
            else if (Expressions.NeedRebuild(this, invalidateArgs))
            {
                await Rebuild();
            }
            else
            {
                base.OnInvalidate(invalidateArgs);
            }
        }

        private (double x, double y) GetOffset(double radius, double xRatio, double yRatio)
        {
            return (radius * Math.Cos(xRatio * MathHelper.Tau / 4), radius * Math.Sin(yRatio * MathHelper.Tau / 4));
        }

        public override Task Rebuild()
        {
            this.DebugDepth("Rebuild");
            var rebuildLock = RebuildLock();

            bool valuesChanged = false;

            var height = Height.Value(this);
            var segments = Segments.ClampIfNotCalculated(this, 1, 32, ref valuesChanged);
            var aabb = this.GetAxisAlignedBoundingBox();
            var radius = Radius.ClampIfNotCalculated(this, 0, Math.Min(Math.Min(aabb.XSize, aabb.YSize) / 2, aabb.ZSize), ref valuesChanged);
            var bevelStart = height - radius;

            // now create a long running task to do the extrusion
            return ApplicationController.Instance.Tasks.Execute(
                "Linear Extrude".Localize(),
                null,
                (reporter, cancellationToken) =>
                {
                    var childPaths = this.CombinedVisibleChildrenPaths();
                    List<(double height, double inset)> bevel = null;
                    if (BevelTop)
                    {
                        bevel = new List<(double, double)>();
                        for (int i = 0; i < segments; i++)
                        {
                            (double x, double y) = GetOffset(radius, (i + 1) / (double)segments, i / (double)segments);
                            bevel.Add((bevelStart + y, -radius + x));
                        }
                    }

                    if (childPaths != null)
                    {
                        //childPaths = childPaths.Union(childPaths);
                        Mesh = VertexSourceToMesh.Extrude(childPaths, height, bevel, InflatePathObject3D.GetJoinType(Style));
                        if (Mesh.Vertices.Count == 0)
                        {
                            Mesh = null;
                        }
                    }
                    else
                    {
                        Mesh = null;
                    }

                    UiThread.RunOnIdle(() =>
                    {
                        rebuildLock.Dispose();
                        Invalidate(InvalidateType.DisplayValues);
                        this.CancelAllParentBuilding();
                        Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
                    });

                    return Task.CompletedTask;
                });
        }

        public void UpdateControls(PublicPropertyChange change)
        {
            change.SetRowVisible(nameof(Radius), () => BevelTop);
            change.SetRowVisible(nameof(Segments), () => BevelTop);
            change.SetRowVisible(nameof(Style), () => BevelTop);
        }

        public IEnumerable<SceneOperation> GetOperations()
        {
            yield return SceneOperations.ById("AddBase");
        }
    }
}