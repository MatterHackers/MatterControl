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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	[HideFromTreeViewAttribute, Immutable]
	public class GeneratedSupportObject3D : Object3D
	{
		public GeneratedSupportObject3D()
		{
			OutputType = PrintOutputTypes.Support;
		}
	}

	public class GenerateSupportPanel : FlowLayoutWidget
	{
		/// <summary>
		/// The amount to reduce the pillars so they are separated in the 3D view
		/// </summary>
		private double reduceAmount => PillarSize / 8;

		private InteractiveScene scene;
		private ThemeConfig theme;

		public GenerateSupportPanel(ThemeConfig theme, InteractiveScene scene)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.scene = scene;

			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Absolute;
			this.Width = 300;
			this.BackgroundColor = theme.BackgroundColor;
			this.Padding = theme.DefaultContainerPadding;

			// put in support pillar size

			// support pillar resolution
			var pillarSizeField = new DoubleField(theme);
			pillarSizeField.Initialize(0);
			pillarSizeField.DoubleValue = PillarSize;
			pillarSizeField.ValueChanged += (s, e) =>
			{
				PillarSize = pillarSizeField.DoubleValue;
			};

			var pillarRow = PublicPropertyEditor.CreateSettingsRow("Pillar Size".Localize(), "The width and depth of the support pillars".Localize());
			pillarRow.AddChild(pillarSizeField.Content);
			this.AddChild(pillarRow);

			// put in the angle setting
			var overHangField = new DoubleField(theme);
			overHangField.Initialize(0);
			overHangField.DoubleValue = MaxOverHangAngle;
			overHangField.ValueChanged += (s, e) =>
			{
				MaxOverHangAngle = overHangField.DoubleValue;
			};

			var overHangRow = PublicPropertyEditor.CreateSettingsRow("Overhang Angle".Localize(), "The angle to generate support for".Localize());
			overHangRow.AddChild(overHangField.Content);
			this.AddChild(overHangRow);

			var buttonRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(top: 5)
			};
			this.AddChild(buttonRow);

			buttonRow.AddChild(new HorizontalSpacer());

			// add 'Remove Auto Supports' button
			var removeButton = theme.CreateDialogButton("Remove".Localize());
			removeButton.ToolTipText = "Remove all auto generated supports".Localize();
			removeButton.Click += (s, e) => RemoveExisting();
			buttonRow.AddChild(removeButton);

			// add 'Generate Supports' button
			var generateButton = theme.CreateDialogButton("Generate".Localize());
			generateButton.ToolTipText = "Find and create supports where needed".Localize();
			generateButton.Click += (s, e) => Rebuild();
			buttonRow.AddChild(generateButton);
			theme.ApplyPrimaryActionStyle(generateButton);
		}

		public static double MaxOverHangAngle { get; private set; } = 45;

		public double PillarSize { get; private set; } = 4;

		private void AddSupportColumn(double gridX, double gridY, double bottomZ, double topZ)
		{
			if(topZ - bottomZ < .01)
			{
				// less than 10 micros high, don't ad it
				return;
			}
			scene.Children.Add(new GeneratedSupportObject3D()
			{
				Mesh = PlatonicSolids.CreateCube(PillarSize - reduceAmount, PillarSize - reduceAmount, topZ - bottomZ),
				Matrix = Matrix4X4.CreateTranslation(gridX, gridY, bottomZ + (topZ - bottomZ) / 2)
			});
		}

		private void Rebuild()
		{
			Task.Run(() =>
			{
				// Get visible meshes for each of them
				var visibleMeshes = scene.Children.SelectMany(i => i.VisibleMeshes());

				var selectedItem = scene.SelectedItem;
				if (selectedItem != null)
				{
					visibleMeshes = selectedItem.VisibleMeshes();
				}

				var supportCandidates = visibleMeshes.Where(i => i.OutputType != PrintOutputTypes.Support);

				// find all the faces that are candidates for support
				var upVerts = new Vector3List();
				var upFaces = new FaceList();
				var downVerts = new Vector3List();
				var downFaces = new FaceList();
				foreach (var item in supportCandidates)
				{
					var matrix = item.WorldMatrix(scene);
					foreach (var face in item.Mesh.Faces)
					{
						var face0Normal = Vector3.TransformVector(face.Normal, matrix).GetNormal();
						var angle = MathHelper.RadiansToDegrees(Math.Acos(Vector3.Dot(-Vector3.UnitZ, face0Normal)));

						if (angle < MaxOverHangAngle)
						{
							foreach (var triangle in face.AsTriangles())
							{
								downFaces.Add(new int[] { downVerts.Count, downVerts.Count + 1, downVerts.Count + 2 });
								downVerts.Add(Vector3.Transform(triangle.p0, matrix));
								downVerts.Add(Vector3.Transform(triangle.p1, matrix));
								downVerts.Add(Vector3.Transform(triangle.p2, matrix));
							}
						}

						if (angle > 0)
						{
							foreach (var triangle in face.AsTriangles())
							{
								upFaces.Add(new int[] { upVerts.Count, upVerts.Count + 1, upVerts.Count + 2 });
								upVerts.Add(Vector3.Transform(triangle.p0, matrix));
								upVerts.Add(Vector3.Transform(triangle.p1, matrix));
								upVerts.Add(Vector3.Transform(triangle.p2, matrix));
							}
						}
					}
				}

				if (downFaces.Count > 0)
				{
					var downTraceData = downFaces.CreateTraceData(downVerts, 0);
					var upTraceData = upFaces.CreateTraceData(upVerts, 0);

					// get the bounds of all verts
					var bounds = downVerts.Bounds();

					// create the gird of possible support
					RectangleDouble gridBounds = new RectangleDouble(Math.Floor(bounds.minXYZ.X / PillarSize),
						Math.Floor(bounds.minXYZ.Y / PillarSize),
						Math.Ceiling(bounds.maxXYZ.X / PillarSize),
						Math.Ceiling(bounds.maxXYZ.Y / PillarSize));

					var supportGrid = new List<List<double>>((int)(gridBounds.Width * gridBounds.Height));

					// at the center of every grid item add in a list of all the top faces to look down from
					for (int y = 0; y < gridBounds.Height; y++)
					{
						var yPos = (gridBounds.Bottom + y) * PillarSize;
						for (int x = 0; x < gridBounds.Width; x++)
						{
							var xPos = (gridBounds.Left + x) * PillarSize;
							IntersectInfo upHit = null;
							var upRay = new Ray(new Vector3(xPos, yPos, 0), Vector3.UnitZ, intersectionType: IntersectionType.Both);
							do
							{
								upHit = downTraceData.GetClosestIntersection(upRay);
								if (upHit != null)
								{
									// we found a ceiling above this spot, look down from that to find the first floor
									var downRay = new Ray(new Vector3(upHit.HitPosition.X, upHit.HitPosition.Y, upHit.HitPosition.Z - .001), -Vector3.UnitZ, intersectionType: IntersectionType.Both);
									var downHit = upTraceData.GetClosestIntersection(downRay);
									if (downHit != null)
									{
										AddSupportColumn(downHit.HitPosition.X, downHit.HitPosition.Y, downHit.HitPosition.Z, upHit.HitPosition.Z);
									}
									else
									{
										// did not find a hit, go to the bed
										AddSupportColumn(upHit.HitPosition.X, upHit.HitPosition.Y, upRay.origin.Z, upHit.HitPosition.Z);
									}

									// make a new ray just past the last hit to keep looking for up hits
									upRay = new Ray(new Vector3(xPos, yPos, upHit.HitPosition.Z + .001), Vector3.UnitZ, intersectionType: IntersectionType.Both);
								}
							} while (upHit != null);
						}
					}

					// foreach face set the support heights in the overlapped support grid
					// foreach grid column that has data
					// trace down from the top to the first bottom hit (or bed)
					// add a support column
					var first = downFaces.First();
					var position = downVerts[first[0]];
					//AddSupportColumn(position.X, position.Y, position.Z, 0);
				}

				// this is the theory for regions rather than pillars
				// separate the faces into face patch groups (these are the new support tops)
				// project all the vertices of each patch group down until they hit an up face in the scene (or 0)
				// make a new patch group at the z of the hit (these will be the bottoms)
				// find the outline of the patch groups (these will be the walls of the top and bottom patches
				// make a new mesh object with the top, bottom and walls, add it to the scene and mark it as support
			});
		}

		private void RemoveExisting()
		{
			var existingSupports = scene.Children.Where(i => i.GetType() == typeof(GeneratedSupportObject3D));

			scene.Children.Modify((list) =>
			{
				foreach (var item in existingSupports)
				{
					list.Remove(item);
				}
			});
		}

		public static bool RequiresSupport(InteractiveScene scene)
		{
			bool supportInScene = scene.VisibleMeshes().Any(i => i.WorldOutputType() == PrintOutputTypes.Support);
			if (!supportInScene)
			{
				// there is no support in the scene check if there are faces that require support
				var supportCandidates = scene.VisibleMeshes().Where(i => i.OutputType != PrintOutputTypes.Support);

				// find all the faces that are candidates for support
				foreach (var item in supportCandidates)
				{
					var matrix = item.WorldMatrix(scene);
					foreach (var face in item.Mesh.Faces)
					{
						bool aboveBed = false;
						foreach(var vertex in face.Vertices())
						{
							if(Vector3.Transform(vertex.Position, matrix).Z > .01)
							{
								aboveBed = true;
								break;
							}
						}
						if (aboveBed)
						{
							var face0Normal = Vector3.TransformVector(face.Normal, matrix).GetNormal();
							var angle = MathHelper.RadiansToDegrees(Math.Acos(Vector3.Dot(-Vector3.UnitZ, face0Normal)));

							if (angle < MaxOverHangAngle)
							{
								// TODO: consider how much area all supported polygons represent
								return true;
							}
						}
					}
				}
			}

			return false;
		}
	}

	public static class FaceListExtensions
	{
		public static IPrimitive CreateTraceData(this FaceList faceList, Vector3List vertexList, int maxRecursion = int.MaxValue)
		{
			var allPolys = new List<IPrimitive>();

			foreach (var face in faceList)
			{
				allPolys.Add(new TriangleShape(vertexList[face[0]], vertexList[face[1]], vertexList[face[2]], null));
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(allPolys, maxRecursion);
		}
	}
}