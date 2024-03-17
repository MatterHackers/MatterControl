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

using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.DesignTools.Primitives;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.PartPreviewWindow;
using ClipperLib;
using System.Collections.Generic;
using MatterHackers.DataConverters2D;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.MatterControl.DesignTools
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class FindSliceObject3D : OperationSourceContainerObject3D, IEditorDraw, IPropertyGridModifier, IPathProvider, IPrimaryOperationsSpecifier
	{
		public FindSliceObject3D()
		{
			Name = "Find Slice".Localize();
		}

		public DoubleOrExpression SliceHeight { get; set; } = 10;
        public VertexStorage VertexStorage { get; set; }

        public enum FillTypes
		{
			Even_Odd, 
			Non_Zero, 
			Positive, 
			Negative
		};

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
        public FillTypes FillType { get; set; } = FillTypes.Even_Odd;

        public bool MeshIsSolidObject => false;

        private double cutMargin = .01;

		public Polygons FindSlice(IObject3D item)
		{
			var mesh = new Mesh(item.Mesh.Vertices, item.Mesh.Faces);

			var itemMatrix = item.WorldMatrix(this);
			mesh.Transform(itemMatrix);

			var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, SliceHeight.Value(this)));
			var slice = SliceLayer.CreateSlice(mesh, cutPlane);

			return slice;
		}

        public virtual void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
        {
            this.DrawPath();
        }

        public virtual AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
        {
            return this.GetWorldspaceAabbOfDrawPath();
        }
        
		public override void Apply(UndoBuffer undoBuffer)
		{
			var newPathObject = new CustomPathObject3D();

			var vertexStorage = new VertexStorage(this.GetRawPath());
			newPathObject.PathForEditing.SvgDString = vertexStorage.SvgDString;
            newPathObject.Rebuild();

            base.Apply(undoBuffer, new IObject3D[] { newPathObject });
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLocks = this.RebuilLockAll();

			return RunAysncRebuild(
				"Find Slice".Localize(),
				(reporter, cancellationToken) =>
				{
					var polygons = new Polygons();

					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var slicePolygons = FindSlice(sourceItem);

						// set the fill type from the FillType tranlated to the ClipperLib enum
						polygons = polygons.ApplyClipping(slicePolygons, ClipType.ctUnion, (PolyFillType)FillType);
					}

					VertexStorage = polygons.CreateVertexStorage();

					RemoveAllButSource();
					SourceContainer.Visible = false;

                    this.Mesh = VertexSourceToMesh.Extrude(VertexStorage, Constants.PathPolygonsHeight);

                    UiThread.RunOnIdle(() =>
					{
						rebuildLocks.Dispose();
						Invalidate(InvalidateType.DisplayValues);
						this.DoRebuildComplete();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					});

					return Task.CompletedTask;
				});
		}

		public void UpdateControls(PublicPropertyChange change)
		{
		}

        public IVertexSource GetRawPath()
        {
            return VertexStorage;
        }

        public IEnumerable<SceneOperation> GetOperations()
        {
            return BoxPathObject3D.GetOperations(GetType());
        }
    }
}