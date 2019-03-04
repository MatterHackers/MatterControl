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
	public class TextPathObject3D : Object3D, IPathObject, IEditorDraw
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
		public string Text { get; set; } = "Text";

		public double PointSize { get; set; } = 24;

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

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			using (RebuildLock())
			{
				double pointsToMm = 0.352778;

				var printer = new TypeFacePrinter(Text, new StyledTypeFace(ApplicationController.GetTypeFace(Font), PointSize))
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

				this.VertexSource = vertexSource;
				base.Mesh = null;
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
			if (!string.IsNullOrWhiteSpace(Text))
			{
			}

			return null;
		}

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			ImageToPathObject3D.DrawPath(this);
		}
	}
}