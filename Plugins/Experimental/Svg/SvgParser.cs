/*
Copyright (c) 2017, John Lewin
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
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;
using Svg;

namespace MatterHackers.MatterControl.Plugins.SvgConverter
{
	public static class SvgParser
	{
		private static XNamespace svg = "http://www.w3.org/2000/svg";

		public static List<SvgNodeInfo> GetPaths(string filePath)
		{
			var allPaths = new List<SvgNodeInfo>();

			// Attempt to expand AssetPath into full path
			if (Path.GetDirectoryName(filePath) == "")
			{

				filePath = Path.Combine(Object3D.AssetsPath, filePath);
			}

			var root = XElement.Load(filePath);

			ProcTree(root, allPaths, 0);

			return allPaths;
		}

		public class SvgNodeInfo
		{
			private static TypeConverter svgUnitConverter = TypeDescriptor.GetConverter(typeof(SvgUnit));

			private XElement elem;

			public SvgNodeInfo(XElement elem)
			{
				this.elem = elem;

				var elemStyles = new Dictionary<string, string>();

				string style = (string)elem.Attribute("style");

				if (!string.IsNullOrWhiteSpace(style))
				{
					var cssParser = new ExCSS.Parser();

					var inlineSheet = cssParser.Parse("#a{" + style + "}");

					foreach (var rule in inlineSheet.StyleRules)
					{
						foreach (var decl in rule.Declarations)
						{
							elemStyles[decl.Name] = decl.Term.ToString();
						}
					}
				}

				// Look for explicit attribute and override style
				foreach(var attributeName in new[] {"stroke", "fill" })
				{
					if ((string)elem.Attribute(attributeName) is string attributeValue)
					{
						elemStyles[attributeName] = attributeValue;
					}
				}

				// Process style rule into value
				if (elemStyles.TryGetValue("stroke", out string strokeText))
				{
					this.Stroke = ColorExtensions.GetColorServer(strokeText);
				}

				string strokeWidth = (string)elem.Attribute("stroke-width");
				this.StrokeWidth = string.IsNullOrWhiteSpace(strokeWidth) ? new SvgUnit(1.0f) : (SvgUnit)svgUnitConverter.ConvertFrom(strokeWidth);

				// Process style rule into value
				if (elemStyles.TryGetValue("fill", out string fillText))
				{
					this.Fill = ColorExtensions.GetColorServer(fillText);
				}
			}

			public double Z { get; internal set; }

			public SvgColourServer Stroke { get; internal set; }

			public SvgUnit StrokeWidth { get; internal set; }

			public SvgColourServer Fill { get; internal set; }
		}

		public class SvgPathInfo : SvgNodeInfo
		{
			public SvgPathInfo(XElement elem)
				: base(elem)
			{
				this.DString = (string)elem.Attribute("d");
			}

			public string DString { get; set; }
		}

		public class SvgPolygonInfo : SvgNodeInfo
		{
			private static TypeConverter svgPointCollectionConverter = TypeDescriptor.GetConverter(typeof(SvgPointCollection));

			public List<Vector2> Points { get; set; }

			public SvgPolygonInfo(XElement elem)
				: base(elem)
			{
				var points = new List<Vector2>();

				if (elem.Name.LocalName == "polygon")
				{
					string pointsString = (string)elem.Attribute("points");
					var svgPoints = svgPointCollectionConverter.ConvertFrom(pointsString) as SvgPointCollection; ;

					for (var i = 0; i < svgPoints.Count; i += 2)
					{
						points.Add(new Vector2()
						{
							X = svgPoints[i].Value,
							Y = svgPoints[i + 1].Value,
						});
					}
				}
				else if (elem.Name.LocalName == "rect")
				{
					// <rect xmlns="http://www.w3.org/2000/svg" x="256.002" y="400.008" style="fill:#868491;" width="64" height="16"/>
					var x = (double)elem.Attribute("x");
					var y = (double)elem.Attribute("y");

					var width = (double)elem.Attribute("width");
					var height = (double)elem.Attribute("height");

					points.Add(new Vector2(x, y));
					points.Add(new Vector2(x + width, y));
					points.Add(new Vector2(x + width, y + height));
					points.Add(new Vector2(x, y + height));
					points.Add(new Vector2(x, y));
				}

				this.Points = points;
			}
		}

		public class SvgPolyLineInfo : SvgNodeInfo
		{
			private static TypeConverter svgPointCollectionConverter = TypeDescriptor.GetConverter(typeof(SvgPointCollection));

			public List<Vector2> Points { get; set; }

			public SvgPolyLineInfo(XElement elem)
				: base(elem)
			{
				var points = new List<Vector2>();

				string pointsString = ((string)elem.Attribute("points"));
				var svgPoints = svgPointCollectionConverter.ConvertFrom(pointsString) as SvgPointCollection; ;

				for (var i = 0; i < svgPoints.Count; i += 2)
				{
					points.Add(new Vector2()
					{
						X = svgPoints[i].Value,
						Y = svgPoints[i + 1].Value,
					});
				}

				this.Points = points;
			}
		}

		public class SvgLineInfo : SvgNodeInfo
		{
			private static TypeConverter svgPointCollectionConverter = TypeDescriptor.GetConverter(typeof(SvgPointCollection));

			public List<Vector2> Points { get; set; }

			public SvgLineInfo(XElement elem)
				: base(elem)
			{
				double x1 = (double)elem.Attribute("x1");
				double x2 = (double)elem.Attribute("x2");
				double y1 = (double)elem.Attribute("y1");
				double y2 = (double)elem.Attribute("y2");

				this.Points = new List<Vector2> { new Vector2(x1, y1), new Vector2(x2, y2) }; ;
			}
		}

		private static void ProcTree(XElement parent, List<SvgNodeInfo> allPaths, int depth)
		{
			foreach (var elem in parent.Elements())
			{
				switch (elem.Name.LocalName)
				{
					case "polygon":
						allPaths.Add(new SvgPolygonInfo(elem)
						{
							Z = depth * 0.5
						});
						break;

					case "rect":
						allPaths.Add(new SvgPolygonInfo(elem)
						{
							Z = depth * 0.5
						});
						break;


					case "polyline":
						allPaths.Add(new SvgPolyLineInfo(elem)
						{
							Z = depth * 0.5
						});
						break;

					case "line":
						allPaths.Add(new SvgLineInfo(elem)
						{
							Z = depth * 0.5
						});
						break;

					case "path":
						allPaths.Add(new SvgPathInfo(elem)
						{
							Z = depth * 0.5
						});

						break;

					case "g":
						ProcTree(elem, allPaths, depth + 1);
						break;
					default:
						Console.WriteLine();
						break;
				}
			}
		}

		public class ColoredVertexSource
		{
			public IVertexSource VertexSource { get; set; }
			public Color Color { get; set; }
		}
	}

	public static class ColorExtensions
	{
		private static TypeConverter svgColorConverter = TypeDescriptor.GetConverter(typeof(SvgPaintServer));

		public static SvgColourServer GetColorServer(this XElement element, string attributeName)
		{
			return GetColorServer((string)element.Attribute(attributeName));
		}

		public static SvgColourServer GetColorServer(string value)
		{
			if (!string.IsNullOrWhiteSpace(value)
				&& value != "none"
				&& svgColorConverter.ConvertFrom(value) is SvgColourServer colorServer)
			{
				return colorServer;
			}

			return null;
		}

		public static Color GetAggColor(this System.Drawing.Color color)
		{
			return new Color(color.R, color.G, color.B, color.A);
		}
	}
}