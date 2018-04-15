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
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class IObject3DComponentAttribute: Attribute
	{
	}

	public interface IObject3DComponent
	{
	}

	public class SelectedObjectPanel : FlowLayoutWidget, IContentStore
	{
		private IObject3D item = new Object3D();

		private ThemeConfig theme;
		private View3DWidget view3DWidget;
		private InteractiveScene scene;
		private PrinterConfig printer;
		private SectionWidget editorSection;
		private TextButton editButton;

		private GuiWidget editorPanel;
		private InlineTitleEdit inlineTitleEdit;

		public SelectedObjectPanel(View3DWidget view3DWidget, InteractiveScene scene, ThemeConfig theme, PrinterConfig printer)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;
			this.Padding = 0; // new BorderDouble(8, 10);
							  //this.MinimumSize = new Vector2(220, 0);

			this.view3DWidget = view3DWidget;
			this.theme = theme;
			this.scene = scene;
			this.printer = printer;

			this.AddChild(inlineTitleEdit = new InlineTitleEdit("", theme, "Object Name")
			{
				Border = new BorderDouble(bottom: 1),
				BorderColor = theme.GetBorderColor(50)
			});
			inlineTitleEdit.TitleChanged += (s, e) =>
			{
				if (item != null)
				{
					item.Name = inlineTitleEdit.Text;
				}
			};

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
			var icon = AggContext.StaticData.LoadIcon("permanent.png", 16, 16, theme.InvertIcons).SetPreMultiply();
			var bakeButton = new IconButton(icon, theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Make operation permanent".Localize()
			};
			bakeButton.Click += (s, e) =>
			{
				scene.SelectedItem = null;
				this.item.MakePermanent();
			};
			scene.SelectionChanged += (s, e) => bakeButton.Enabled = scene.SelectedItem?.CanBake == true;
			toolbar.AddChild(bakeButton);

			// put in a remove button
			var removeButton = new IconButton(ThemeConfig.RestoreNormal, theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Remove operation from parts".Localize()
			};
			removeButton.Click += (s, e) =>
			{
				scene.SelectedItem = null;
				this.item.Remove();
			};
			scene.SelectionChanged += (s, e) => removeButton.Enabled = scene.SelectedItem?.CanRemove == true;
			toolbar.AddChild(removeButton);

			// Add container used to host the current specialized editor for the selection
			editorColumn.AddChild(editorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(top: 10)
			});

			editorSection = new SectionWidget("Editor", editorColumn, theme, serializationKey: UserSettingsKey.EditorPanelExpanded, defaultExpansion: true);
			this.ContentPanel.AddChild(editorSection);

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
				sectionWidget.ContentPanel.Padding = new BorderDouble(10, 10, 10, 0);
				sectionWidget.ExpandableWhenDisabled = true;
				sectionWidget.Enabled = false;
			}
		}

		public GuiWidget ContentPanel { get; set; }

		private static Type componentAttribute = typeof(IObject3DComponentAttribute);
		private static Type componentType = typeof(IObject3DComponent);
		private static Type iobject3DType = typeof(IObject3D);

		public void SetActiveItem(IObject3D selectedItem)
		{
			if (!scene.HasSelection)
			{
				this.Parent.Visible = false;
				return;
			}

			var selectedItemType = selectedItem.GetType();

			editButton.Enabled = (selectedItem.Children.Count > 0);

			inlineTitleEdit.Text = selectedItem.Name ?? selectedItemType.Name;

			this.item = selectedItem;

			var viewMode = printer?.ViewState.ViewMode;

			HashSet<IObject3DEditor> mappedEditors = ApplicationController.Instance.GetEditorsForType(selectedItemType);

			var activeEditors = new List<(IObject3DEditor, IObject3D, string)>();

			// If item is IObject3DComponent
			if (componentType.IsAssignableFrom(selectedItemType))
			{
				// Get all public, instance properties where property type is IObject3D
				var members = from item in selectedItemType.GetProperties(PublicPropertyEditor.OwnedPropertiesOnly)
								let propertyType = item.PropertyType
								where iobject3DType.IsAssignableFrom(propertyType)
								select new
								{
									Type = propertyType,
									Value = item.GetValue(selectedItem, null) as IObject3D,
									DisplayName = EditableProperty.GetDisplayName(item)
								};

				// Shown known editors for any matching properties
				foreach (var member in members)
				{
					if (ApplicationController.Instance.GetEditorsForType(member.Type)?.FirstOrDefault() is IObject3DEditor editor)
					{
						activeEditors.Add((editor, member.Value, member.DisplayName));
					}
				}
			}
			else
			{
				// Get all public, instance properties where property type is marked with IObject3DComponentAttribute
				var members = from item in selectedItemType.GetProperties(PublicPropertyEditor.OwnedPropertiesOnly)
							  where Attribute.IsDefined(item, componentAttribute)
							  select new
							  {
								  Type = item.PropertyType,
								  Value = item.GetValue(selectedItem, null) as IObject3D,
								  DisplayName = EditableProperty.GetDisplayName(item)
							  };

				// Shown known editors for any matching properties
				foreach (var member in members.Where(m => m.Value != null))
				{
					if (ApplicationController.Instance.GetEditorsForType(member.Type)?.FirstOrDefault() is IObject3DEditor editor)
					{
						activeEditors.Add((editor, member.Value, member.DisplayName));
					}
				}
			}

			if (mappedEditors?.Any() == true)
			{
				// Use first filtered or fall back to unfiltered first
				activeEditors.Add((mappedEditors.First(), selectedItem, null));
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

				var editorWidget = scopeItem.editor.Create(selectedItem, view3DWidget, theme);
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
								nodeOperation.Operation(selectedItem, scene).ConfigureAwait(false);
							};

							buttons.Add(button);
						}
					}
				}

				if (buttons.Any())
				{
					var toolbar = new Toolbar()
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