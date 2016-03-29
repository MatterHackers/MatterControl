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
		private bool firstDraw = true;
		BrailleCreatorSidebar brailleCreator;

		public View3DBrailleBuilder(Vector3 viewerVolume, Vector2 bedCenter, MeshViewerWidget.BedShape bedShape)
			: base(viewerVolume, bedCenter, bedShape, "BrailleBuilder_", partSelectVisible: false)
		{
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (firstDraw)
			{
#if !__ANDROID__
				brailleCreator.SetInitialFocus();
#endif
				//textToAddWidget.Text = "Test Text";
				firstDraw = false;
			}

			base.OnDraw(graphics2D);
		}

		protected override void AddToSidebar(GuiWidget sidePanel)
		{
			brailleCreator = new BrailleCreatorSidebar();
			brailleCreator.IsSystemWindow = true;
			brailleCreator.TextInserted += (s, e) =>
			{
				saveButton.Visible = true;
				saveAndExitButton.Visible = true;
			};

			var mainContainer = brailleCreator.CreateSideBarTool(this);
			mainContainer.HAnchor = HAnchor.Max_FitToChildren_ParentWidth;
			sidePanel.AddChild(mainContainer);
		}

		protected override void AddToBottomToolbar(GuiWidget parentContainer)
		{
			brailleCreator.AddToBottomToolbar(parentContainer);
		}
	}

	public class BrailleCreatorSidebar : SideBarPlugin
	{
		public event EventHandler TextInserted;

		private MHTextEditWidget textToAddWidget;
		private SolidSlider sizeScrollBar;
		private SolidSlider heightScrollBar;
		private CheckBox includeText;
		private CheckBox useGrade2;

		private GuiWidget insertContainer;

		private String wordText;

		private BrailleGenerator brailleGenerator;
		private PartPreview3DWidget view3DWidget;

		private IObject3D injectedItem = null;

		public BrailleCreatorSidebar()
		{
		}

		public bool IsSystemWindow { get; set; } = false;

		public override GuiWidget CreateSideBarTool(PartPreview3DWidget widget)
		{
			brailleGenerator = new BrailleGenerator();
			this.view3DWidget = widget;

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var tabButton = view3DWidget.ExpandMenuOptionFactory.GenerateCheckBoxButton("BRAILLE".Localize().ToUpper(), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
			tabButton.Margin = new BorderDouble(bottom: 2);
			mainContainer.AddChild(tabButton);

			FlowLayoutWidget tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Visible = false
			};
			mainContainer.AddChild(tabContainer);

			tabButton.CheckedStateChanged += (sender, e) =>
			{
				tabContainer.Visible = tabButton.Checked;

				if (!IsSystemWindow)
				{
					if (insertContainer == null)
					{
						AddToBottomToolbar(view3DWidget.doEdittingButtonsContainer);
					}

					insertContainer.Visible = tabButton.Checked;
				}
			};

			sizeScrollBar = View3DWidget.InsertUiForSlider(tabContainer, "Size:".Localize(), .3, 2);
			{
				sizeScrollBar.ValueChanged += (sender, e) =>
				{
					brailleGenerator.SetWordSize(view3DWidget.Scene, sizeScrollBar.Value);

					//SetWordSpacing(MeshGroups, MeshGroupTransforms, MeshGroupExtraData);
					RebuildBase();
				};
			}

			heightScrollBar = View3DWidget.InsertUiForSlider(tabContainer, "Height:".Localize(), .05, 1);
			{
				heightScrollBar.ValueChanged += (sender, e) =>
				{
					brailleGenerator.SetWordHeight(view3DWidget.Scene, heightScrollBar.Value);
				};
			}

			// put in the user alpha check box
			{
				includeText = new CheckBox(new CheckBoxViewText("Include Text".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor))
				{
					ToolTipText = "Show normal text above the braille".Localize(),
					Checked = false,
					Margin = new BorderDouble(10, 5),
					HAnchor = HAnchor.ParentLeft
				};

				tabContainer.AddChild(includeText);
				includeText.CheckedStateChanged += (s, e) => InsertTextNow(this.wordText);
			}

			// put in the user alpha check box
			{
				useGrade2 = new CheckBox(new CheckBoxViewText("Use Grade 2".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor));
				useGrade2.ToolTipText = "Experimental support for Braille grade 2 (contractions)".Localize();
				useGrade2.Checked = false;
				useGrade2.Margin = new BorderDouble(10, 5);
				useGrade2.HAnchor = HAnchor.ParentLeft;
				tabContainer.AddChild(useGrade2);
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

				tabContainer.AddChild(moreAboutBrailleLink);
			}

			tabButton.Checked = this.IsSystemWindow;

			return mainContainer;
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
				//printItemName = wordText;

				// ResetWordLayoutSettings

				// TODO: jlewin - setting the slider values causes the onchange events to fire causing object to get rebuilt. What's the reason for chaning the slider values on insert?
				//sizeScrollBar.Value = 1;
				//heightScrollBar.Value = 1;

				brailleGenerator.ResetSettings();
				
				//processingProgressControl.ProcessType = "Inserting Text".Localize();
				//processingProgressControl.Visible = true;
				//processingProgressControl.PercentComplete = 0;
				//LockEditControls();

				injectedItem = await Task.Run(() =>
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

				view3DWidget.Scene.ModifyChildren(children =>
				{
					if(IsSystemWindow)
					{
						children.Clear();
					}

					children.Add(injectedItem);
				});

				//RebuildBase();

				//UnlockEditControls();
				//saveButton.Visible = true;
				//saveAndExitButton.Visible = true;

				view3DWidget.Scene.SelectLastChild();
			}
		}

		public void AddToBottomToolbar(GuiWidget parentContainer)
		{
			insertContainer = new FlowLayoutWidget(FlowDirection.LeftToRight, HAnchor.FitToChildren);

			textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter Text Here".Localize())
			{
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(5)
			};
			textToAddWidget.ActualTextEditWidget.EnterPressed += (s, e) => InsertTextNow(textToAddWidget.Text);
			insertContainer.AddChild(textToAddWidget);

			Button insertTextButton = view3DWidget.textImageButtonFactory.Generate("Insert".Localize());
			insertTextButton.Click += (s, e) => InsertTextNow(textToAddWidget.Text);
			insertContainer.AddChild(insertTextButton);

			parentContainer.AddChild(insertContainer);
		}

		private void RebuildBase()
		{
			if (view3DWidget.Scene.HasChildren && injectedItem != null)
			{
				// Remove the old base and create and add a new one
				view3DWidget.Scene.ModifyChildren(children =>
				{
					children.Remove(injectedItem);
					injectedItem = brailleGenerator.CreateBaseplate(injectedItem);
					children.Add(injectedItem);
				});
			}
		}

		internal void SetInitialFocus()
		{
			textToAddWidget.Focus();
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

			private void AddCharacterMeshes(IObject3D group, string currentText, TypeFacePrinter printer)
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

						group.Children.Add(characterObject);
					}

					// TODO: jlewin - we need a reporter instance
					//processingProgressControl.PercentComplete = ((i + 1) * 95 / currentText.Length);
				}
			}

			private void SetWordPositions(IObject3D group)
			{
				if (group.HasChildren)
				{
					foreach (var object3D in group.Children)
					{
						Vector3 startPosition = Vector3.Transform(Vector3.Zero, object3D.Matrix);
						object3D.Matrix *= Matrix4X4.CreateTranslation(-startPosition);

						double newX = object3D.ExtraData.Spacing.x * lastSizeValue;
						double newY = object3D.ExtraData.Spacing.y * lastSizeValue;

						object3D.Matrix *= Matrix4X4.CreateTranslation(new Vector3(newX, newY, startPosition.z));
					}
				}
			}

			public void SetWordHeight(IObject3D group, double newHeight)
			{
				if (group.HasChildren)
				{
					AxisAlignedBoundingBox baseBounds = group.Children.Last().GetAxisAlignedBoundingBox();

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
				if (group.HasChildren)
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

			private void CenterTextOnScreen(IObject3D group)
			{
				// Center on bed
				Vector3 bedCenter = new Vector3(MeshViewerWidget.BedCenter);
				Vector3 centerOffset = group.GetAxisAlignedBoundingBox().Center - bedCenter;

				group.Matrix *= Matrix4X4.CreateTranslation(new Vector3(-centerOffset.x, -centerOffset.y, 0));
			}

			private bool CharacterHasMesh(TypeFacePrinter letterPrinter, string letter)
			{
				return letterPrinter.LocalBounds.Width > 0
					&& letter != " "
					&& letter != "\n";
			}

			public IObject3D CreateBaseplate(IObject3D group)
			{
				if (group.HasChildren)
				{
					AxisAlignedBoundingBox bounds = group.GetAxisAlignedBoundingBox();

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