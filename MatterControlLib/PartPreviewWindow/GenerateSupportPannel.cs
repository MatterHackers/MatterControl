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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	[UnlockLinkAttribute("175mm-pla-filament-yellow-1-kg")]
	public class GeneratedSupportObject3D : Object3D
	{
		public override bool Persistable { get => ApplicationController.Instance.UserHasPermissions(typeof(GeneratedSupportObject3D)); }
	}

	public class GenerateSupportPannel : FlowLayoutWidget
	{
		private InteractiveScene scene;
		private ThemeConfig theme;

		public GenerateSupportPannel(ThemeConfig theme, InteractiveScene scene)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.scene = scene;

			VAnchor = VAnchor.Fit;
			HAnchor = HAnchor.Absolute;
			Width = 400;

			// put in the registered information
			if (!ApplicationController.Instance.UserHasPermissions(typeof(GeneratedSupportObject3D)))
			{
				this.AddChild(PublicPropertyEditor.GetUnlockRow(theme, "175mm-pla-filament-yellow-1-kg"));

				var wrappedText = @"Advanced Support Generation

This tool can automatically analize and add support to your parts.
Just select the part you want to add support to and click 'Generate'.

You can try it out for free, then upgrade when you want to print with your automatic supports".Localize();
				this.AddChild(new WrappedTextWidget(wrappedText, theme.H1PointSize)
				{
					TextColor = theme.TextColor,
					Margin = 5
				});
			}

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

			// add 'Generate Supports' button
			var generateButton = new TextButton("Generate".Localize(), theme)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Right,
				Margin = 5,
				ToolTipText = "Find and create supports where needed".Localize()
			};
			this.AddChild(generateButton);
			generateButton.Click += (s, e) => Rebuild();

			// add 'Remove Auto Supports' button
			var removeButton = new TextButton("Remove".Localize(), theme)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Right,
				Margin = 5,
				ToolTipText = "Remvoe all auto generated supports".Localize()
			};
			this.AddChild(removeButton);
			removeButton.Click += (s, e) => RemoveExisting();
		}

		public double MaxOverHangAngle { get; private set; } = 45;

		public double PillarSize { get; private set; } = 4;

		void RemoveExisting()
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

		private void Rebuild()
		{
			// Get visible meshes for each of them
			var visibleMeshes = scene.Children.SelectMany(i => i.VisibleMeshes());

			var selectedItem = scene.SelectedItem;
			if(selectedItem != null)
			{
				visibleMeshes = selectedItem.VisibleMeshes();
			}

			var supportCandidates = visibleMeshes.Where(i => i.OutputType != PrintOutputTypes.Support);

			// find all the faces that are candidates for support
			var verts = new Vector3List();
			var faces = new FaceList();
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
							faces.Add(new int[] { verts.Count, verts.Count + 1, verts.Count + 2 });
							verts.Add(Vector3.Transform(triangle.p0, matrix));
							verts.Add(Vector3.Transform(triangle.p0, matrix));
							verts.Add(Vector3.Transform(triangle.p0, matrix));
						}
					}
				}
			}

			if (faces.Count > 0)
			{
				// get the bounds of all verts
				var bounds = verts.Bounds();

				// create the gird of possible support
				// foreach face set the support heights in the overlapped support grid
				// foreach grid column that has data
				// trace down from the top to the first bottom hit (or bed)
				// add a support column
				var first = faces.First();
				var position = verts[first[0]];
				AddSupportColumn(position.X, position.Y, position.Z, 0);
			}

			// this is the theory for regions rather than pillars
			// separate the faces into face patch groups (these are the new support tops)
			// project all the vertecies of each patch group down until they hit an up face in the scene (or 0)
			// make a new patch group at the z of the hit (thes will bthe bottoms)
			// find the outline of the patch groups (these will be the walls of the top and bottom patches
			// make a new mesh object with the top, bottom and walls, add it to the scene and mark it as support
		}

		private void AddSupportColumn(double gridX, double gridY, double topZ, double bottomZ)
		{
			scene.Children.Add(new GeneratedSupportObject3D()
			{
				Mesh = PlatonicSolids.CreateCube(PillarSize / 2, PillarSize / 2, topZ - bottomZ),
				Matrix = Matrix4X4.CreateTranslation(gridX, gridY, bottomZ + (topZ - bottomZ) / 2)
			});
		}
	}
}