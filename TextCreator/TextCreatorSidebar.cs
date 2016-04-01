﻿/*
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
	public class TextCreatorSidebar : SideBarPlugin
	{
		public event EventHandler TextInserted;

		private MHTextEditWidget textToAddWidget;
		private SolidSlider spacingScrollBar;
		private SolidSlider sizeScrollBar;
		private SolidSlider heightScrollBar;
		private CheckBox createUnderline;

		private GuiWidget insertContainer;

		private String wordText;

		private TextGenerator textGenerator;
		private View3DWidget view3DWidget;

		private IObject3D injectedItem = null;

		public TextCreatorSidebar()
		{
		}

		public bool IsSystemWindow { get; set; } = false;

		public override GuiWidget CreateSideBarTool(View3DWidget widget)
		{
			textGenerator = new TextGenerator();
			this.view3DWidget = widget;

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var tabButton = view3DWidget.ExpandMenuOptionFactory.GenerateCheckBoxButton("TEXT".Localize().ToUpper(), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
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

			spacingScrollBar = PartPreview3DWidget.InsertUiForSlider(tabContainer, "Spacing:".Localize(), .5, 1);
			spacingScrollBar.ValueChanged += (sender, e) =>
			{
				if (injectedItem != null)
				{
					textGenerator.SetWordSpacing(injectedItem, spacingScrollBar.Value, rebuildUnderline: true);
				}
			};

			sizeScrollBar = PartPreview3DWidget.InsertUiForSlider(tabContainer, "Size:".Localize(), .3, 2);
			sizeScrollBar.ValueChanged += (sender, e) =>
			{
				if (injectedItem != null)
				{
					textGenerator.SetWordSize(injectedItem, sizeScrollBar.Value, rebuildUnderline: true);
				}
			};

			heightScrollBar = PartPreview3DWidget.InsertUiForSlider(tabContainer, "Height:".Localize(), .05, 1);
			heightScrollBar.ValueChanged += (sender, e) =>
			{
				if (injectedItem != null)
				{
					textGenerator.SetWordHeight(injectedItem, heightScrollBar.Value, rebuildUnderline: true);
				}
			};

			createUnderline = new CheckBox(new CheckBoxViewText("Underline".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor))
			{
				Checked = true,
				Margin = new BorderDouble(10, 5),
				HAnchor = HAnchor.ParentLeft
			};
			createUnderline.CheckedStateChanged += CreateUnderline_CheckedStateChanged;
			tabContainer.AddChild(createUnderline);

			tabButton.Checked = this.IsSystemWindow;

			return mainContainer;
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

		private void ResetWordLayoutSettings()
		{
			spacingScrollBar.Value = 1;
			sizeScrollBar.Value = 1;
			heightScrollBar.Value = .25;

			textGenerator.ResetSettings();
		}

		private async void InsertTextNow(string text)
		{
			// Clear prior selection
			injectedItem = null;

			if (text.Length > 0)
			{
				this.wordText = text;
				ResetWordLayoutSettings();

				//view3DWidget.processingProgressControl.ProcessType = "Inserting Text".Localize();
				//view3DWidget.processingProgressControl.Visible = true;
				//view3DWidget.processingProgressControl.PercentComplete = 0;

				view3DWidget.LockEditControls();

				injectedItem = await Task.Run(() =>
				{
					Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

					return textGenerator.CreateText(
						wordText,
						sizeScrollBar.Value,
						heightScrollBar.Value,
						spacingScrollBar.Value,
						createUnderline.Checked);
				});

				PlatingHelper.MoveToOpenPosition(injectedItem, view3DWidget.Scene);

				view3DWidget.InsertNewItem(injectedItem);

				view3DWidget.UnlockEditControls();
			}

			TextInserted?.Invoke(null, null);
		}

		private void CreateUnderline_CheckedStateChanged(object sender, EventArgs e)
		{
			ModifyInjectedItem(workItem =>
			{
				// Change the contents, adding or removing the underline
				textGenerator.EnableUnderline(workItem, enableUnderline: createUnderline.Checked);
			});
		}

		private void ModifyInjectedItem(Action<IObject3D> modifier)
		{
			// Create a copy of the injected group
			IObject3D workItem = injectedItem.Clone();

			// Invoke the passed in action
			modifier(workItem);

			// Modify the scene graph, swapping in the modified item
			view3DWidget.Scene.ModifyChildren(children =>
			{
				children.Remove(injectedItem);
				children.Add(workItem);
			});

			// Update the injected item and the scene selection
			injectedItem = workItem;
			view3DWidget.Scene.Select(injectedItem);
		}

		internal void SetInitialFocus()
		{
			textToAddWidget.Focus();
		}

		public class TextGenerator
		{
			private double lastHeightValue = 1;
			private double lastSizeValue = 1;
			private bool hasUnderline;

			private TypeFace boldTypeFace;

			public TextGenerator()
			{
				boldTypeFace = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine("Fonts", "LiberationSans-Bold.svg")));
			}

			public IObject3D CreateText(string wordText, double wordSize, double wordHeight, double characterSpacing, bool createUnderline)
			{
				var groupItem = new Object3D { ItemType = Object3DTypes.Group };

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

						groupItem.Children.Add(characterObject);

						//public static void PlaceMeshGroupOnBed(List<MeshGroup> meshesGroupList, List<Matrix4X4> meshTransforms, int index)
						{
							AxisAlignedBoundingBox bounds = characterObject.GetAxisAlignedBoundingBox();
							Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;

							characterObject.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.z + bounds.ZSize / 2));
						}

						currentRatioDone += ratioPerMeshGroup;
					}

					//processingProgressControl.PercentComplete = ((i + 1) * 95 / wordText.Length);
				}

				SetWordSpacing(groupItem, characterSpacing);
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
				if (group.HasChildren)
				{
					AxisAlignedBoundingBox bounds = group.GetAxisAlignedBoundingBox();
					
					double xSize = bounds.XSize;
					double ySize = lastSizeValue * 3;
					double zSize = bounds.ZSize / 3;

					var lineObject = new Object3D()
					{
						MeshGroup = new MeshGroup(PlatonicSolids.CreateCube(xSize, ySize, zSize)),
						ItemType = Object3DTypes.Model,
						Matrix = Matrix4X4.CreateTranslation((bounds.maxXYZ.x + bounds.minXYZ.x) / 2, bounds.minXYZ.y + ySize / 2 - ySize * 1 / 3, zSize / 2)
					};

					return lineObject;
				}

				return null;
			}

			public void EnableUnderline(IObject3D group, bool enableUnderline)
			{
				hasUnderline = enableUnderline;

				if (enableUnderline && group.HasChildren)
				{
					// we need to add the underline
					group.Children.Add(CreateUnderline(group));
				}
				else if (group.HasChildren)
				{
					// we need to remove the underline
					group.Children.Remove(group.Children.Last());
				}
			}

			public void RebuildUnderline(IObject3D group)
			{
				if (hasUnderline && group.HasChildren)
				{
					// Remove the underline object
					group.Children.Remove(group.Children.Last());

					// we need to add the underline
					group.Children.Add(CreateUnderline(group));
				}
			}

			public void SetWordSpacing(IObject3D group, double spacing, bool rebuildUnderline = false)
			{
				if (group.HasChildren)
				{
					foreach (var sceneItem in group.Children)
					{
						Vector3 startPosition = Vector3.Transform(Vector3.Zero, sceneItem.Matrix);

						sceneItem.Matrix *= Matrix4X4.CreateTranslation(-startPosition);

						double newX = sceneItem.ExtraData.Spacing.x * spacing * lastSizeValue;
						sceneItem.Matrix *= Matrix4X4.CreateTranslation(new Vector3(newX, 0, 0) + new Vector3(MeshViewerWidget.BedCenter));
					}

					if(rebuildUnderline)
					{
						RebuildUnderline(group);
					}
				}
			}

			public void SetWordSize(IObject3D group, double newSize, bool rebuildUnderline = false)
			{
				// take out the last scale
				double oldSize = 1.0 / lastSizeValue;

				Vector3 bedCenter = new Vector3(MeshViewerWidget.BedCenter);
				if (group.HasChildren)
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
				if (group.HasChildren)
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
}