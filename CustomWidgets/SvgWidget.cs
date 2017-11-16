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

using System.Collections.Generic;
using System.Xml.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class SvgWidget : GuiWidget
	{
		List<ColoredVertexSource> items = new List<ColoredVertexSource>();

		private static XNamespace svg = "http://www.w3.org/2000/svg";

		private ImageBuffer imageBuffer;

		public double Scale { get; set; } = 0.7;

		public SvgWidget(string filePath, double scale, int width = -1, int height = -1)
		{
			var root = XElement.Load(filePath);

			this.Scale = scale;

			string viewBox = (string)root.Attribute("viewBox");
			if (!string.IsNullOrEmpty(viewBox))
			{
				var segments = viewBox.Split(' ');

				if (width == -1)
				{
					int.TryParse(segments[2], out width);
				}

				if (height == -1)
				{
					int.TryParse(segments[3], out height);
				}
			}

			foreach (var elem in root.Elements(svg + "g"))
			{
				ProcTree(elem);
			}

			width = (int)(width * this.Scale);
			height = (int)(height * this.Scale);

			imageBuffer = new ImageBuffer(width, height);

			this.MinimumSize = new VectorMath.Vector2(width, height);

			var graphics2D = imageBuffer.NewGraphics2D();

			graphics2D.SetTransform(Affine.NewScaling(this.Scale));
			foreach (var item in items)
			{
				graphics2D.Render(item.VertexSource, item.Color);
			}

			imageBuffer.FlipY();

			//this.source = new PathStorage(svgDString);
		}

		private void ProcTree(XElement g)
		{
			foreach (var elem in g.Elements())
			{
				switch (elem.Name.LocalName)
				{
					case "path":
					case "polygon":

						string htmlColor = ((string)elem.Attribute("style")).Replace("fill:", "").Replace(";", "");

						if (elem.Name.LocalName == "polygon")
						{
							var path = new VertexStorage();

							string pointsLine = ((string)elem.Attribute("points"))?.Trim();

							var segments = pointsLine.Split(' ');

							bool firstMove = true;
							foreach(var segment in segments)
							{
								var point = segment.Split(',');

								if (firstMove)
								{
									path.MoveTo(new Vector2(double.Parse(point[0]), double.Parse(point[1])));
									firstMove = false;
								}
								else
								{
									path.LineTo(new Vector2(double.Parse(point[0]), double.Parse(point[1])));
								}
							}

							path.ClosePolygon();

							items.Add(new ColoredVertexSource()
							{
								VertexSource = path,
								Color = new Color(htmlColor)
							});

						}
						else
						{
							string dString = (string)elem.Attribute("d");
							items.Add(new ColoredVertexSource()
							{
								VertexSource = new VertexStorage(dString),
								Color = new Color(htmlColor)
							});
						}

						break;

					case "g":
						ProcTree(elem);
						break;
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Render(imageBuffer, Point2D.Zero);

			base.OnDraw(graphics2D);
		}

		public class ColoredVertexSource
		{
			public IVertexSource VertexSource { get; set; }
			public Color Color { get; set; }
		}
	}
}
