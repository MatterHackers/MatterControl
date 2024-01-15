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

using System.Collections.Generic;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterControlLib.DesignTools.Operations.Path;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Primitives
{
    public class CustomPathObject3D : Object3D, IEditorDraw, IStaticThumbnail, IPathProvider, IPrimaryOperationsSpecifier
    {
        public static double MinEdgeSize = .001;

        public CustomPathObject3D()
        {
            // make sure the path editor is registered
            PropertyEditor.RegisterEditor(typeof(PathEditorFactory.EditableVertexStorage), new PathEditorFactory());

            Name = "Custom Path".Localize();
            Color = Operations.Object3DExtensions.PrimitiveColors["Cube"];
        }

        public override bool CanApply => false;

        public bool MeshIsSolidObject => false;

        [PathEditorFactory.ShowOrigin]
        public PathEditorFactory.EditableVertexStorage PathForEditing { get; set; } = new PathEditorFactory.EditableVertexStorage();

        public string ThumbnailName => "Custom Path";

        public static async Task<CustomPathObject3D> Create()
        {
            var item = new CustomPathObject3D();
            await item.Rebuild();
            return item;
        }

        public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
        {
            this.DrawPath();
        }

        public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
        {
            return this.GetWorldspaceAabbOfDrawPath();
        }

        public IEnumerable<SceneOperation> GetOperations()
        {
            return PathObject3DAbstract.GetOperations(this.GetType());
        }

        public virtual IVertexSource GetRawPath()
        {
            return PathForEditing;
        }

        public override async void OnInvalidate(InvalidateArgs invalidateArgs)
        {
            if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this)
            {
                await Rebuild();
            }
            else if (Expressions.NeedRebuild(this, invalidateArgs))
            {
                await Rebuild();
            }
            else if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Path))
            {
                await Rebuild();
            }
            else if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children))

            {
                base.OnInvalidate(invalidateArgs);
            }
        }

        public override Task Rebuild()
        {
            this.DebugDepth("Rebuild");

            using (RebuildLock())
            {
                if (PathForEditing.Count == 0)
                {
                    var maxWidthDepth = 20;
                    var bottom = -10;
                    var top = 10;

                    var bottomPoint = new Vector2(maxWidthDepth, bottom * 10);
                    var topPoint = new Vector2(maxWidthDepth, top * 10);
                    var middlePoint = (bottomPoint + topPoint) / 2;
                    middlePoint.X *= 2;

                    var Point1 = new Vector2(maxWidthDepth, bottom);
                    var Point2 = new Vector2(maxWidthDepth, bottom + (top - bottom) * .2);
                    var Point3 = new Vector2(maxWidthDepth * 1.5, bottom + (top - bottom) * .2);
                    var Point4 = new Vector2(maxWidthDepth * 1.5, bottom + (top - bottom) * .5);
                    var Point5 = new Vector2(maxWidthDepth * 1.5, bottom + (top - bottom) * .8);
                    var Point6 = new Vector2(maxWidthDepth, bottom + (top - bottom) * .8);
                    var Point7 = new Vector2(maxWidthDepth, top);

                    var newPath = new VertexStorage();
                    newPath.MoveTo(Point1);
                    newPath.Curve4(Point2, Point3, Point4);
                    newPath.Curve4(Point5, Point6, Point7);
                    newPath.ClosePolygon();

                    PathForEditing.SvgDString = newPath.SvgDString;
                }

                using (new CenterAndHeightMaintainer(this))
                {
                    Mesh = PathForEditing.Extrude(Constants.PathPolygonsHeight);
                }
            }

            this.CancelAllParentBuilding();
            Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
            return Task.CompletedTask;
        }
    }
}