/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectedObjectPanel : FlowLayoutWidget, IContentStore
	{
		private IObject3D item = new Object3D();

		private FlowLayoutWidget scrollableContent;
		private TextWidget itemName;
		private ThemeConfig theme;
		private View3DWidget view3DWidget;
		private InteractiveScene scene;
		private PrinterConfig printer;
		private Dictionary<Type, HashSet<IObject3DEditor>> objectEditorsByType;
		private ObservableCollection<GuiWidget> materialButtons = new ObservableCollection<GuiWidget>();
		private SectionWidget editorSection;
		private TextButton editButton;
		private GuiWidget editorPanel;

		public SelectedObjectPanel(View3DWidget view3DWidget, InteractiveScene scene, ThemeConfig theme, PrinterConfig printer)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;
			this.Padding = 0; // new BorderDouble(8, 10);
							  //this.MinimumSize = new VectorMath.Vector2(220, 0);

			this.view3DWidget = view3DWidget;
			this.theme = theme;
			this.scene = scene;
			this.printer = printer;

			var firstPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = 10,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			this.AddChild(firstPanel);

			firstPanel.AddChild(itemName = new TextWidget("", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				EllipsisIfClipped = true,
				Margin = new BorderDouble(bottom: 10)
			});

			scrollableContent = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};

			var scrollable = new ScrollableWidget(true)
			{
				Name = "editorPanel",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};

			scrollable.AddChild(scrollableContent);
			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;

			this.AddChild(scrollable);

			// Add heading separator
			scrollableContent.AddChild(new HorizontalLine(25)
			{
				Margin = new BorderDouble(0)
			});


			var editorColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};

			var toolbar = new Toolbar()
			{
				Padding = theme.ToolbarPadding,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			editorColumn.AddChild(toolbar);

			// put in the button for making the behavior solid
			var solidBehaviorButton = new PopupButton(new TextButton("Color".Localize(), theme))
			{
				Name = "Solid Colors",
				AlignToRightEdge = true,
				PopupContent = new ColorSwatchSelector(scene)
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
				},
				Margin = theme.ButtonSpacing.Clone(left: 0),
				BackgroundColor = theme.MinimalShade
			};
			toolbar.AddChild(solidBehaviorButton);

			editButton = new TextButton("Edit".Localize(), theme)
			{
				BackgroundColor = theme.MinimalShade,
				Margin = theme.ButtonSpacing
			};
			editButton.Click += async (s, e) =>
			{
				var bed = new BedConfig();

				var partPreviewContent = this.Parents<PartPreviewContent>().FirstOrDefault();
				partPreviewContent.CreatePartTab(
					"New Part",
					bed,
					theme);

				await bed.LoadContent(
					new EditContext()
					{
						ContentStore = ApplicationController.Instance.Library.PlatingHistory,
						SourceItem = new InMemoryItem(this.item),
					});
			};
			toolbar.AddChild(editButton);

			// Add container used to host the current specialized editor for the selection
			editorColumn.AddChild(editorPanel = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = 6
			});

			editorSection = new SectionWidget("Editor", ActiveTheme.Instance.PrimaryTextColor, editorColumn);
			scrollableContent.AddChild(editorSection);

			// TODO: Implements
			//alignButton.Enabled = this.scene.HasSelection
			//	&& this.scene.SelectedItem is SelectionGroup
			//	&& this.scene.SelectedItem.Children.Count > 1;

			var alignSection = new SectionWidget("Align".Localize(), ActiveTheme.Instance.PrimaryTextColor, this.AddAlignControls(), expanded: false)
			{
				Name = "Align Panel",
			};
			scrollableContent.AddChild(alignSection);

			var mirrorSection = new SectionWidget("Mirror".Localize(), ActiveTheme.Instance.PrimaryTextColor, new MirrorControls(scene), expanded: false)
			{
				Name = "Mirror Panel",
			};
			scrollableContent.AddChild(mirrorSection);

			var scaleSection = new SectionWidget("Scale".Localize(), ActiveTheme.Instance.PrimaryTextColor, new ScaleControls(scene, ActiveTheme.Instance.PrimaryTextColor), expanded: false)
			{
				Name = "Scale Panel",
			};
			scrollableContent.AddChild(scaleSection);

			var materialsSection = new SectionWidget("Materials".Localize(), ActiveTheme.Instance.PrimaryTextColor, this.AddMaterialControls(), expanded: false)
			{
				Name = "Materials Panel",
			};
			scrollableContent.AddChild(materialsSection);

			// Enforce panel padding in sidebar
			foreach(var sectionWidget in scrollableContent.Children<SectionWidget>())
			{
				var contentPanel = sectionWidget.ContentPanel;
				contentPanel.Padding = 8;
			}

			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType = new Dictionary<Type, HashSet<IObject3DEditor>>();

			// TODO: Consider only loading once into a static
			var objectEditors = PluginFinder.CreateInstancesOf<IObject3DEditor>();
			foreach (IObject3DEditor editor in objectEditors)
			{
				foreach (Type type in editor.SupportedTypes())
				{
					if (!objectEditorsByType.TryGetValue(type, out mappedEditors))
					{
						mappedEditors = new HashSet<IObject3DEditor>();
						objectEditorsByType.Add(type, mappedEditors);
					}

					mappedEditors.Add(editor);
				}
			}
		}

		public void SetActiveItem(IObject3D selectedItem)
		{
			if (!scene.HasSelection)
			{
				this.Parent.Visible = false;
				return;
			}

			editButton.Enabled = (selectedItem.Children.Count > 0);

			this.itemName.Text = selectedItem.Name ?? selectedItem.GetType().Name;

			this.item = selectedItem;

			//this.editorPanel.RemoveAllChildren();

			var viewMode = printer?.ViewState.ViewMode;

			this.Parent.Visible = viewMode == null || viewMode == PartViewMode.Model;

			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType.TryGetValue(selectedItem.GetType(), out mappedEditors);

			if (mappedEditors == null)
			{
				foreach (var editor in objectEditorsByType)
				{
					if (selectedItem.GetType().IsSubclassOf(editor.Key))
					{
						mappedEditors = editor.Value;
						break;
					}
				}
			}

			// Add any editor mapped to Object3D to the list
			if (objectEditorsByType.TryGetValue(typeof(Object3D), out HashSet<IObject3DEditor> globalEditors))
			{
				foreach (var editor in globalEditors)
				{
					mappedEditors.Add(editor);
				}
			}

			if (mappedEditors == null)
			{
				editorPanel.CloseAllChildren();
				editorPanel.VAnchor = VAnchor.Absolute;
				editorPanel.VAnchor = VAnchor.Fit;
				editorPanel.Invalidate();
			}
			else
			{
				//var dropDownList = new DropDownList("", ActiveTheme.Instance.PrimaryTextColor, maxHeight: 300)
				//{
				//	HAnchor = HAnchor.Stretch
				//};

				//foreach (IObject3DEditor editor in mappedEditors)
				//{
				//	MenuItem menuItem = dropDownList.AddItem(editor.Name);
				//	menuItem.Selected += (s, e2) =>
				//	{
				//		ShowObjectEditor(editor);
				//	};
				//}

				//editorPanel.AddChild(dropDownList);

				// Select the active editor or fall back to the first if not found
				var firstEditor = (from editor in mappedEditors
											  let type = editor.GetType()
											  where type.Name == selectedItem.ActiveEditor
											  select editor).FirstOrDefault();

				// Fall back to default editor?
				if (firstEditor == null)
				{
					firstEditor = mappedEditors.First();
				}

				//int selectedIndex = 0;
				//for (int i = 0; i < dropDownList.MenuItems.Count; i++)
				//{
				//	if (dropDownList.MenuItems[i].Text == firstEditor.Name)
				//	{
				//		selectedIndex = i;
				//		break;
				//	}
				//}

				//dropDownList.SelectedIndex = selectedIndex;

				ShowObjectEditor(firstEditor);

				if (materialButtons?.Count > 0)
				{
					bool setSelection = false;
					// Set the material selector to have the correct material button selected
					for (int i = 0; i < materialButtons.Count; i++)
					{
						if (selectedItem.MaterialIndex == i)
						{
							((RadioButton)materialButtons[i]).Checked = true;
							setSelection = true;
						}
					}

					if (!setSelection)
					{
						((RadioButton)materialButtons[0]).Checked = true;
					}
				}
			}
		}

		private void ShowObjectEditor(IObject3DEditor editor)
		{
			editorPanel.CloseAllChildren();

			if (editor == null)
			{
				return;
			}

			var editorWidget = editor.Create(scene.SelectedItem, view3DWidget, theme);
			editorWidget.HAnchor = HAnchor.Stretch;
			editorWidget.VAnchor = VAnchor.Fit;

			editorPanel.AddChild(editorWidget);
		}

		public void Save(ILibraryItem item, IObject3D content)
		{
			this.item.Parent.Children.Modify(children =>
			{
				children.Remove(this.item);
				children.Add(content);
			});
		}

		private GuiWidget AddMaterialControls()
		{
			var widget = new IgnoredPopupWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = Color.White,
				Padding = new BorderDouble(0, 5, 5, 0)
			};

			FlowLayoutWidget buttonPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Fit,
			};
			widget.AddChild(buttonPanel);

			materialButtons.Clear();
			int extruderCount = 4;
			for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
			{
				FlowLayoutWidget colorSelectionContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
				{
					HAnchor = HAnchor.Fit,
					Padding = new BorderDouble(5)
				};
				buttonPanel.AddChild(colorSelectionContainer);

				string materialLabelText = string.Format("{0} {1}", "Material".Localize(), extruderIndex + 1);

				RadioButton materialSelection = new RadioButton(materialLabelText, textColor: Color.Black);
				materialButtons.Add(materialSelection);
				materialSelection.SiblingRadioButtonList = materialButtons;
				colorSelectionContainer.AddChild(materialSelection);
				colorSelectionContainer.AddChild(new HorizontalSpacer());
				int extruderIndexCanPassToClick = extruderIndex;
				materialSelection.Click += (sender, e) =>
				{
					if (scene.HasSelection)
					{
						scene.SelectedItem.MaterialIndex = extruderIndexCanPassToClick;
						scene.Invalidate();

						// "View 3D Overflow Menu" // the menu to click on
						// "Materials Option" // the item to highlight
						//HelpSystem.
					}
				};

				colorSelectionContainer.AddChild(new GuiWidget(16, 16)
				{
					BackgroundColor = MaterialRendering.Color(extruderIndex),
					Margin = new BorderDouble(5, 0, 0, 0)
				});
			}

			return widget;
		}

		private List<TransformData> GetTransforms(int axisIndex, AxisAlignment alignment)
		{
			var transformDatas = new List<TransformData>();
			var totalAABB = scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

			Vector3 firstSourceOrigin = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);

			// move the objects to the right place
			foreach (var child in scene.SelectedItem.Children)
			{
				var childAABB = child.GetAxisAlignedBoundingBox(scene.SelectedItem.Matrix);
				var offset = new Vector3();
				switch (alignment)
				{
					case AxisAlignment.Min:
						offset[axisIndex] = totalAABB.minXYZ[axisIndex] - childAABB.minXYZ[axisIndex];
						break;

					case AxisAlignment.Center:
						offset[axisIndex] = totalAABB.Center[axisIndex] - childAABB.Center[axisIndex];
						break;

					case AxisAlignment.Max:
						offset[axisIndex] = totalAABB.maxXYZ[axisIndex] - childAABB.maxXYZ[axisIndex];
						break;

					case AxisAlignment.SourceCoordinateSystem:
						{
							// move the object back to the origin
							offset = -Vector3.Transform(Vector3.Zero, child.Matrix);

							// figure out how to move it back to the start center
							if (firstSourceOrigin.X == double.MaxValue)
							{
								firstSourceOrigin = -offset;
							}

							offset += firstSourceOrigin;
						}
						break;
				}
				transformDatas.Add(new TransformData()
				{
					TransformedObject = child,
					RedoTransform = child.Matrix * Matrix4X4.CreateTranslation(offset),
					UndoTransform = child.Matrix,
				});
			}

			return transformDatas;
		}

		private void AddAlignDelegates(int axisIndex, AxisAlignment alignment, Button alignButton)
		{
			alignButton.Click += (sender, e) =>
			{
				if (scene.HasSelection)
				{
					var transformDatas = GetTransforms(axisIndex, alignment);
					scene.UndoBuffer.AddAndDo(new TransformCommand(transformDatas));

					//scene.SelectedItem.MaterialIndex = extruderIndexCanPassToClick;
					scene.Invalidate();
				}
			};

			alignButton.MouseEnter += (s2, e2) =>
			{
				if (scene.HasSelection)
				{
					// make a preview of the new positions
					var transformDatas = GetTransforms(axisIndex, alignment);
					scene.Children.Modify((list) =>
					{
						foreach (var transform in transformDatas)
						{
							var copy = transform.TransformedObject.Clone();
							copy.Matrix = transform.RedoTransform;
							copy.Color = new Color(Color.Gray, 126);
							list.Add(copy);
						}
					});
				}
			};

			alignButton.MouseLeave += (s3, e3) =>
			{
				if (scene.HasSelection)
				{
					// clear the preview of the new positions
					scene.Children.Modify((list) =>
					{
						for (int i = list.Count - 1; i >= 0; i--)
						{
							if (list[i].Color.Alpha0To255 == 126)
							{
								list.RemoveAt(i);
							}
						}
					});
				}
			};
		}

		internal enum AxisAlignment { Min, Center, Max, SourceCoordinateSystem };

		private GuiWidget CreateAlignButton(int axisIndex, AxisAlignment alignment, string lable)
		{
			var smallMarginButtonFactory = theme.MenuButtonFactory;
			var alignButton = smallMarginButtonFactory.Generate(lable);
			alignButton.Margin = new BorderDouble(3, 0);

			AddAlignDelegates(axisIndex, alignment, alignButton);

			return alignButton;
		}

		private GuiWidget AddAlignControls()
		{
			var widget = new IgnoredPopupWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = Color.White,
				Padding = new BorderDouble(5, 5, 5, 0)
			};

			FlowLayoutWidget buttonPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Fit,
			};
			widget.AddChild(buttonPanel);

			string[] axisNames = new string[] { "X", "Y", "Z" };
			for (int axisIndex = 0; axisIndex < 3; axisIndex++)
			{
				FlowLayoutWidget alignButtons = new FlowLayoutWidget(FlowDirection.LeftToRight)
				{
					HAnchor = HAnchor.Fit,
					Padding = new BorderDouble(5)
				};
				buttonPanel.AddChild(alignButtons);

				alignButtons.AddChild(new TextWidget(axisNames[axisIndex])
				{
					VAnchor = VAnchor.Center,
					Margin = new BorderDouble(0, 0, 3, 0)
				});

				alignButtons.AddChild(CreateAlignButton(axisIndex, AxisAlignment.Min, "Min"));
				alignButtons.AddChild(new HorizontalSpacer());
				alignButtons.AddChild(CreateAlignButton(axisIndex, AxisAlignment.Center, "Center"));
				alignButtons.AddChild(new HorizontalSpacer());
				alignButtons.AddChild(CreateAlignButton(axisIndex, AxisAlignment.Max, "Max"));
				alignButtons.AddChild(new HorizontalSpacer());
			}

			var dualExtrusionAlignButton = theme.MenuButtonFactory.Generate("Align for Dual Extrusion".Localize());
			dualExtrusionAlignButton.Margin = new BorderDouble(21, 0);
			dualExtrusionAlignButton.HAnchor = HAnchor.Left;
			buttonPanel.AddChild(dualExtrusionAlignButton);

			AddAlignDelegates(0, AxisAlignment.SourceCoordinateSystem, dualExtrusionAlignButton);

			return widget;
		}

		public class InMemoryItem : ILibraryContentItem
		{
			private IObject3D existingItem;

			public InMemoryItem(IObject3D existingItem)
			{
				this.existingItem = existingItem;
			}

			public string ID => existingItem.ID;

			public string Name => existingItem.Name;

			public bool IsProtected => !existingItem.Persistable;

			public bool IsVisible => existingItem.Visible;

			public string ContentType => "stl";

			public string Category => "";

			public Task<IObject3D> GetContent(Action<double, string> reportProgress)
			{
				return Task.FromResult(existingItem);
			}

			public void SetContent(IObject3D item)
			{
			}
		}
	}
}