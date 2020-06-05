/*
Copyright (c) 2019, John Lewin
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
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.VectorMath;
using Svg;

namespace MatterHackers.MatterControl.Plugins.SvgConverter
{
	public class SvgObject3D : Object3D
	{
		public string DString { get; set; }
		public string SvgPath { get; set; }

		public override Task Rebuild()
		{
			var allPaths = SvgParser.GetPaths(this.SvgPath);

			var selectedItems = new HashSet<SvgParser.SvgNodeInfo>(allPaths);

			try
			{
				this.Children.Modify(children =>
				{
					children.Clear();

					var svgContent = new List<IObject3D>();

					int i = 0;
					foreach (var item in selectedItems)
					{
						IVertexSource vertexSource = null;

						if (item is SvgParser.SvgPolygonInfo polygon)
						{
							var storage = new VertexStorage();

							storage.MoveTo(polygon.Points[0]);

							for (var j = 1; j < polygon.Points.Count; j++)
							{
								storage.LineTo(polygon.Points[j]);
							}

							// close
							storage.LineTo(polygon.Points[0]);

							vertexSource = storage;
						}
						else if (item is SvgParser.SvgPolyLineInfo polyline)
						{
							var storage = new VertexStorage();
							storage.MoveTo(polyline.Points[0]);

							for (var j = 1; j < polyline.Points.Count; j++)
							{
								storage.LineTo(polyline.Points[j]);
							}

							vertexSource = storage;
						}
						else if (item is SvgParser.SvgLineInfo line)
						{
							var storage = new VertexStorage();
							storage.MoveTo(line.Points[0]);
							storage.LineTo(line.Points[1]);

							vertexSource = storage;
						}
						else if (item is SvgParser.SvgPathInfo path)
						{
							vertexSource = new VertexStorage(path.DString);
						}
						else
						{
							// Skip unknown type
							continue;
						}

						var flattened = new FlattenCurves(vertexSource)
						{
							ResolutionScale = 6
						};

						var itemZ = 3 + item.Z + (0.1 * i++);

						if (item.Fill is SvgColourServer fill)
						{
							var fillColor = fill.Colour.GetAggColor();

							var object3D = new Object3D()
							{
								Mesh = VertexSourceToMesh.Extrude(flattened, itemZ),
								Color = fillColor,
								// Flip
								//Matrix = Matrix4X4.Identity * Matrix4X4.CreateScale(1, -1, 1) 
							};

							svgContent.Add(object3D);
						}

						if (item.Stroke is SvgColourServer stroke)
						{
							var aggStroke = new Stroke(flattened, item.StrokeWidth)
							{
								LineCap = LineCap.Round,
								LineJoin = LineJoin.Round
							};

							//							aggStroke.l

							var strokeObject = new Object3D()
							{
								Mesh = VertexSourceToMesh.Extrude(aggStroke, itemZ),
								Color = stroke.Colour.GetAggColor()
							};

							svgContent.Add(strokeObject);
						}
					}

					var mirror = new MirrorObject3D()
					{
						MirrorOn = MirrorObject3D.MirrorAxis.Y_Axis
					};

					mirror.Children.Modify(list =>
					{
						list.AddRange(svgContent);
					});

					mirror.Rebuild();

					mirror.Matrix = Matrix4X4.CreateScale(0.15);

					children.Add(mirror);
				});
			}
			catch { }

			return base.Rebuild();
		}
	}
}