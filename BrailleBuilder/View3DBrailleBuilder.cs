/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Plugins.BrailleBuilder
{
	public class View3DBrailleBuilder : View3DCreatorWidget
	{
		// Unique to View3DBrailleBuilder {{
		private MHTextEditWidget textToAddWidget;
		private SolidSlider sizeScrollBar;
		private SolidSlider heightScrollBar;
		private CheckBox includeText;
		private CheckBox useGrade2;

		private bool firstDraw = true;

		BrailleGenerator brailleGenerator;
		private String wordText;
		// Unique to View3DBrailleBuilder }}

		public View3DBrailleBuilder(Vector3 viewerVolume, Vector2 bedCenter, MeshViewerWidget.BedShape bedShape)
			: base(viewerVolume, bedCenter, bedShape, "BrailleBuilder_", partSelectVisible: false)
		{
			brailleGenerator = new BrailleGenerator();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (firstDraw)
			{
#if !__ANDROID__
				textToAddWidget.Focus();
#endif
				//textToAddWidget.Text = "Test Text";
				firstDraw = false;
			}

			base.OnDraw(graphics2D);
		}

		private async void InsertTextNow(string brailleText)
		{
			if (brailleText.Length > 0)
			{
				this.wordText = brailleText;
				if (useGrade2.Checked)
				{
					brailleText = BrailleGrade2.ConvertString(brailleText);
				}

				// Update the name to use when generating the print item wrapper
				printItemName = wordText;

				// ResetWordLayoutSettings
				sizeScrollBar.Value = 1;
				heightScrollBar.Value = 1;

				brailleGenerator.ResetSettings();
				
				processingProgressControl.ProcessType = "Inserting Text".Localize();
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				var newItem = await Task.Run(() =>
				{
					Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

					return brailleGenerator.CreateText(
						brailleText, 
						sizeScrollBar.Value, 
						heightScrollBar.Value, 
						includeText.Checked, 
						wordText);

					//processingProgressControl.PercentComplete = 95;
					//InsertTextDoWork(text, this.word));//replace with this.word when not testing conversions
				});

				Scene.Modify(scene =>
				{
					scene.Clear();
					scene.Add(newItem);
				});

				Scene.SetSelectionToLastItem();
				//RebuildBase();

				UnlockEditControls();
				saveButton.Visible = true;
				saveAndExitButton.Visible = true;

				Scene.SetSelectionToLastItem();
			}
		}

		protected override void AddToBottomToolbar(GuiWidget parentContainer)
		{
			textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter Text Here".Localize())
			{
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(5)
			};

			parentContainer.AddChild(textToAddWidget);

			textToAddWidget.ActualTextEditWidget.EnterPressed += (object sender, KeyEventArgs keyEvent) =>
			{
				InsertTextNow(textToAddWidget.Text);
			};

			Button insertTextButton = textImageButtonFactory.Generate("Insert".Localize());
			parentContainer.AddChild(insertTextButton);
			insertTextButton.Click += (sender, e) =>
			{
				InsertTextNow(textToAddWidget.Text);
			};
		}

		protected override void AddToWordEditMenu(GuiWidget wordOptionContainer)
		{
			sizeScrollBar = InsertUiForSlider(wordOptionContainer, "Size:".Localize(), .3, 2);
			{
				sizeScrollBar.ValueChanged += (sender, e) =>
				{
					brailleGenerator.SetWordSize(Scene, sizeScrollBar.Value);

					//SetWordSpacing(MeshGroups, MeshGroupTransforms, MeshGroupExtraData);
					RebuildBase();
				};
			}

			heightScrollBar = InsertUiForSlider(wordOptionContainer, "Height:".Localize(), .05, 1);
			{
				heightScrollBar.ValueChanged += (sender, e) =>
				{
					brailleGenerator.SetWordHeight(Scene, heightScrollBar.Value);
					RebuildBase();
				};
			}

			// put in the user alpha check box
			{
				includeText = new CheckBox(new CheckBoxViewText("Include Text".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor));
				includeText.ToolTipText = "Show normal text above the braille".Localize();
				includeText.Checked = false;
				includeText.Margin = new BorderDouble(10, 5);
				includeText.HAnchor = HAnchor.ParentLeft;
				wordOptionContainer.AddChild(includeText);
				includeText.CheckedStateChanged += (sender, e) =>
				{
					InsertTextNow(this.wordText);
				};
			}

			// put in the user alpha check box
			{
				useGrade2 = new CheckBox(new CheckBoxViewText("Use Grade 2".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor));
				useGrade2.ToolTipText = "Experimental support for Braille grade 2 (contractions)".Localize();
				useGrade2.Checked = false;
				useGrade2.Margin = new BorderDouble(10, 5);
				useGrade2.HAnchor = HAnchor.ParentLeft;
				wordOptionContainer.AddChild(useGrade2);
				useGrade2.CheckedStateChanged += (sender, e) =>
				{
					InsertTextNow(this.wordText);
				};
			}

			// put in a link to the wikipedia article
			{
				LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
				linkButtonFactory.fontSize = 10;
				linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;

				Button moreAboutBrailleLink = linkButtonFactory.Generate("About Braille".Localize());
				moreAboutBrailleLink.Margin = new BorderDouble(10, 5);
				moreAboutBrailleLink.HAnchor = HAnchor.ParentLeft;
				moreAboutBrailleLink.Click += (sender, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						MatterControlApplication.Instance.LaunchBrowser("https://en.wikipedia.org/wiki/Braille");
					});
				};

				wordOptionContainer.AddChild(moreAboutBrailleLink);
			}
		}

		private void RebuildBase()
		{
			if (Scene.HasItems)
			{
				Scene.Modify(scene =>
				{
					// Remove the old base and create and add a new one
					scene.Remove(scene.Last());
					scene.Add(brailleGenerator.CreateBaseplate(Scene));
				});
			}
		}

		public class BrailleGenerator
		{
			private double lastHeightValue = 1;
			private double lastSizeValue = 1;

			private TypeFace brailTypeFace;
			private TypeFace boldTypeFace;

			private const double unscaledBaseHeight = 7;
			private const double unscaledLetterHeight = 3;

			public BrailleGenerator()
			{
				boldTypeFace = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine("Fonts", "LiberationMono.svg")));
				brailTypeFace = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine("Fonts", "Braille.svg")));
			}

			public IObject3D CreateText(string brailleText, double wordSize, double wordHeight, bool includeText = false, string wordText = null)
			{
				var tempScene = new Object3D { ItemType = Object3DTypes.Group };

				TypeFacePrinter brailPrinter = new TypeFacePrinter(brailleText, new StyledTypeFace(brailTypeFace, 12));

				StyledTypeFace boldStyled = new StyledTypeFace(boldTypeFace, 12);

				if (includeText)
				{
					TypeFacePrinter normalPrinter = new TypeFacePrinter(wordText, boldStyled);
					Vector2 normalSize = normalPrinter.GetSize();
					AddCharacterMeshes(tempScene, wordText, normalPrinter);
				}

				AddCharacterMeshes(tempScene, brailleText, brailPrinter);
				Vector2 brailSize = brailPrinter.GetSize();

				foreach (var object3D in tempScene.Children)
				{
					object3D.ExtraData.Spacing += new Vector2(0, boldStyled.CapHeightInPixels * 1.5);
				}

				tempScene.Children.Add(CreateBaseplate(tempScene));

				SetWordPositions(tempScene);
				SetWordSize(tempScene, wordSize);
				SetWordHeight(tempScene, wordHeight);

				var basePlateObject = CreateBaseplate(tempScene);
				tempScene.Children.Add(basePlateObject);

				CenterTextOnScreen(tempScene);

				return tempScene;
			}

			private void AddCharacterMeshes(Object3D scene, string currentText, TypeFacePrinter printer)
			{
				StyledTypeFace typeFace = printer.TypeFaceStyle;

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
							MeshGroup = new MeshGroup(textMesh),
							ItemType = Object3DTypes.Model
						};

						characterObject.ExtraData.Spacing = printer.GetOffsetLeftOfCharacterIndex(i);
						characterObject.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, unscaledLetterHeight / 2));
						characterObject.CreateTraceables();

						scene.Children.Add(characterObject);
					}

					// TODO: jlewin - we need a reporter instance
					//processingProgressControl.PercentComplete = ((i + 1) * 95 / currentText.Length);
				}
			}

			private void SetWordPositions(IObject3D scene)
			{
				if (scene.HasItems)
				{
					foreach (var object3D in scene.Children)
					{
						Vector3 startPosition = Vector3.Transform(Vector3.Zero, object3D.Matrix);
						object3D.Matrix *= Matrix4X4.CreateTranslation(-startPosition);

						double newX = object3D.ExtraData.Spacing.x * lastSizeValue;
						double newY = object3D.ExtraData.Spacing.y * lastSizeValue;

						object3D.Matrix *= Matrix4X4.CreateTranslation(new Vector3(newX, newY, startPosition.z));
					}
				}
			}

			// jlewin - source from heightScrollBar.Value
			public void SetWordHeight(IObject3D scene, double newHeight)
			{
				if (scene.HasItems)
				{
					AxisAlignedBoundingBox baseBounds = scene.Children.Last().GetAxisAlignedBoundingBox();

					// Skip the base item
					foreach (var sceneItem in scene.Children.Take(scene.Children.Count - 1))
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

			// jlewin - source from sizeScrollbar.Value
			public void SetWordSize(IObject3D scene, double newSize)
			{
				if (scene.HasItems)
				{
					foreach (var object3D in scene.Children)
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

			private void CenterTextOnScreen(Object3D object3D)
			{
				// Center on bed
				Vector3 bedCenter = new Vector3(MeshViewerWidget.BedCenter);
				Vector3 centerOffset = object3D.GetAxisAlignedBoundingBox().Center - bedCenter;

				object3D.Matrix *= Matrix4X4.CreateTranslation(new Vector3(-centerOffset.x, -centerOffset.y, 0));

				/*
				// center in y
				if (object3D.Children.Count > 0)
				{
					AxisAlignedBoundingBox bounds = object3D.GetAxisAlignedBoundingBox();

					Vector3 bedCenter = new Vector3(MeshViewerWidget.BedCenter);
					Vector3 centerOffset = bounds.Center - bedCenter;

					// TODO: Would it make sense to group these by default, to move as a unit and still allow the user to easily ungroup when desired?
					foreach (var child in object3D.Children)
					{
						child.Matrix *= Matrix4X4.CreateTranslation(new Vector3(-centerOffset.x, -centerOffset.y, 0));
					}
				} */
			}

			private bool CharacterHasMesh(TypeFacePrinter letterPrinter, string letter)
			{
				return letterPrinter.LocalBounds.Width > 0
					&& letter != " "
					&& letter != "\n";
			}

			public IObject3D CreateBaseplate(IObject3D scene)
			{
				if (scene.HasItems)
				{
					AxisAlignedBoundingBox bounds = scene.GetAxisAlignedBoundingBox();

					double roundingScale = 20;
					RectangleDouble baseRect = new RectangleDouble(bounds.minXYZ.x, bounds.minXYZ.y, bounds.maxXYZ.x, bounds.maxXYZ.y);
					baseRect.Inflate(2);
					baseRect *= roundingScale;

					RoundedRect baseRoundedRect = new RoundedRect(baseRect, 1 * roundingScale);
					Mesh baseMeshResult = VertexSourceToMesh.Extrude(baseRoundedRect, unscaledBaseHeight / 2 * roundingScale * lastHeightValue);
					baseMeshResult.Transform(Matrix4X4.CreateScale(1 / roundingScale));

					var basePlateObject = new Object3D()
					{
						MeshGroup = new MeshGroup(baseMeshResult),
						ItemType = Object3DTypes.Model
					};

					basePlateObject.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, 0));
					basePlateObject.CreateTraceables();

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
	}
}