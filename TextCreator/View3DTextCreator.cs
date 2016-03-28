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
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Plugins.TextCreator
{
	public class View3DTextCreator : View3DCreatorWidget
	{
		MHTextEditWidget textToAddWidget;
		private SolidSlider spacingScrollBar;
		private SolidSlider sizeScrollBar;
		private SolidSlider heightScrollBar;
		private bool firstDraw = true;
		private Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;

		private CheckBox createUnderline;

		private String wordText;

		TextGenerator textGenerator;

		protected override void AddToBottomToolbar(GuiWidget parentContainer)
		{
			textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter Text Here".Localize())
			{
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(5)
			};
			textToAddWidget.ActualTextEditWidget.EnterPressed += (s, e) => InsertTextNow(textToAddWidget.Text);
			parentContainer.AddChild(textToAddWidget);

			Button insertTextButton = textImageButtonFactory.Generate("Insert".Localize());
			insertTextButton.Click += (s, e) => InsertTextNow(textToAddWidget.Text);
			parentContainer.AddChild(insertTextButton);

			// jlewin - this looks like "Undo on esc", needs confirmation
			KeyDown += (s, e) =>
			{
				if (e != null && !e.Handled && e.KeyCode == Keys.Escape)
				{
					if (CurrentSelectInfo.DownOnPart)
					{
						CurrentSelectInfo.DownOnPart = false;
						Scene.SelectedItem.Matrix *= transformOnMouseDown;
						Invalidate();
					}
				}
			};
		}

		public View3DTextCreator(Vector3 viewerVolume, Vector2 bedCenter, MeshViewerWidget.BedShape bedShape)
			: base(viewerVolume, bedCenter, bedShape, "TextCreator_", partSelectVisible: true)
		{
			textGenerator = new TextGenerator();
		}

		private void ResetWordLayoutSettings()
		{
			spacingScrollBar.Value = 1;
			sizeScrollBar.Value = 1;
			heightScrollBar.Value = .25;

			textGenerator.ResetSettings();
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			if (meshViewerWidget.TrackballTumbleWidget.UnderMouseState == Agg.UI.UnderMouseState.FirstUnderMouse)
			{
				if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
				{
					viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;

					IntersectInfo info = new IntersectInfo();

					IObject3D hitObject = FindHitObject3D(mouseEvent.Position, ref info);
					if (hitObject != null)
					{
						CurrentSelectInfo.HitPlane = new PlaneShape(Vector3.UnitZ, CurrentSelectInfo.PlaneDownHitPos.z, null);
						CurrentSelectInfo.DownOnPart = true;

						transformOnMouseDown = hitObject.Matrix;

						if (hitObject != Scene.SelectedItem)
						{

						}
					}
				}
			}
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
			//DoCsgTest();
			base.OnDraw(graphics2D);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None && CurrentSelectInfo.DownOnPart)
			{
				Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, new Vector2(mouseEvent.X, mouseEvent.Y));
				Ray ray = meshViewerWidget.TrackballTumbleWidget.GetRayFromScreen(meshViewerWidgetScreenPosition);
				IntersectInfo info = CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
				if (info != null)
				{

					Vector3 delta = info.hitPosition - CurrentSelectInfo.PlaneDownHitPos;

					Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(-CurrentSelectInfo.LastMoveDelta));
					totalTransform *= Matrix4X4.CreateTranslation(new Vector3(delta));
					CurrentSelectInfo.LastMoveDelta = delta;

					Scene.SelectedItem.Matrix *= totalTransform;

					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		private async void InsertTextNow(string text)
		{
			if (text.Length > 0)
			{
				this.wordText = text;
				ResetWordLayoutSettings();
				processingProgressControl.ProcessType = "Inserting Text".Localize();
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				var newItem = await Task.Run(() =>
				{
					Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

					return textGenerator.CreateText(
						wordText,
						sizeScrollBar.Value,
						heightScrollBar.Value,
						spacingScrollBar.Value,
						createUnderline.Checked);
				});

				Scene.Modify(scene =>
				{
					scene.Clear();
					scene.Add(newItem);
				});

				Scene.SetSelectionToLastItem();

				UnlockEditControls();
				saveButton.Visible = true;
				saveAndExitButton.Visible = true;
			}

			meshViewerWidget.ResetView();
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None
				&& CurrentSelectInfo.DownOnPart
				&& CurrentSelectInfo.LastMoveDelta != Vector3.Zero)
			{
				saveButton.Visible = true;
				saveAndExitButton.Visible = true;
			}

			CurrentSelectInfo.DownOnPart = false;

			base.OnMouseUp(mouseEvent);
		}

		/*
		private void DeleteSelectedMesh()
		{
			// don't ever delete the last mesh
			if (MeshGroups.Count > 1)
			{
				int removeIndex = SelectedMeshGroupIndex;
				MeshGroups.RemoveAt(removeIndex);
				MeshGroupExtraData.RemoveAt(removeIndex);
				MeshGroupTransforms.RemoveAt(removeIndex);
				SelectedMeshGroupIndex = Math.Min(SelectedMeshGroupIndex, MeshGroups.Count - 1);
				saveButton.Visible = true;
				saveAndExitButton.Visible = true;
				Invalidate();
			}
		} */

		protected override void AddToWordEditMenu(GuiWidget wordOptionContainer)
		{
			spacingScrollBar = InsertUiForSlider(wordOptionContainer, "Spacing:".Localize(), .5, 1);
			spacingScrollBar.ValueChanged += (sender, e) =>
			{
				var textGroup = Scene.Children.FirstOrDefault();
				if (textGroup != null)
				{
					textGenerator.SetWordSpacing(textGroup, spacingScrollBar.Value, rebuildUnderline: true);
				}
			};

			sizeScrollBar = InsertUiForSlider(wordOptionContainer, "Size:".Localize(), .3, 2);
			sizeScrollBar.ValueChanged += (sender, e) =>
			{
				var textGroup = Scene.Children.FirstOrDefault();
				if (textGroup != null)
				{
					textGenerator.SetWordSize(textGroup, sizeScrollBar.Value, rebuildUnderline: true);
				}
			};

			heightScrollBar = InsertUiForSlider(wordOptionContainer, "Height:".Localize(), .05, 1);
			heightScrollBar.ValueChanged += (sender,e) =>
			{
				var textGroup = Scene.Children.FirstOrDefault();
				if (textGroup != null)
				{
					textGenerator.SetWordHeight(textGroup, heightScrollBar.Value, rebuildUnderline: true);
				}
			};
			createUnderline = new CheckBox(new CheckBoxViewText("Underline".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor))
			{
				Checked = true,
				Margin = new BorderDouble(10, 5),
				HAnchor = HAnchor.ParentLeft
			};
			createUnderline.CheckedStateChanged += CreateUnderline_CheckedStateChanged;
			wordOptionContainer.AddChild(createUnderline);
		}

		private void CreateUnderline_CheckedStateChanged(object sender, EventArgs e)
		{
			// The character data is now inject as a group and is the only item in the scene, thus it's easy to grab
			var currentGroup = Scene.Children.First();

			// Create a copy of the tree for the group
			IObject3D workItem = new Object3D()
			{
				Children = new List<IObject3D>(currentGroup.Children)
			};

			// Change the contents, adding or removing the underline
			textGenerator.EnableUnderline(workItem, createUnderline.Checked);

			// Modify the active scene graph, swapping in the new item
			Scene.Modify(scene =>
			{
				scene.Clear();
				scene.Add(workItem);
			});

			Scene.SetSelectionToLastItem();
		}

		private void AddLetterControls(FlowLayoutWidget buttonPanel)
		{
			textImageButtonFactory.FixedWidth = 44 * TextWidget.GlobalPointSizeScaleRatio;

			FlowLayoutWidget degreesContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			degreesContainer.HAnchor = HAnchor.ParentLeftRight;
			degreesContainer.Padding = new BorderDouble(5);

			GuiWidget horizontalSpacer = new GuiWidget();
			horizontalSpacer.HAnchor = HAnchor.ParentLeftRight;

			TextWidget degreesLabel = new TextWidget("Degrees:".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			degreesContainer.AddChild(degreesLabel);
			degreesContainer.AddChild(horizontalSpacer);

			MHNumberEdit degreesControl = new MHNumberEdit(45, pixelWidth: 40, allowNegatives: true, increment: 5, minValue: -360, maxValue: 360);
			degreesControl.VAnchor = Agg.UI.VAnchor.ParentTop;
			degreesContainer.AddChild(degreesControl);

			buttonPanel.AddChild(degreesContainer);

			FlowLayoutWidget rotateButtonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			rotateButtonContainer.HAnchor = HAnchor.ParentLeftRight;

			buttonPanel.AddChild(rotateButtonContainer);

			buttonPanel.AddChild(GenerateHorizontalRule());
			textImageButtonFactory.FixedWidth = 0;
		}

		public class TextGenerator
		{
			private double lastHeightValue = 1;
			private double lastSizeValue = 1;

			private TypeFace boldTypeFace;

			public TextGenerator()
			{
				boldTypeFace = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine("Fonts", "LiberationSans-Bold.svg")));
			}

			public IObject3D CreateText(string wordText, double wordSize, double wordHeight, double characterSpacing, bool createUnderline)
			{
				var tempScene = new Object3D { ItemType = Object3DTypes.Group };

				StyledTypeFace typeFace = new StyledTypeFace(boldTypeFace, 12);
				TypeFacePrinter printer = new TypeFacePrinter(wordText, typeFace);

				Vector2 size = printer.GetSize(wordText);
				double centerOffset = -size.x / 2;

				double ratioPerMeshGroup = 1.0 / wordText.Length;
				double currentRatioDone = 0;

				for (int i = 0; i < wordText.Length; i++)
				{
					string letter = wordText[i].ToString();
					TypeFacePrinter letterPrinter = new TypeFacePrinter(letter, typeFace);

					Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, 10 + (i % 2));
					if (textMesh.Faces.Count > 0)
					{
						var characterObject = new Object3D()
						{
							MeshGroup = new MeshGroup(textMesh),
							ItemType = Object3DTypes.Model
						};
						characterObject.ExtraData.Spacing.x = printer.GetOffsetLeftOfCharacterIndex(i).x + centerOffset;

						tempScene.Children.Add(characterObject);



						//public static void PlaceMeshGroupOnBed(List<MeshGroup> meshesGroupList, List<Matrix4X4> meshTransforms, int index)
						{
							AxisAlignedBoundingBox bounds = characterObject.GetAxisAlignedBoundingBox();
							Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;

							characterObject.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.z + bounds.ZSize / 2));
						}
						characterObject.CreateTraceables();

						currentRatioDone += ratioPerMeshGroup;
					}

					//processingProgressControl.PercentComplete = ((i + 1) * 95 / wordText.Length);
				}

				SetWordSpacing(tempScene, characterSpacing);
				SetWordSize(tempScene, wordSize);
				SetWordHeight(tempScene, wordHeight);

				if (createUnderline)
				{
					tempScene.Children.Add(CreateUnderline(tempScene));
				}

				// jlewin - restore progress
				//processingProgressControl.PercentComplete = 95;

				return tempScene;
			}

			private IObject3D CreateUnderline(IObject3D scene)
			{
				if (scene.HasItems)
				{
					AxisAlignedBoundingBox bounds = scene.GetAxisAlignedBoundingBox();
					
					double xSize = bounds.XSize;
					double ySize = lastSizeValue * 3;
					double zSize = bounds.ZSize / 3;

					var lineObject = new Object3D()
					{
						MeshGroup = new MeshGroup(PlatonicSolids.CreateCube(xSize, ySize, zSize)),
						ItemType = Object3DTypes.Model,
						Matrix = Matrix4X4.CreateTranslation((bounds.maxXYZ.x + bounds.minXYZ.x) / 2, bounds.minXYZ.y + ySize / 2 - ySize * 1 / 3, zSize / 2)
					};
					lineObject.CreateTraceables();

					return lineObject;
				}

				return null;
			}

			private bool hasUnderline;

			public void EnableUnderline(IObject3D scene, bool enableUnderline)
			{
				hasUnderline = enableUnderline;

				if (enableUnderline && scene.HasItems)
				{
					// we need to add the underline
					scene.Children.Add(CreateUnderline(scene));
				}
				else if (scene.HasItems)
				{
					// we need to remove the underline
					scene.Children.Remove(scene.Children.Last());
				}
			}


			public void RebuildUnderline(IObject3D scene)
			{
				if (hasUnderline && scene.HasItems)
				{
					// Remove the underline object
					scene.Children.Remove(scene.Children.Last());

					// we need to add the underline
					CreateUnderline(scene);
				}
			}

			public void SetWordSpacing(IObject3D scene, double spacing, bool rebuildUnderline = false)
			{
				if (scene.HasItems)
				{
					foreach (var sceneItem in scene.Children)
					{
						Vector3 startPosition = Vector3.Transform(Vector3.Zero, sceneItem.Matrix);

						sceneItem.Matrix *= Matrix4X4.CreateTranslation(-startPosition);

						double newX = sceneItem.ExtraData.Spacing.x * spacing * lastSizeValue;
						sceneItem.Matrix *= Matrix4X4.CreateTranslation(new Vector3(newX, 0, 0) + new Vector3(MeshViewerWidget.BedCenter));
					}

					if(rebuildUnderline)
					{
						RebuildUnderline(scene);
					}
				}
			}

			public void SetWordSize(IObject3D scene, double newSize, bool rebuildUnderline = false)
			{
				// take out the last scale
				double oldSize = 1.0 / lastSizeValue;

				Vector3 bedCenter = new Vector3(MeshViewerWidget.BedCenter);
				if (scene.HasItems)
				{
					foreach (var object3D in scene.Children)
					{
						object3D.Matrix = PlatingHelper.ApplyAtPosition(object3D.Matrix, Matrix4X4.CreateScale(new Vector3(oldSize, oldSize, oldSize)), new Vector3(bedCenter));
						object3D.Matrix = PlatingHelper.ApplyAtPosition(object3D.Matrix, Matrix4X4.CreateScale(new Vector3(newSize, newSize, newSize)), new Vector3(bedCenter));
					}

					lastSizeValue = newSize;

					if (rebuildUnderline)
					{
						RebuildUnderline(scene);
					}
				}
			}

			public void SetWordHeight(IObject3D scene, double newHeight, bool rebuildUnderline = false)
			{
				if (scene.HasItems)
				{
					foreach (var sceneItem in scene.Children)
					{
						// take out the last scale
						double oldHeight = lastHeightValue;
						sceneItem.Matrix *= Matrix4X4.CreateScale(new Vector3(1, 1, 1 / oldHeight));

						sceneItem.Matrix *= Matrix4X4.CreateScale(new Vector3(1, 1, newHeight));
					}

					lastHeightValue = newHeight;

					if (rebuildUnderline)
					{
						RebuildUnderline(scene);
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
}