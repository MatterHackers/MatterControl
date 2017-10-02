/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using System.IO;
using System.Linq;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Plugins.TextCreator
{
	public class TextGenerator
	{
		private double lastHeightValue = 1;
		private double lastSizeValue = 1;
		private bool hasUnderline;

		private TypeFace boldTypeFace;
		private Vector2[] characterSpacing;

		public TextGenerator()
		{
			boldTypeFace = TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(Path.Combine("Fonts", "LiberationSans-Bold.svg")));
		}

		public IObject3D CreateText(string wordText, double wordSize, double wordHeight, double spacing, bool createUnderline)
		{
			var groupItem = new TextObject()
			{
				ItemType = Object3DTypes.Group,
				Text = wordText,
				Spacing = spacing,
				ActiveEditor = "TextEditor"
			};

			StyledTypeFace typeFace = new StyledTypeFace(boldTypeFace, 12);
			TypeFacePrinter printer = new TypeFacePrinter(wordText, typeFace);

			Vector2 size = printer.GetSize(wordText);
			double centerOffset = -size.x / 2;

			double ratioPerMeshGroup = 1.0 / wordText.Length;
			double currentRatioDone = 0;

			characterSpacing = new Vector2[wordText.Length];

			for (int i = 0; i < wordText.Length; i++)
			{
				string letter = wordText[i].ToString();
				TypeFacePrinter letterPrinter = new TypeFacePrinter(letter, typeFace);

				Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, 10 + (i % 2));
				if (textMesh.Faces.Count > 0)
				{
					var characterObject = new Object3D()
					{
						Mesh = textMesh,
					};

					characterSpacing[i] = new Vector2( printer.GetOffsetLeftOfCharacterIndex(i).x + centerOffset, 0);

					groupItem.Children.Add(characterObject);

					//public static void PlaceMeshGroupOnBed(List<MeshGroup> meshesGroupList, List<Matrix4X4> meshTransforms, int index)
					{
						AxisAlignedBoundingBox bounds = characterObject.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
						Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;

						characterObject.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.z + bounds.ZSize / 2));
					}

					currentRatioDone += ratioPerMeshGroup;
				}

				//processingProgressControl.PercentComplete = ((i + 1) * 95 / wordText.Length);
			}

			SetWordSpacing(groupItem, spacing);
			SetWordSize(groupItem, wordSize);
			SetWordHeight(groupItem, wordHeight);

			if (createUnderline)
			{
				groupItem.Children.Add(CreateUnderline(groupItem));
			}

			// jlewin - restore progress
			//processingProgressControl.PercentComplete = 95;

			return groupItem;
		}

		private IObject3D CreateUnderline(IObject3D group)
		{
			if (group.HasChildren())
			{
				AxisAlignedBoundingBox bounds = group.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

				double xSize = bounds.XSize;
				double ySize = lastSizeValue * 3;
				double zSize = bounds.ZSize / 3;

				var lineObject = new Object3D()
				{
					Mesh = PlatonicSolids.CreateCube(xSize, ySize, zSize),
					Matrix = Matrix4X4.CreateTranslation((bounds.maxXYZ.x + bounds.minXYZ.x) / 2, bounds.minXYZ.y + ySize / 2 - ySize * 1 / 3, zSize / 2)
				};

				return lineObject;
			}

			return null;
		}

		public void EnableUnderline(IObject3D group, bool enableUnderline)
		{
			hasUnderline = enableUnderline;

			if (enableUnderline && group.HasChildren())
			{
				// we need to add the underline
				group.Children.Add(CreateUnderline(group));
			}
			else if (group.HasChildren())
			{
				// we need to remove the underline
				group.Children.Remove(group.Children.Last());
			}
		}

		public void RebuildUnderline(IObject3D group)
		{
			if (hasUnderline && group.HasChildren())
			{
				// Remove the underline object
				group.Children.Remove(group.Children.Last());

				// we need to add the underline
				group.Children.Add(CreateUnderline(group));
			}
		}

		public void SetWordSpacing(IObject3D group, double spacing, bool rebuildUnderline = false)
		{
			if (group.HasChildren())
			{
				var bedCenter = ApplicationController.Instance.ActivePrinter.Bed.BedCenter;

				int i = 0;
				foreach (var sceneItem in group.Children)
				{
					Vector3 startPosition = Vector3.Transform(Vector3.Zero, sceneItem.Matrix);

					sceneItem.Matrix *= Matrix4X4.CreateTranslation(-startPosition);

					double newX = characterSpacing[i].x * spacing * lastSizeValue;
					sceneItem.Matrix *= Matrix4X4.CreateTranslation(new Vector3(newX, 0, 0) + new Vector3(bedCenter));
					i += 1;
				}

				if (rebuildUnderline)
				{
					RebuildUnderline(group);
				}
			}
		}

		public void SetWordSize(IObject3D group, double newSize, bool rebuildUnderline = false)
		{
			// take out the last scale
			double oldSize = 1.0 / lastSizeValue;

			Vector3 bedCenter = new Vector3(ApplicationController.Instance.ActivePrinter.Bed.BedCenter);
			if (group.HasChildren())
			{
				foreach (var object3D in group.Children)
				{
					object3D.Matrix = PlatingHelper.ApplyAtPosition(object3D.Matrix, Matrix4X4.CreateScale(new Vector3(oldSize, oldSize, oldSize)), new Vector3(bedCenter));
					object3D.Matrix = PlatingHelper.ApplyAtPosition(object3D.Matrix, Matrix4X4.CreateScale(new Vector3(newSize, newSize, newSize)), new Vector3(bedCenter));
				}

				lastSizeValue = newSize;

				if (rebuildUnderline)
				{
					RebuildUnderline(group);
				}
			}
		}

		public void SetWordHeight(IObject3D group, double newHeight, bool rebuildUnderline = false)
		{
			if (group.HasChildren())
			{
				foreach (var sceneItem in group.Children)
				{
					// take out the last scale
					double oldHeight = lastHeightValue;
					sceneItem.Matrix *= Matrix4X4.CreateScale(new Vector3(1, 1, 1 / oldHeight));

					sceneItem.Matrix *= Matrix4X4.CreateScale(new Vector3(1, 1, newHeight));
				}

				lastHeightValue = newHeight;

				if (rebuildUnderline)
				{
					RebuildUnderline(group);
				}
			}
		}

		public void ResetSettings()
		{
			lastHeightValue = 1;
			lastSizeValue = 1;
		}
	}
}