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
using JsonPath;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.SlicerConfiguration;
using static JsonPath.JsonPathContext.ReflectionValueSystem;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectedObjectPanel : FlowLayoutWidget, IContentStore
	{
		private IObject3D item = new Object3D();

		private readonly ThemeConfig theme;
		private readonly ISceneContext sceneContext;
		private readonly SectionWidget editorSectionWidget;

		private readonly GuiWidget editorPanel;

		private readonly string editorTitle = "Properties".Localize();

		public SelectedObjectPanel(View3DWidget view3DWidget, ISceneContext sceneContext, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;
			this.Padding = 0;
			this.theme = theme;
			this.sceneContext = sceneContext;

			var toolbar = new LeftClipFlowLayoutWidget()
			{
				BackgroundColor = theme.BackgroundColor,
				Padding = theme.ToolbarPadding,
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};

			scene = sceneContext.Scene;

			// put in a make permanent button
			var icon = AggContext.StaticData.LoadIcon("noun_766157.png", 16, 16, theme.InvertIcons).SetPreMultiply();
			flattenButton = new IconButton(icon, theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Flatten".Localize(),
				Enabled = true
			};
			flattenButton.Click += (s, e) =>
			{
				if (this.item.CanFlatten)
				{
					var item = this.item;
					using (new SelectionMaintainer(view3DWidget.Scene))
					{
						item.Flatten(view3DWidget.Scene.UndoBuffer);
					}
				}
				else
				{
					// try to ungroup it
					sceneContext.Scene.UngroupSelection();
				}
			};
			toolbar.AddChild(flattenButton);

			// put in a remove button
			removeButton = new IconButton(AggContext.StaticData.LoadIcon("remove.png", 16, 16, theme.InvertIcons), theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Delete".Localize(),
				Enabled = scene.SelectedItem != null
			};
			removeButton.Click += (s, e) =>
			{
				var item = this.item;
				using (new SelectionMaintainer(view3DWidget.Scene))
				{
					item.Remove(view3DWidget.Scene.UndoBuffer);
				}
			};
			toolbar.AddChild(removeButton);

			primaryActionsPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Center | VAnchor.Fit
			};

			toolbar.AddChild(primaryActionsPanel);

			overflowButton = new OverflowBar.OverflowMenuButton(theme)
			{
				Enabled = scene.SelectedItem != null,
			};
			overflowButton.DynamicPopupContent = () =>
			{
				var remainingOperations = ApplicationController.Instance.Graph.Operations.Values.Except(primaryActions);

				return ApplicationController.Instance.GetActionMenuForSceneItem(item, sceneContext.Scene, false, remainingOperations);
			};
			toolbar.AddChild(overflowButton);

			editorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Name = "editorPanel",
			};

			// Wrap editorPanel with scrollable container
			var scrollableWidget = new ScrollableWidget(true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			scrollableWidget.AddChild(editorPanel);
			scrollableWidget.ScrollArea.HAnchor = HAnchor.Stretch;

			editorSectionWidget = new SectionWidget(editorTitle, scrollableWidget, theme, toolbar, expandingContent: false, defaultExpansion: true, setContentVAnchor: false)
			{
				VAnchor = VAnchor.Stretch
			};
			this.AddChild(editorSectionWidget);

			this.ContentPanel = editorPanel;

			// Register listeners
			scene.SelectionChanged += Scene_SelectionChanged;
		}

		public GuiWidget ContentPanel { get; set; }

		private readonly JsonPathContext pathResolver = new JsonPathContext();
		private readonly IconButton flattenButton;
		private readonly IconButton removeButton;
		private readonly OverflowBar.OverflowMenuButton overflowButton;
		private readonly InteractiveScene scene;
		private readonly FlowLayoutWidget primaryActionsPanel;

		private List<NodeOperation> primaryActions = new List<NodeOperation>();

		public void SetActiveItem(IObject3D selectedItem)
		{
			if (this.item == selectedItem)
			{
				return;
			}

			this.item = selectedItem;
			editorPanel.CloseAllChildren();

			// Allow caller to clean up with passing null for selectedItem
			if (item == null)
			{
				editorSectionWidget.Text = editorTitle;
				return;
			}

			var selectedItemType = selectedItem.GetType();

			primaryActionsPanel.RemoveAllChildren();

			var graph = ApplicationController.Instance.Graph;
			if (!graph.PrimaryOperations.TryGetValue(selectedItemType, out primaryActions))
			{
				primaryActions = new List<NodeOperation>();
			}
			else
			{
				// Loop over primary actions creating a button for each
				foreach (var primaryAction in primaryActions)
				{
					// TODO: Run visible/enable rules on actions, conditionally add/enable as appropriate
					var button = new IconButton(primaryAction.IconCollector(theme.InvertIcons), theme)
					{
						// Name = namedAction.Title + " Button",
						ToolTipText = primaryAction.Title,
						Margin = theme.ButtonSpacing,
						BackgroundColor = theme.ToolbarButtonBackground,
						HoverColor = theme.ToolbarButtonHover,
						MouseDownColor = theme.ToolbarButtonDown,
					};

					button.Click += (s, e) =>
					{
						primaryAction.Operation.Invoke(item, scene);
					};

					primaryActionsPanel.AddChild(button);
				}
			}

			editorSectionWidget.Text = selectedItem.Name ?? selectedItemType.Name;

			HashSet<IObject3DEditor> mappedEditors = ApplicationController.Instance.Extensions.GetEditorsForType(selectedItemType);

			var undoBuffer = sceneContext.Scene.UndoBuffer;

			// put in a color edit field
			var colorField = new ColorField(theme, selectedItem.Color);
			colorField.Initialize(0);
			colorField.ValueChanged += (s, e) =>
			{
				if (selectedItem.Color != colorField.Color)
				{
					undoBuffer.AddAndDo(new ChangeColor(selectedItem, colorField.Color));
				}
			};

			colorField.Content.MouseDown += (s, e) =>
			{
				// make sure the render mode is set to shaded or outline
				if (sceneContext.ViewState.RenderType != RenderOpenGl.RenderTypes.Shaded
					&& sceneContext.ViewState.RenderType != RenderOpenGl.RenderTypes.Outlines)
				{
					// make sure the render mode is set to outline
					sceneContext.ViewState.RenderType = RenderOpenGl.RenderTypes.Outlines;
				}
			};

			// color row
			var row = PublicPropertyEditor.CreateSettingsRow("Color".Localize(), null, colorField.Content, theme);

			// Special top border style for first item in editor
			row.Border = new BorderDouble(0, 1);

			editorPanel.AddChild(row);

			// put in a material edit field
			var materialField = new MaterialIndexField(theme, selectedItem.MaterialIndex);
			materialField.Initialize(0);
			materialField.ValueChanged += (s, e) =>
			{
				if (selectedItem.MaterialIndex != materialField.MaterialIndex)
				{
					undoBuffer.AddAndDo(new ChangeMaterial(selectedItem, materialField.MaterialIndex));
				}
			};

			materialField.Content.MouseDown += (s, e) =>
			{
				if (sceneContext.ViewState.RenderType != RenderOpenGl.RenderTypes.Materials)
				{
					// make sure the render mode is set to material
					sceneContext.ViewState.RenderType = RenderOpenGl.RenderTypes.Materials;
				}
			};

			// material row
			editorPanel.AddChild(
				PublicPropertyEditor.CreateSettingsRow("Material".Localize(), null, materialField.Content, theme));

			// put in the normal editor
			if (selectedItem is ComponentObject3D componentObject
				&& componentObject.Finalized)
			{
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
							if (ApplicationController.Instance.Extensions.GetEditorsForType(object3D.GetType())?.FirstOrDefault() is IObject3DEditor editor)
							{
								ShowObjectEditor((editor, object3D, object3D.Name), selectedItem);
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
					sectionWidget.Margin = 0;
				}
			}
			else
			{
				if (ApplicationController.Instance.Extensions.GetEditorsForType(item.GetType())?.FirstOrDefault() is IObject3DEditor editor)
				{
					ShowObjectEditor((editor, item, item.Name), selectedItem);
				}
			}
		}

		private class OperationButton : TextButton
		{
			private readonly NodeOperation graphOperation;
			private readonly IObject3D sceneItem;

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

		private void ShowObjectEditor((IObject3DEditor editor, IObject3D item, string displayName) scopeItem, IObject3D rootSelection)
		{
			var selectedItem = scopeItem.item;

			var editorWidget = scopeItem.editor.Create(selectedItem, sceneContext.Scene.UndoBuffer, theme);
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

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			scene.SelectionChanged -= Scene_SelectionChanged;

			base.OnClosed(e);
		}

		private void Scene_SelectionChanged(object sender, EventArgs e)
		{
			if (editorPanel.Children.FirstOrDefault()?.DescendantsAndSelf<SectionWidget>().FirstOrDefault() is SectionWidget firstSectionWidget)
			{
				firstSectionWidget.Margin = firstSectionWidget.Margin.Clone(top: 0);
			}

			var selectedItem = scene.SelectedItem;

			flattenButton.Enabled = selectedItem != null
				&& (selectedItem is GroupObject3D
				|| selectedItem.GetType() == typeof(Object3D)
				|| selectedItem.CanFlatten);
			removeButton.Enabled = selectedItem != null;
			overflowButton.Enabled = selectedItem != null;
			if (selectedItem == null)
			{
				primaryActionsPanel.RemoveAllChildren();
			}
		}
	}
}