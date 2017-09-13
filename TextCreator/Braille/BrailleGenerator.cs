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
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Plugins.BrailleBuilder
{
	public class BrailleGenerator
	{
		private double lastHeightValue = 1;
		private double lastSizeValue = 1;

		private TypeFace brailTypeFace;
		private TypeFace boldTypeFace;

		private const double unscaledBaseHeight = 7;
		private const double unscaledLetterHeight = 3;

		private Vector2[] characterSpacing;

		public BrailleGenerator()
		{
			boldTypeFace = TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(Path.Combine("Fonts", "LiberationMono.svg")));
			brailTypeFace = TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(Path.Combine("Fonts", "Braille.svg")));
		}

		public IObject3D CreateText(string brailleText, double wordSize, double wordHeight, string wordText = null)
		{
			var group = new TextObject
			{
				ItemType = Object3DTypes.Group,
				Text = wordText,
				ActiveEditor = "BrailleEditor"
			};

			TypeFacePrinter brailPrinter = new TypeFacePrinter(brailleText, new StyledTypeFace(brailTypeFace, 12));

			StyledTypeFace boldStyled = new StyledTypeFace(boldTypeFace, 12);

			AddCharacterMeshes(group, brailleText, brailPrinter);
			Vector2 brailSize = brailPrinter.GetSize();

			for (int i = 0; i < brailleText.Length; i++)
			{
				characterSpacing[i] += new Vector2(0, boldStyled.CapHeightInPixels * 1.5);
			}

			IObject3D basePlate = CreateBaseplate(group);
			group.Children.Add(basePlate);

			SetCharacterPositions(group);
			SetWordSize(group, wordSize);
			SetWordHeight(group, wordHeight);

			// Remove the temporary baseplate added above and required by SetPositions/SetSize
			group.Children.Remove(basePlate);

			// Add the actual baseplate that can be correctly sized to its siblings bounds
			basePlate = CreateBaseplate(group);
			group.Children.Add(basePlate);

			return group;
		}

		private void AddCharacterMeshes(IObject3D group, string currentText, TypeFacePrinter printer)
		{
			StyledTypeFace typeFace = printer.TypeFaceStyle;

			characterSpacing = new Vector2[currentText.Length];

			for (int i = 0; i < currentText.Length; i++)
			{
				string letter = currentText[i].ToString();
				TypeFacePrinter letterPrinter = new TypeFacePrinter(letter, typeFace);

				if (CharacterHasMesh(letterPrinter, letter))
				{
#if true
					Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, unscaledLetterHeight / 2);
#else
					Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, unscaledLetterHeight / 2);
					// this is the code to make rounded tops
					// convert the letterPrinter to clipper polygons
					List<List<IntPoint>> insetPoly = VertexSourceToPolygon.CreatePolygons(letterPrinter);
					// inset them
					ClipperOffset clipper = new ClipperOffset();
					clipper.AddPaths(insetPoly, JoinType.jtMiter, EndType.etClosedPolygon);
					List<List<IntPoint>> solution = new List<List<IntPoint>>();
					clipper.Execute(solution, 5.0);
					// convert them back into a vertex source
					// merge both the inset and original vertex sources together
					// convert the new vertex source into a mesh (triangulate them)
					// offset the inner loop in z
					// create the polygons from the inner loop to a center point so that there is the rest of an approximation of the bubble
					// make the mesh for the bottom 
					// add the top and bottom together
					// done
#endif
					var characterObject = new Object3D()
					{
						Mesh = textMesh,
						ItemType = Object3DTypes.Model
					};

					characterSpacing[i] = printer.GetOffsetLeftOfCharacterIndex(i);
					characterObject.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, unscaledLetterHeight / 2));

					group.Children.Add(characterObject);
				}

				//processingProgressControl.PercentComplete = ((i + 1) * 95 / currentText.Length);
			}
		}

		private void SetCharacterPositions(IObject3D group)
		{
			if (group.HasChildren())
			{
				for (int i = 0; i < characterSpacing.Length; i++)
				{
					IObject3D child = group.Children[i];

					Vector3 startPosition = Vector3.Transform(Vector3.Zero, child.Matrix);

					var spacing = characterSpacing[i];

					double newX = spacing.x * lastSizeValue;
					double newY = spacing.y * lastSizeValue;

					child.Matrix *= Matrix4X4.CreateTranslation(new Vector3(newX, newY, startPosition.z));
				}
			}
		}

		public void SetWordHeight(IObject3D group, double newHeight)
		{
			if (group.HasChildren())
			{
				AxisAlignedBoundingBox baseBounds = group.Children.Last().GetAxisAlignedBoundingBox(Matrix4X4.Identity);

				// Skip the base item
				foreach (var sceneItem in group.Children.Take(group.Children.Count - 1))
				{
					Vector3 startPosition = Vector3.Transform(Vector3.Zero, sceneItem.Matrix);

					// take out the last scale
					double oldHeight = 1.0 / lastHeightValue;

					// move the part to keep it in the same relative position
					sceneItem.Matrix *= Matrix4X4.CreateScale(new Vector3(1, 1, oldHeight));
					sceneItem.Matrix *= Matrix4X4.CreateScale(new Vector3(1, 1, newHeight));

					sceneItem.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, baseBounds.ZSize - startPosition.z));
				}

				lastHeightValue = newHeight;
			}
		}

		public void SetWordSize(IObject3D group, double newSize)
		{
			if (group.HasChildren())
			{
				foreach (var object3D in group.Children)
				{
					Vector3 startPositionRelCenter = Vector3.Transform(Vector3.Zero, object3D.Matrix);

					// take out the last scale
					double oldSize = 1.0 / lastSizeValue;
					Vector3 unscaledStartPositionRelCenter = startPositionRelCenter * oldSize;

					Vector3 endPositionRelCenter = unscaledStartPositionRelCenter * newSize;

					Vector3 deltaPosition = endPositionRelCenter - startPositionRelCenter;

					// move the part to keep it in the same relative position
					object3D.Matrix *= Matrix4X4.CreateScale(new Vector3(oldSize, oldSize, oldSize));
					object3D.Matrix *= Matrix4X4.CreateScale(new Vector3(newSize, newSize, newSize));
					object3D.Matrix *= Matrix4X4.CreateTranslation(deltaPosition);
				}

				lastSizeValue = newSize;
			}
		}
		private bool CharacterHasMesh(TypeFacePrinter letterPrinter, string letter)
		{
			return letterPrinter.LocalBounds.Width > 0
				&& letter != " "
				&& letter != "\n";
		}

		public IObject3D CreateBaseplate(IObject3D group)
		{
			if (group.HasChildren())
			{
				AxisAlignedBoundingBox bounds = group.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

				double roundingScale = 20;
				RectangleDouble baseRect = new RectangleDouble(bounds.minXYZ.x, bounds.minXYZ.y, bounds.maxXYZ.x, bounds.maxXYZ.y);
				baseRect.Inflate(2);
				baseRect *= roundingScale;

				RoundedRect baseRoundedRect = new RoundedRect(baseRect, 1 * roundingScale);
				Mesh baseMeshResult = VertexSourceToMesh.Extrude(baseRoundedRect, unscaledBaseHeight / 2 * roundingScale * lastHeightValue);
				baseMeshResult.Transform(Matrix4X4.CreateScale(1 / roundingScale));

				var basePlateObject = new BraileBasePlate()
				{
					Mesh = baseMeshResult,
					ItemType = Object3DTypes.Model
				};

				basePlateObject.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, 0));

				return basePlateObject;
			}

			return null;
		}

		public void ResetSettings()
		{
			lastHeightValue = 1;
			lastSizeValue = 1;
		}
	}

	public class BraileBasePlate : Object3D
	{
	}

}