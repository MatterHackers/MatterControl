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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using Svg;

namespace MatterHackers.MatterControl.Plugins.SvgConverter
{
	public class SvgObject3DEditor : IObject3DEditor
	{
		private SvgObject3D injectedItem;
		private string lastDPath;

		private string[] d = new[] {
						"m111,52.0625 l-42.999992,78.921265 l42.999992,79.016235 l86,0l43,-79.016235 l-43,-78.921265 l-86,0z",
						"m -762.85715,129.50504 c -13.44619,9.60443 15.72949,49.28263 0.25566,55.07966 -15.47384,5.79703 -19.55047,-43.28414 -35.99743,-41.68956 -16.44696,1.59458 -11.01917,50.54475 -27.31842,47.82821 -16.29925,-2.71655 4.71087,-47.2604 -10.32991,-54.10293 -15.04077,-6.84254 -34.81526,38.26345 -47.57255,27.76123 -12.75729,-10.50222 27.70993,-38.57327 18.10551,-52.01947 -9.60443,-13.446187 -49.28263,15.72949 -55.07966,0.25566 -5.79703,-15.47383 43.28414,-19.550466 41.68956,-35.997423 -1.59458,-16.446955 -50.54475,-11.019174 -47.82821,-27.318421 2.71654,-16.299247 47.2604,4.710867 54.10293,-10.329905 6.84253,-15.040771 -38.26345,-34.8152603 -27.76123,-47.5725513 10.50222,-12.7572917 38.57327,27.7099283 52.01946,18.1055048 13.4462,-9.60442353 -15.72949,-49.2826255 -0.25565,-55.0796545 15.47383,-5.79703 19.55046,43.2841379 35.99742,41.6895592 16.44696,-1.5945787 11.01918,-50.5447502 27.31842,-47.8282092 16.29925,2.716541 -4.71086,47.2603977 10.32991,54.1029302 15.04077,6.8425322 34.81526,-38.2634502 47.57255,-27.7612332 12.75729,10.502217 -27.70993,38.573271 -18.10551,52.019464 9.60443,13.446192 49.28263,-15.72949 55.07966,-0.255658 5.79703,15.473833 -43.28414,19.550469 -41.68956,35.997426 1.59458,16.446955 50.54475,11.019174 47.82821,27.318421 -2.71654,16.29925 -47.2604,-4.710866 -54.10293,10.32991 -6.84253,15.04077 38.26345,34.81526 27.76123,47.57255 -10.50222,12.75729 -38.57327,-27.70993 -52.01946,-18.10551 z",
						"m12.16472,100.99991l81.85645,-81.5815l81.85645,81.5815l-40.92823,0l0,81.97406l-81.85645,0l0,-81.97406l-40.92823,0z"
						};
		private HashSet<SvgParser.SvgNodeInfo> selectedItems;

		public bool Unlocked => true;
		public string Name => "Svg Editor";

		public GuiWidget Create(IObject3D item, UndoBuffer undoBuffer, ThemeConfig theme)
		{
			this.injectedItem = item as SvgObject3D;
			if (this.injectedItem == null)
			{
				return null;
			}

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Absolute,
				Width = 210,
				Padding = new BorderDouble(12),
			};
			column.Closed += (s, e) =>
			{
				//unregisterEvents?.Invoke(this, null);
			};

			var rightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};

			var label = new TextWidget("All Paths", textColor: AppContext.Theme.TextColor)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(0, 0, 0, 15)
			};
			column.AddChild(label);

			var row = new FlowLayoutWidget()
			{
			};
			column.AddChild(row);

			var editButton = new TextButton("Edit".Localize(), theme)
			{
				Enabled = false,
			};
			editButton.Click += async (s, e) =>
			{
				ApplicationController.Instance.OpenIntoNewTab(new[] { new InMemoryLibraryItem(new VertexStorageObject3D(lastDPath)) });
			};

			int pathCount = 1;
			var droplist = new PopupMenu(theme)
			{
				BackgroundColor = theme.InactiveTabColor
			};
			row.AddChild(droplist);

			row.AddChild(editButton);

			var allPaths = SvgParser.GetPaths(this.injectedItem.SvgPath);

			selectedItems = new HashSet<SvgParser.SvgNodeInfo>(allPaths);

			foreach (var pathItem in allPaths)
			{
				bool itemChecked = true;

				PopupMenu.MenuItem menuItem = null;

				menuItem = droplist.CreateBoolMenuItem(
					$"Path {pathCount++}",
					() => itemChecked,
					(isChecked) =>
					{
						lastDPath = (pathItem as SvgParser.SvgPathInfo)?.DString;
						editButton.Enabled = true;

						if (isChecked)
						{
							selectedItems.Add(pathItem);
						}
						else
						{
							selectedItems.Remove(pathItem);
						}

						this.Rebuild();
					});
			}

			this.Rebuild();

			return column;
		}

		public void Rebuild()
		{
			try
			{
				injectedItem.Children.Modify(children =>
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

					mirror.Matrix = Matrix4X4.CreateScale(0.2);

					children.Add(mirror);
				});
			}
			catch { }
		}

		public IEnumerable<Type> SupportedTypes() => new Type[] { typeof(SvgObject3D) };
	}
}