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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using System.Reflection;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public interface IObject3DComponent
	{
	}

	public class SelectedObjectPanel : FlowLayoutWidget, IContentStore
	{
		private IObject3D item = new Object3D();

		private FlowLayoutWidget scrollableContent;
		private ThemeConfig theme;
		private View3DWidget view3DWidget;
		private InteractiveScene scene;
		private PrinterConfig printer;
		private Dictionary<Type, HashSet<IObject3DEditor>> objectEditorsByType;
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
							  //this.MinimumSize = new VectorMath.Vector2(220, 0);

			this.view3DWidget = view3DWidget;
			this.theme = theme;
			this.scene = scene;
			this.printer = printer;

			this.AddChild(inlineTitleEdit = new InlineTitleEdit("", theme, "Object Name"));
			inlineTitleEdit.TitleChanged += (s, e) =>
			{
				if (item != null)
				{
					item.Name = inlineTitleEdit.Text;
				}
			};

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
						SourceItem = new InMemoryItem(clonedItem),
					});
			};
			toolbar.AddChild(editButton);

			// put in a bake button
			var icon = AggContext.StaticData.LoadIcon("bake.png", 16, 16).SetPreMultiply();
			var bakeButton = new IconButton(icon, theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Bake operation into parts".Localize()
			};
			bakeButton.Click += (s, e) =>
			{
				scene.SelectedItem = null;
				this.item.Bake();
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

			editorSection = new SectionWidget("Editor", editorColumn, theme);
			scrollableContent.AddChild(editorSection);

			// TODO: Implements
			//alignButton.Enabled = this.scene.HasSelection
			//	&& this.scene.SelectedItem is SelectionGroup
			//	&& this.scene.SelectedItem.Children.Count > 1;

			var alignSection = new SectionWidget("Align".Localize(), new AlignControls(scene, theme), theme, expanded: false)
			{
				Name = "Align Panel",
			};
			scrollableContent.AddChild(alignSection);

			var mirrorSection = new SectionWidget("Mirror".Localize(), new MirrorControls(scene, theme), theme, expanded: false)
			{
				Name = "Mirror Panel",
			};
			scrollableContent.AddChild(mirrorSection);

			var scaleSection = new SectionWidget("Scale".Localize(), new ScaleControls(scene, theme), theme, expanded: false)
			{
				Name = "Scale Panel",
			};
			scrollableContent.AddChild(scaleSection);

			var materialsSection = new SectionWidget("Materials".Localize(), new MaterialControls(scene, theme), theme, expanded: false)
			{
				Name = "Materials Panel",
			};
			scrollableContent.AddChild(materialsSection);

			// Enforce panel padding in sidebar
			foreach(var sectionWidget in scrollableContent.Children<SectionWidget>())
			{
				var contentPanel = sectionWidget.ContentPanel;
				contentPanel.Padding = new BorderDouble(10, 10, 10, 0);
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

			this.Parent.Visible = viewMode == null || viewMode == PartViewMode.Model;

			HashSet<IObject3DEditor> mappedEditors = GetEditorsForType(selectedItemType);

			var activeEditors = new List<(IObject3DEditor, IObject3D)>();

			if (componentType.IsAssignableFrom(selectedItemType))
			{
				var members = from item in selectedItemType.GetProperties(PublicPropertyEditor.OwnedPropertiesOnly)
								let value = item.GetValue(selectedItem, null) as IObject3D
								let propertyType = item.PropertyType
								where iobject3DType.IsAssignableFrom(propertyType)
								select new
								{
									Type = propertyType,
									Value = value
								};

				foreach (var member in members)
				{
					if (this.GetEditorsForType(member.Type)?.FirstOrDefault() is IObject3DEditor editor)
					{
						activeEditors.Add((editor, member.Value));
					}
				}
			}

			if (mappedEditors?.Any() == true)
			{
				// Select the active editor or fall back to the first if not found
				var firstFilteredEditor = (from editor in mappedEditors
								   let type = editor.GetType()
								   where type.Name == selectedItem.ActiveEditor
								   select editor).FirstOrDefault();

				// Use first filtered or fall back to unfiltered first
				activeEditors.Add((firstFilteredEditor ?? mappedEditors.First(), selectedItem));
			}

			ShowObjectEditor(activeEditors);
		}

		private HashSet<IObject3DEditor> GetEditorsForType(Type selectedItemType)
		{
			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType.TryGetValue(selectedItemType, out mappedEditors);

			if (mappedEditors == null)
			{
				foreach (var kvp in objectEditorsByType)
				{
					var editorType = kvp.Key;

					if (editorType.IsAssignableFrom(selectedItemType))
					{
						mappedEditors = kvp.Value;
						break;
					}
				}
			}

			return mappedEditors;
		}

		private class OperationButton :TextButton
		{
			private GraphOperation graphOperation;
			private IObject3D sceneItem;

			public OperationButton(GraphOperation graphOperation, IObject3D sceneItem, ThemeConfig theme)
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

		private void ShowObjectEditor(IEnumerable<(IObject3DEditor editor, IObject3D item)> scope)
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
				editorWidget.Padding = 0;

				editorPanel.AddChild(editorWidget);

				var buttons = new List<OperationButton>();

				foreach (var graphOperation in ApplicationController.Instance.Graph.Operations)
				{
					foreach (var type in graphOperation.MappedTypes)
					{
						if (type.IsAssignableFrom(selectedItemType))
						{
							var button = new OperationButton(graphOperation, selectedItem, theme)
							{
								BackgroundColor = theme.MinimalShade,
								Margin = theme.ButtonSpacing
							};
							button.EnsureAvailablity();
							button.Click += (s, e) =>
							{
								graphOperation.Operation(selectedItem, scene).ConfigureAwait(false);
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

		public class InMemoryItem : ILibraryContentItem
		{
			private IObject3D existingItem;

			public InMemoryItem(IObject3D existingItem)
			{
				this.existingItem = existingItem;
			}

			public string ID => existingItem.ID;

			public string Name => existingItem.Name;

			public string FileName => $"{this.Name}.{this.ContentType}";

			public bool IsProtected => !existingItem.Persistable;

			public bool IsVisible => existingItem.Visible;

			public string ContentType => "mcx";

			public string Category => "General";

			public string AssetPath { get; set; }

			public Task<IObject3D> GetContent(Action<double, string> reportProgress)
			{
				return Task.FromResult(existingItem);
			}
		}
	}

	public enum AxisAlignment { Min, Center, Max, SourceCoordinateSystem };
}