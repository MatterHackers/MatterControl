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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JsonPath;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Library;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using static JsonPath.JsonPathContext.ReflectionValueSystem;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectedObjectPanel : FlowLayoutWidget, IContentStore
	{
		private IObject3D item = new Object3D();

		private ThemeConfig theme;
		private BedConfig sceneContext;
		private View3DWidget view3DWidget;
		private SectionWidget editorSectionWidget;

		private GuiWidget editorPanel;

		public SelectedObjectPanel(View3DWidget view3DWidget, BedConfig sceneContext, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;
			this.Padding = 0;
			this.view3DWidget = view3DWidget;
			this.theme = theme;
			this.sceneContext = sceneContext;

			var toolbar = new LeftClipFlowLayoutWidget()
			{
				BackgroundColor = theme.TabBodyBackground,
				Padding = theme.ToolbarPadding,
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};

			var scene = sceneContext.Scene;

			var itemColorButton = new ItemColorButton(scene, theme)
			{
				Width = 30,
				Height = 30,
			};
			toolbar.AddChild(itemColorButton);

			var itemMaterialButton = new ItemMaterialButton(scene, theme)
			{
				Width = 30,
				Height = 30,
			};
			toolbar.AddChild(itemMaterialButton);

			// put in a make permanent button
			var icon = AggContext.StaticData.LoadIcon("noun_766157.png", 16, 16, theme.InvertIcons).SetPreMultiply();
			var applyButton = new IconButton(icon, theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Merge".Localize(),
				Enabled = scene.SelectedItem?.CanApply == true
			};
			applyButton.Click += (s, e) =>
			{
				this.item.Apply(view3DWidget.Scene.UndoBuffer);
				scene.SelectedItem = null;
			};
			toolbar.AddChild(applyButton);

			// put in a remove button
			var removeButton = new IconButton(AggContext.StaticData.LoadIcon("remove.png", 16, 16, theme.InvertIcons), theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Delete".Localize(),
				Enabled = scene.SelectedItem != null
			};
			removeButton.Click += (s, e) =>
			{
				var rootSelection = scene.SelectedItemRoot;

				var removeItem = item;
				removeItem.Remove(view3DWidget.Scene.UndoBuffer);

				scene.SelectedItem = null;

				// Only restore the root selection if it wasn't the item removed
				if (removeItem != rootSelection)
				{
					scene.SelectedItem = rootSelection;
				}
			};
			toolbar.AddChild(removeButton);

			var overflowButton = new OverflowBar.OverflowMenuButton(theme)
			{
				Enabled = scene.SelectedItem != null,
				PopupBorderColor = ApplicationController.Instance.MenuTheme.GetBorderColor(120)
			};
			overflowButton.DynamicPopupContent = () =>
			{
				return ApplicationController.Instance.GetActionMenuForSceneItem(item, sceneContext.Scene, false);
			};
			toolbar.AddChild(overflowButton);

			editorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Name = "editorPanel",
				Padding = new BorderDouble(right: theme.DefaultContainerPadding + 1)
			};

			// Wrap editorPanel with scrollable container
			var scrollableWidget = new ScrollableWidget(true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			scrollableWidget.AddChild(editorPanel);
			scrollableWidget.ScrollArea.HAnchor = HAnchor.Stretch;

			editorSectionWidget = new SectionWidget("Editor", scrollableWidget, theme, toolbar, serializationKey: UserSettingsKey.EditorPanelExpanded, expandingContent: false, defaultExpansion: true, setContentVAnchor: false)
			{
				VAnchor = VAnchor.Stretch
			};
			this.AddChild(editorSectionWidget);

			this.ContentPanel = editorPanel;
			editorPanel.Padding = new BorderDouble(theme.DefaultContainerPadding, 0);

			scene.SelectionChanged += (s, e) =>
			{
				if (editorPanel.Children.FirstOrDefault()?.DescendantsAndSelf<SectionWidget>().FirstOrDefault() is SectionWidget firstSectionWidget)
				{
					firstSectionWidget.Margin = firstSectionWidget.Margin.Clone(top: 0);
				}

				var selectedItem = scene.SelectedItem;
				if (selectedItem != null)
				{
					itemColorButton.Color = (selectedItem.Color == Color.Transparent) ? theme.MinimalHighlight : selectedItem.Color;
					itemMaterialButton.Color = MaterialRendering.Color(selectedItem.MaterialIndex, theme.MinimalHighlight);
				}

				itemColorButton.Enabled = selectedItem != null;
				itemMaterialButton.Enabled = selectedItem != null;
				applyButton.Enabled = selectedItem?.CanApply == true;
				removeButton.Enabled = selectedItem != null;
				overflowButton.Enabled = selectedItem != null;
			};
		}

		/// <summary>
		/// Behavior from removed Edit button - keeping around for reuse as an advanced feature in the future
		/// </summary>
		/// <returns></returns>
		private async Task EditChildInIsolatedContext()
		{
			var bed = new BedConfig();

			var partPreviewContent = this.Parents<PartPreviewContent>().FirstOrDefault();
			partPreviewContent.CreatePartTab(
				"New Part",
				bed,
				theme);

			var clonedItem = this.item.Clone();

			// Edit in Identity transform
			clonedItem.Matrix = Matrix4X4.Identity;

			await bed.LoadContent(
				new EditContext()
				{
					ContentStore = new DynamicContentStore((libraryItem, object3D) =>
					{
						var replacement = object3D.Clone();

						this.item.Parent.Children.Modify(list =>
						{
							list.Remove(item);

								// Restore matrix of item being replaced
								replacement.Matrix = item.Matrix;

							list.Add(replacement);

							item = replacement;
						});

						sceneContext.Scene.SelectedItem = replacement;
					}),
					SourceItem = new InMemoryLibraryItem(clonedItem),
				});
		}

		public GuiWidget ContentPanel { get; set; }

		private JsonPathContext pathResolver = new JsonPathContext();

		public void SetActiveItem(IObject3D selectedItem)
		{
			this.item = selectedItem;
			editorPanel.CloseAllChildren();

			// Allow caller to clean up with passing null for selectedItem
			if (item == null)
			{
				return;
			}

			var selectedItemType = selectedItem.GetType();

			editorSectionWidget.Text = selectedItem.Name ?? selectedItemType.Name;

			HashSet<IObject3DEditor> mappedEditors = ApplicationController.Instance.GetEditorsForType(selectedItemType);

			var undoBuffer = sceneContext.Scene.UndoBuffer;

			bool allowOperations = true;

			if (selectedItem is ComponentObject3D componentObject
				&& componentObject.Finalized)
			{
				allowOperations = false;

				foreach (var selector in componentObject.SurfacedEditors)
				{
					// Get the named property via reflection
					// Selector example:            '$.Children<CylinderObject3D>'
					var match = pathResolver.Select(componentObject, selector).ToList();

					//// TODO: Create editor row for each property
					//// - Use the type of the property to find a matching editor (ideally all datatype -> editor functionality would resolve consistently)
					//// - Add editor row for each

					foreach (var instance in match)
					{
						if (instance is IObject3D object3D)
						{
							if (ApplicationController.Instance.GetEditorsForType(object3D.GetType())?.FirstOrDefault() is IObject3DEditor editor)
							{
								ShowObjectEditor((editor, object3D, object3D.Name), selectedItem, allowOperations: allowOperations);
							}
						}
						else if (JsonPath.JsonPathContext.ReflectionValueSystem.LastMemberValue is ReflectionTarget reflectionTarget)
						{
							var context = new PPEContext();

							if (reflectionTarget.Source is IObject3D editedChild)
							{
								context.item = editedChild;
							}
							else
							{
								context.item = item;
							}

							var editableProperty = new EditableProperty(reflectionTarget.PropertyInfo, reflectionTarget.Source);

							var editor = PublicPropertyEditor.CreatePropertyEditor(editableProperty, undoBuffer, context, theme);
							if (editor != null)
							{
								editorPanel.AddChild(editor);
							}
						}
					}
				}

				// Enforce panel padding
				foreach (var sectionWidget in editorPanel.Descendants<SectionWidget>())
				{
					sectionWidget.Margin = new BorderDouble(0, theme.DefaultContainerPadding / 2);
				}
			}
			else
			{
				if (ApplicationController.Instance.GetEditorsForType(item.GetType())?.FirstOrDefault() is IObject3DEditor editor)
				{
					ShowObjectEditor((editor, item, item.Name), selectedItem, allowOperations: allowOperations);
				}
			}
		}

		private class OperationButton :TextButton
		{
			private NodeOperation graphOperation;
			private IObject3D sceneItem;

			public OperationButton(NodeOperation graphOperation, IObject3D sceneItem, ThemeConfig theme)
				: base(graphOperation.Title, theme)
			{
				this.graphOperation = graphOperation;
				this.sceneItem = sceneItem;
			}

			public void EnsureAvailablity()
			{
				this.Enabled = graphOperation.IsEnabled?.Invoke(sceneItem) != false;
			}
		}

		private void ShowObjectEditor((IObject3DEditor editor, IObject3D item, string displayName) scopeItem, IObject3D rootSelection, bool allowOperations = true)
		{
			var selectedItem = scopeItem.item;
			var selectedItemType = selectedItem.GetType();

			var editorWidget = scopeItem.editor.Create(selectedItem, theme);
			editorWidget.HAnchor = HAnchor.Stretch;
			editorWidget.VAnchor = VAnchor.Fit;

			if (scopeItem.item != rootSelection
				&& scopeItem.editor is PublicPropertyEditor)
			{
				editorWidget.Padding = new BorderDouble(10, 10, 10, 0);

				// EditOutline section
				var sectionWidget = new SectionWidget(
						scopeItem.displayName ?? "Unknown",
						editorWidget,
						theme);

				theme.ApplyBoxStyle(sectionWidget, margin: 0);

				editorWidget = sectionWidget;
			}
			else
			{
				editorWidget.Padding = 0;
			}

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
	}
}