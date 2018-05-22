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
using MatterHackers.MatterControl.CustomWidgets.TreeView;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	[HideUpdateButtonAttribute]
	public class SelectedObjectPanel : FlowLayoutWidget, IContentStore
	{
		private IObject3D rootSelectedItem = new Object3D();

		private ThemeConfig theme;
		private View3DWidget view3DWidget;
		private InteractiveScene scene;
		private PrinterConfig printer;
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
				if (rootSelectedItem != null)
				{
					rootSelectedItem.Name = inlineTitleEdit.Text;
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

			var toolbar = new Toolbar(theme)
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

				var clonedItem = this.rootSelectedItem.Clone();

				// Edit in Identity transform
				clonedItem.Matrix = Matrix4X4.Identity;

				await bed.LoadContent(
					new EditContext()
					{
						ContentStore = new DynamicContentStore((libraryItem, object3D) =>
						{
							var replacement = object3D.Clone();

							this.rootSelectedItem.Parent.Children.Modify(list =>
							{
								list.Remove(rootSelectedItem);

								// Restore matrix of item being replaced
								replacement.Matrix = rootSelectedItem.Matrix;

								list.Add(replacement);

								rootSelectedItem = replacement;
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
				this.rootSelectedItem.Apply(view3DWidget.Scene.UndoBuffer);
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
				this.rootSelectedItem.Remove(view3DWidget.Scene.UndoBuffer);
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

			var editorSection = new SectionWidget("Editor".Localize(), editorColumn, theme, serializationKey: UserSettingsKey.EditorPanelExpanded, defaultExpansion: true);
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

			this.rootSelectedItem = selectedItem;

			var viewMode = printer?.ViewState.ViewMode;

			ShowObjectEditor(selectedItem);
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

		private void ShowObjectEditor(IObject3D selectedItem)
		{
			editorPanel.CloseAllChildren();

			// Add the tree view container. eventually we may want to make this a stretch container of some type
			var treeViewContainer = new BottomResizeContainer(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute
			};
			treeViewContainer.Height = 250;
			editorPanel.AddChild(treeViewContainer);

			// add the tree view
			var treeView = GetPartTreeView(selectedItem);
			treeViewContainer.AddChild(treeView);

			// Add the selected item editor container. eventually we may want to make this a stretch container of some type
			var selectedItemContainer = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			treeViewContainer.Height = 250;
			editorPanel.AddChild(selectedItemContainer);

			treeView.AfterSelect += (s, e) =>
			{
				selectedItemContainer.CloseAllChildren();
				var selectedNodeItem = (IObject3D)treeView.SelectedNode.Tag;
				var selectedNodeItemType = selectedNodeItem.GetType();

				HashSet<IObject3DEditor> mappedEditors = ApplicationController.Instance.GetEditorsForType(selectedNodeItemType);
				if (ApplicationController.Instance.GetEditorsForType(selectedNodeItemType)?.FirstOrDefault() is IObject3DEditor editor)
				{
					var editorWidget = editor.Create(selectedNodeItem, view3DWidget, theme);
					editorWidget.HAnchor = HAnchor.Stretch;
					editorWidget.VAnchor = VAnchor.Fit;

					editorWidget.Padding = new BorderDouble(10, 10, 10, 0);

					// EditOutline section
					var sectionWidget = new SectionWidget(
							string.IsNullOrWhiteSpace(selectedNodeItem.Name) ? "Unknown" : selectedNodeItem.Name,
							editorWidget,
							theme);

					theme.ApplyBoxStyle(sectionWidget, margin: 0);

					editorWidget = sectionWidget;

					selectedItemContainer.AddChild(editorWidget);

					var buttons = new List<OperationButton>();

					foreach (var nodeOperation in ApplicationController.Instance.Graph.Operations)
					{
						foreach (var type in nodeOperation.MappedTypes)
						{
							if (type.IsAssignableFrom(selectedNodeItemType)
								&& (nodeOperation.IsVisible == null || nodeOperation.IsVisible(selectedNodeItem)))
							{
								var button = new OperationButton(nodeOperation, selectedNodeItem, theme)
								{
									BackgroundColor = theme.MinimalShade,
									Margin = theme.ButtonSpacing
								};
								button.EnsureAvailablity();
								button.Click += (s1, e1) =>
								{
									nodeOperation.Operation(selectedNodeItem, scene).ConfigureAwait(false);
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
						selectedItemContainer.AddChild(toolbar);

						foreach (var button in buttons)
						{
							toolbar.AddChild(button);
						}

						// TODO: Fix likely leak
						selectedNodeItem.Invalidated += (s2, e2) =>
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
			};

			treeView.SelectedNode = treeView.TopNode;
		}

		private void AddTree(IObject3D item, TreeNode parent)
		{
			var node = AddItem(item, parent);

			foreach (var child in item.Children)
			{
				AddTree(child, node);
			}
		}

		private TreeNode AddItem(IObject3D item, TreeNode parentNode)
		{
			var inmemoryItem = new InMemoryLibraryItem(item.Clone());
			var iconView = new IconViewItem(new ListViewItem(inmemoryItem, ApplicationController.Instance.Library.PlatingHistory), 16, 16, theme);

			var node = new TreeNode()
			{
				Text = BuildDefaultName(item),
				Tag = item,
				TextColor = theme.Colors.PrimaryTextColor,
				PointSize = theme.DefaultFontSize,
				//Expanded = true,
			};
			selectionTreeNodes.Add(item, node);

			node.Load += (s, e) =>
			{
				iconView.OnLoad(e);

				iconView.ImageSet += (s1, e1) =>
				{
					node.Image = iconView.imageWidget.Image;
				};
			};

			parentNode.Nodes.Add(node);
			parentNode.Expanded = true;

			return node;
		}

		private Dictionary<IObject3D, TreeNode> selectionTreeNodes = new Dictionary<IObject3D, TreeNode>();

		private string BuildDefaultName(IObject3D item)
		{
			string nameToWrite = "";
			if (!string.IsNullOrEmpty(item.Name))
			{
				nameToWrite += $"{item.GetType().Name} - {item.Name}";
			}
			else
			{
				nameToWrite += $"{item.GetType().Name}";
			}

			return nameToWrite;
		}

		private TreeView GetPartTreeView(IObject3D rootSelection)
		{
			var topNode = new TopNode()
			{
				Text = BuildDefaultName(rootSelection),
				Tag = rootSelection,
				TextColor = theme.Colors.PrimaryTextColor,
				PointSize = theme.DefaultFontSize
			};

			var treeView = new TreeView(topNode, theme)
			{
				TextColor = theme.Colors.PrimaryTextColor,
				PointSize = theme.DefaultFontSize
			};

			treeView.SuspendLayout();
			selectionTreeNodes.Clear();

			selectionTreeNodes.Add(rootSelection, topNode);

			// add the children to the root node
			foreach (var child in rootSelectedItem.Children)
			{
				AddTree(child, topNode);
			}

			treeView.ResumeLayout();

			return treeView;
		}

		public void Save(ILibraryItem item, IObject3D content)
		{
			this.rootSelectedItem.Parent.Children.Modify(children =>
			{
				children.Remove(this.rootSelectedItem);
				children.Add(content);
			});
		}
	}
}