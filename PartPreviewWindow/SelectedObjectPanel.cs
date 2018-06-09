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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	[HideUpdateButtonAttribute]
	public class SelectedObjectPanel : FlowLayoutWidget, IContentStore
	{
		private IObject3D item = new Object3D();

		private ThemeConfig theme;
		private BedConfig sceneContext;
		private View3DWidget view3DWidget;
		private SectionWidget editorSectionWidget;
		private TextButton editButton;

		private GuiWidget editorPanel;
		private InlineTitleEdit inlineTitleEdit;
		private BottomResizeContainer editorResizeContainer;

		public SelectedObjectPanel(View3DWidget view3DWidget, BedConfig sceneContext, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;
			this.Padding = 0;
			this.view3DWidget = view3DWidget;
			this.theme = theme;
			this.sceneContext = sceneContext;

			this.ContentPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
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

			scrollable.AddChild(this.ContentPanel);
			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;

			this.AddChild(scrollable);

			editorResizeContainer = new BottomResizeContainer(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Height = sceneContext.ViewState.SelectedObjectEditorHeight
			};

			var toolbar = new Toolbar(theme)
			{
				Padding = theme.ToolbarPadding,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			editorResizeContainer.AddChild(toolbar);

			editorResizeContainer.Resized += (s, e) =>
			{
				sceneContext.ViewState.SelectedObjectEditorHeight = editorResizeContainer.Height;
			};

			var scene = sceneContext.Scene;

			editButton = new TextButton("Edit".Localize(), theme)
			{
				BackgroundColor = theme.MinimalShade,
				Margin = theme.ButtonSpacing
			};
			scene.SelectionChanged += (s, e) => editButton.Enabled = scene.SelectedItem?.CanEdit == true;
			editButton.Click += async (s, e) =>
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

							scene.SelectedItem = replacement;
						}),
						SourceItem = new InMemoryLibraryItem(clonedItem),
					});
			};
			toolbar.AddChild(editButton);

			// put in a make permanent button
			var icon = AggContext.StaticData.LoadIcon("fa-check_16.png", 16, 16, theme.InvertIcons).SetPreMultiply();
			var applyButton = new IconButton(icon, theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Apply operation and make permanent".Localize()
			};
			applyButton.Click += (s, e) =>
			{
				scene.SelectedItem = null;
				this.item.Apply(view3DWidget.Scene.UndoBuffer);
			};
			scene.SelectionChanged += (s, e) => applyButton.Enabled = scene.SelectedItem?.CanApply == true;
			toolbar.AddChild(applyButton);

			// put in a remove button
			var removeButton = new IconButton(AggContext.StaticData.LoadIcon("close.png", 16, 16, theme.InvertIcons), theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Remove operation from parts".Localize()
			};
			removeButton.Click += (s, e) =>
			{
				scene.SelectedItem = null;
				this.item.Remove(view3DWidget.Scene.UndoBuffer);
			};
			scene.SelectionChanged += (s, e) => removeButton.Enabled = scene.SelectedItem?.CanRemove == true;
			toolbar.AddChild(removeButton);

			// Add container used to host the current specialized editor for the selection
			var scrollableEditor = new ScrollableWidget(true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			scrollableEditor.AddChild(editorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(top: 10)
			});
			scrollableEditor.ScrollArea.HAnchor = HAnchor.Stretch;

			editorResizeContainer.AddChild(scrollableEditor);

			// A wrapping container to fix resize quirks - GuiWidget with H:Stretch V:Fit that can be hidden and shown and allow the ResizeContainer can keep it's size
			var editorResizeWrapper = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Name = "editorRootContainer"
			};
			editorResizeWrapper.AddChild(editorResizeContainer);

			inlineTitleEdit = new InlineTitleEdit("", theme, "Object Name");
			inlineTitleEdit.TitleChanged += (s, e) =>
			{
				if (item != null)
				{
					item.Name = inlineTitleEdit.Text;
				}
			};

			editorSectionWidget = new SectionWidget("Editor", editorResizeWrapper, theme, serializationKey: UserSettingsKey.EditorPanelExpanded, defaultExpansion: true)
			{
				VAnchor = VAnchor.Fit,
			};

			// TODO: Replace hackery with practical solution
			if (editorSectionWidget.Children.FirstOrDefault() is ExpandCheckboxButton checkbox)
			{
				checkbox.ReplaceChild(checkbox.Children[1], inlineTitleEdit);
			}

			this.ContentPanel.AddChild(editorSectionWidget);

			var colorSection = new SectionWidget(
				"Color".Localize(),
				new ColorSwatchSelector(scene, theme, buttonSize: 16, buttonSpacing: new BorderDouble(1, 1, 0, 0))
				{
					Margin = new BorderDouble(left: 10)
				},
				theme,
				serializationKey: UserSettingsKey.ColorPanelExpanded)
			{
				Name = "Color Panel",
			};
			this.ContentPanel.AddChild(colorSection);

			var mirrorSection = new SectionWidget("Mirror".Localize(), new MirrorControls(scene, theme), theme, serializationKey: UserSettingsKey.MirrorPanelExpanded)
			{
				Name = "Mirror Panel",
			};
			this.ContentPanel.AddChild(mirrorSection);

			var scaleSection = new SectionWidget("Scale".Localize(), new ScaleControls(scene, theme), theme, serializationKey: UserSettingsKey.ScalePanelExpanded)
			{
				Name = "Scale Panel",
			};
			this.ContentPanel.AddChild(scaleSection);

			var materialsSection = new SectionWidget("Materials".Localize(), new MaterialControls(scene, theme), theme, serializationKey: UserSettingsKey.MaterialsPanelExpanded)
			{
				Name = "Materials Panel",
			};
			this.ContentPanel.AddChild(materialsSection);

			// Enforce panel padding in sidebar
			foreach(var sectionWidget in this.ContentPanel.Children<SectionWidget>())
			{
				// Special case for editorRootContainer due to ResizeContainer
				if (sectionWidget.ContentPanel == editorResizeWrapper)
				{
					editorResizeContainer.Padding = new BorderDouble(10, 10, 10, 0);
				}
				else
				{
					sectionWidget.ContentPanel.Padding = new BorderDouble(10, 10, 10, 0);
					sectionWidget.ExpandableWhenDisabled = true;
					sectionWidget.Enabled = false;
				}
			}
		}

		public GuiWidget ContentPanel { get; set; }

		public void SetActiveItem(IObject3D selectedItem)
		{

			var selectedItemType = selectedItem.GetType();

			editButton.Enabled = (selectedItem.Children.Count > 0);

			inlineTitleEdit.Text = selectedItem.Name ?? selectedItemType.Name;

			this.item = selectedItem;

			HashSet<IObject3DEditor> mappedEditors = ApplicationController.Instance.GetEditorsForType(selectedItemType);

			var activeEditors = new List<(IObject3DEditor, IObject3D, string)>();

			foreach (var child in new[] { selectedItem })
			{
				if (ApplicationController.Instance.GetEditorsForType(child.GetType())?.FirstOrDefault() is IObject3DEditor editor)
				{
					activeEditors.Add((editor, child, child.Name));
				}
			}

			ShowObjectEditor(activeEditors, selectedItem);
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
				this.Enabled = graphOperation.IsEnabled(sceneItem);
			}
		}

		private void ShowObjectEditor(IEnumerable<(IObject3DEditor editor, IObject3D item, string displayName)> scope, IObject3D rootSelection)
		{
			editorPanel.CloseAllChildren();

			if (scope == null)
			{
				return;
			}

			foreach (var scopeItem in scope)
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

				var buttons = new List<OperationButton>();

				foreach (var nodeOperation in ApplicationController.Instance.Graph.Operations)
				{
					foreach (var type in nodeOperation.MappedTypes)
					{
						if (type.IsAssignableFrom(selectedItemType)
							&& (nodeOperation.IsVisible == null || nodeOperation.IsVisible(selectedItem)))
						{
							var button = new OperationButton(nodeOperation, selectedItem, theme)
							{
								BackgroundColor = theme.MinimalShade,
								Margin = theme.ButtonSpacing
							};
							button.EnsureAvailablity();
							button.Click += (s, e) =>
							{
								nodeOperation.Operation(selectedItem, sceneContext.Scene).ConfigureAwait(false);
							};

							buttons.Add(button);
						}
					}
				}

				if (buttons.Any())
				{
					var toolbar = new Toolbar(theme)
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit,
						Padding = theme.ToolbarPadding,
						Margin = new BorderDouble(0, 8)
					};
					editorPanel.AddChild(toolbar);

					foreach (var button in buttons)
					{
						toolbar.AddChild(button);
					}

					// TODO: Fix likely leak
					selectedItem.Invalidated += (s, e) =>
					{
						foreach (var button in toolbar.ActionArea.Children.OfType<OperationButton>())
						{
							button.EnsureAvailablity();
						}
					};
				}
				else
				{
					// If the button toolbar isn't added, ensure panel has bottom margin
					editorWidget.Margin = editorWidget.Margin.Clone(bottom: 15);
				}
			}
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