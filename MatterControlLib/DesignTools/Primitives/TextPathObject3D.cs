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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatterHackers.MatterControl.DesignTools
{
	public class TextPathObject3D : Object3D, IPathObject, ISelectedEditorDraw
	{
		public TextPathObject3D()
		{
			Name = "Text".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Text"];
		}

		public static async Task<TextPathObject3D> Create()
		{
			var item = new TextPathObject3D();

			await item.Rebuild();
			return item;
		}

		[DisplayName("Text")]
		public StringOrExpression Text { get; set; } = "Text";

		public DoubleOrExpression PointSize { get; set; } = 24;

		[Sortable]
		[JsonConverter(typeof(StringEnumConverter))]
		public NamedTypeFace Font { get; set; } = new NamedTypeFace();

		public override bool CanFlatten => true;

		public IVertexSource VertexSource { get; set; } = new VertexStorage();

		public override void Flatten(UndoBuffer undoBuffer)
		{
			// change this from a text object to a group
			var newContainer = new GroupObject3D();
			newContainer.CopyProperties(this, Object3DPropertyFlags.All);
			foreach (var child in this.Children)
			{
				newContainer.Children.Add(child.Clone());
			}

			undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, new[] { newContainer }));
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
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
				double pointsToMm = 0.352778;

				var printer = new TypeFacePrinter(Text.Value(this), new StyledTypeFace(ApplicationController.GetTypeFace(Font), PointSize.Value(this)))
				{
					ResolutionScale = 10
				};

				var scaledLetterPrinter = new VertexSourceApplyTransform(printer, Affine.NewScaling(pointsToMm));
				var vertexSource = new VertexStorage();

				foreach (var vertex in scaledLetterPrinter.Vertices())
				{
					if (vertex.IsMoveTo)
					{
						vertexSource.MoveTo(vertex.position);
					}
					else if (vertex.IsLineTo)
					{
						vertexSource.LineTo(vertex.position);
					}
					else if (vertex.IsClose)
					{
						vertexSource.ClosePolygon();
					}
				}

				vertexSource = (VertexStorage)vertexSource.Union(vertexSource);

				this.VertexSource = vertexSource;

				// set the mesh to show the path
				this.Mesh = this.VertexSource.Extrude(Constants.PathPolygonsHeight);
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
			return Task.CompletedTask;
		}

		public override Mesh Mesh
		{
			get
			{
				if (base.Mesh == null)
				{
					using (this.RebuildLock())
					{
						var bounds = this.VertexSource.GetBounds();

						if (bounds.Width == 0 || bounds.Height == 0)
						{
							bounds = new Agg.RectangleDouble(0, 0, 60, 20);
						}

						var center = bounds.Center;
						var mesh = PlatonicSolids.CreateCube(Math.Max(8, bounds.Width), Math.Max(8, bounds.Height), 0.2);

						mesh.Translate(new Vector3(center.X, center.Y, 0));

						base.Mesh = mesh;
					}
				}

				return base.Mesh;
			}
		}

		private Mesh InitMesh()
		{
			if (!string.IsNullOrWhiteSpace(Text.Value(this)))
			{
			}

			return null;
		}

		public void DrawEditor(Object3DControlsLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e)
		{
			this.DrawPath();
		}
	}
}