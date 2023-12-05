/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using ClipperLib;
using MatterControlLib.DesignTools.Operations.Path;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    public class SelectPathsObject3D : PathObject3DAbstract, IEditorDraw, IObject3DControlsProvider
    {
        public SelectPathsObject3D()
        {
            Name = "Select Paths".Localize();
        }

        public override bool MeshIsSolidObject => false;

        public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
        {
            object3DControlsLayer.AddControls(ControlTypes.Standard2D);
        }

        [ReadOnly(true)]
        [DisplayName("")]
        public string IncludeDocs { get; set; } = "You can use the following variables in the Include Function:\n\n[length] - The length of the path loop\n[index] - The loop index in the body\n[depth] - The count from the outside";

        public DoubleOrExpression IncludeFunction { get; set; } = 1;

        public override async void OnInvalidate(InvalidateArgs invalidateArgs)
        {
            if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
                || invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
                || invalidateArgs.InvalidateType.HasFlag(InvalidateType.Path))
                && invalidateArgs.Source != this
                && !RebuildLocked)
            {
                await Rebuild();
            }
            else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
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

        public override Task Rebuild()
        {
            this.DebugDepth("Rebuild");
            using (RebuildLock())
            {
                GetOutlinePath();
                // set the mesh to show the path
                this.Mesh = VertexStorage.Extrude(Constants.PathPolygonsHeight);
            }

            this.CancelAllParentBuilding();
            Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
            return Task.CompletedTask;
        }

        private void GetOutlinePath()
        {
            var path = this.CombinedVisibleChildrenPaths();
            if (path == null)
            {
                // clear our existing data
                VertexStorage = new VertexStorage();
                return;
            }

            var polygons = path.CreatePolygons();
            var separated = polygons.SeparateIntoOutlinesAndContainedHoles();

            var outlines = new List<List<IntPoint>>();

            foreach(var outlineIndHoles in separated)
            {
                if (IncludeFunction.Value(this) != 0)
                {
                    outlines.Add(outlineIndHoles[0]);
                }
            }

            VertexStorage = outlines.CreateVertexStorage();
        }
    }
}