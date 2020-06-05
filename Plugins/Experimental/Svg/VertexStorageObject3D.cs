/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Plugins.SvgConverter
{
	public class VertexStorageObject3D : Object3D
	{
		private VertexStorage vertexStorage;

		private IObject3D generatedMesh;

		public VertexStorageObject3D(string svgDString)
			: this(new VertexStorage(svgDString))
		{
		}

		public VertexStorageObject3D(VertexStorage vertexStorage)
		{
			this.vertexStorage = vertexStorage;

			this.Children.Modify(children =>
			{
				int i = 0;
				foreach (var v in vertexStorage.Vertices())
				{
					if (!v.IsMoveTo && !v.IsLineTo)
					{
						continue;
					}

					var localVertex = v;

					var localIndex = i++;

					var item = new Object3D()
					{
						Mesh = CreateCylinder(1, 1),
						Matrix = Matrix4X4.CreateTranslation(v.position.X, v.position.Y, 0),
						Color = Color.Green
					};

					item.Invalidated += (s, e) =>
					{
						System.Diagnostics.Debugger.Break();
						//vertexStorage.modify_vertex(localIndex, item.Matrix.Position.X, localVertex.position.Y = item.Matrix.Position.Y);
					};

					children.Add(item);
				}

				children.Add(generatedMesh = new Object3D()
				{
					Mesh = VertexSourceToMesh.Extrude(vertexStorage, 0.5),
					Color = new Color("#93CEFF99")
				});
			});

			this.Invalidated += (s, e) =>
			{
				// Recompute path from content
				generatedMesh.Mesh = VertexSourceToMesh.Extrude(vertexStorage, 0.5);
				//VertexSourceToMesh.Revolve(vertexStorage));
			};
		}

		// TODO: EditorTools owns this, move to more general location
		private static Mesh CreateCylinder(double height = 20, double radius = 10, int rotationCount = 30)
		{
			var path = new VertexStorage();
			path.MoveTo(0, 0);
			path.LineTo(radius, 0);
			path.LineTo(radius, height);
			path.LineTo(0, height);

			return VertexSourceToMesh.Revolve(path, rotationCount);
		}
	}
}
