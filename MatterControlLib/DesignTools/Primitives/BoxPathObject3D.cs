/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools
{
	public class BoxPathObject3D : PrimitiveObject3D, IPathObject, IObject3DControlsProvider, IEditorDraw
	{
		public BoxPathObject3D()
		{
			Name = "Box".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Cube"];
		}

		public override string ThumbnailName => "Box";

		[JsonIgnore]
		private IVertexSource _vertexSource = new VertexStorage();

		public IVertexSource VertexSource
		{
			get => _vertexSource;

			set
			{
				_vertexSource = value;
				// set the mesh to show the path
				this.Mesh = this.VertexSource.Extrude(Constants.PathPolygonsHeight);
			}
		}

		public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
		{
			this.DrawPath();
		}

		/// <summary>
		/// This is the actual serialized with that can use expressions
		/// </summary>
		[MaxDecimalPlaces(2)]
		[Slider(1, 400, Easing.EaseType.Quadratic, snapDistance: 1)]
		public DoubleOrExpression Width { get; set; } = 20;

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, Easing.EaseType.Quadratic, snapDistance: 1)]
		public DoubleOrExpression Depth { get; set; } = 20;

		public static async Task<BoxPathObject3D> Create()
		{
			var item = new BoxPathObject3D();
			await item.Rebuild();
			return item;
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.MoveInZ);
			object3DControlsLayer.AddWidthDepthControls(this, Width, Depth, null);

			object3DControlsLayer.AddControls(ControlTypes.MoveInZ);
			object3DControlsLayer.AddControls(ControlTypes.RotateXYZ);
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (SheetObject3D.NeedsRebuild(this, invalidateArgs))
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
				using (new CenterAndHeightMaintainer(this))
				{
					var width = Width.Value(this);
					var depth = Depth.Value(this);
					VertexSource = new RoundedRect(-width / 2, -depth / 2, width / 2, depth / 2, 0);
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
			return Task.CompletedTask;
		}
	}
}