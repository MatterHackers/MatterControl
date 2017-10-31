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
		private Vector2[] characterSpacing;

		public TextGenerator()
		{
		}

		public IObject3D CreateText(string wordText, double spacing)
		{
			var groupItem = new TextObject()
			{
				ItemType = Object3DTypes.Group,
				Text = wordText,
				Spacing = spacing,
				ActiveEditor = "TextEditor"
			};

			StyledTypeFace typeFace = new StyledTypeFace(LiberationSansBoldFont.Instance, 12);
			TypeFacePrinter printer = new TypeFacePrinter(wordText, typeFace);

			Vector2 size = printer.GetSize(wordText);
			double centerOffset = -size.X / 2;

			double ratioPerMeshGroup = 1.0 / wordText.Length;
			double currentRatioDone = 0;

			characterSpacing = new Vector2[wordText.Length];

			int meshIndex = 0;
			for (int i = 0; i < wordText.Length; i++)
			{
				string letter = wordText[i].ToString();
				TypeFacePrinter letterPrinter = new TypeFacePrinter(letter, typeFace);

				Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, 5);
				if (textMesh.Faces.Count > 0)
				{
					var characterObject = new Object3D()
					{
						Mesh = textMesh,
					};

					characterSpacing[meshIndex++] = new Vector2( printer.GetOffsetLeftOfCharacterIndex(i).X + centerOffset, 0);

					groupItem.Children.Add(characterObject);

					AxisAlignedBoundingBox bounds = characterObject.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
					Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;

					characterObject.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.Z + bounds.ZSize / 2));

					currentRatioDone += ratioPerMeshGroup;
				}

				//processingProgressControl.PercentComplete = ((i + 1) * 95 / wordText.Length);
			}

			SetWordSpacing(groupItem, spacing);

			// jlewin - restore progress
			//processingProgressControl.PercentComplete = 95;

			return groupItem;
		}

		private void SetWordSpacing(IObject3D group, double spacing)
		{
			if (group.HasChildren())
			{
				var bedCenter = ApplicationController.Instance.ActivePrinter.Bed.BedCenter;

				int i = 0;
				foreach (var sceneItem in group.Children)
				{
					Vector3 startPosition = Vector3.Transform(Vector3.Zero, sceneItem.Matrix);

					sceneItem.Matrix *= Matrix4X4.CreateTranslation(-startPosition);

					double newX = characterSpacing[i].X * spacing;
					sceneItem.Matrix *= Matrix4X4.CreateTranslation(new Vector3(newX, 0, 0) + new Vector3(bedCenter));
					i += 1;
				}
			}
		}
	}
}