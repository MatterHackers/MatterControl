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
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectedObjectPanel : FlowLayoutWidget, IContentStore
	{
		private IObject3D item = new Object3D();

		private FlowLayoutWidget editorPanel;
		private TextWidget itemName;
		private ThemeConfig theme;
		private View3DWidget view3DWidget;
		private InteractiveScene scene;
		private PrinterConfig printer;
		private Dictionary<Type, HashSet<IObject3DEditor>> objectEditorsByType;

		public SelectedObjectPanel(View3DWidget view3DWidget, InteractiveScene scene, ThemeConfig theme, PrinterConfig printer)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;
			this.Padding = new BorderDouble(8, 10);
			this.MinimumSize = new VectorMath.Vector2(220, 0);

			this.view3DWidget = view3DWidget;
			this.theme = theme;
			this.scene = scene;
			this.printer = printer;

			this.AddChild(itemName = new TextWidget("", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				EllipsisIfClipped = true,
				Margin = new BorderDouble(bottom: 10)
			});

			var behavior3DTypeButtons = new FlowLayoutWidget();
			this.AddChild(behavior3DTypeButtons);

			var buttonMargin = new BorderDouble(2, 5);

			// put in the button for making the behavior solid
			var solidButtonView = new TextButton("Color".Localize(), theme)
			{
				BackgroundColor = theme.MinimalShade
			};
			var solidBehaviorButton = new PopupButton(solidButtonView)
			{
				Name = "Solid Colors",
				AlignToRightEdge = true,
				PopupContent = new ColorSwatchSelector(scene)
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
				},
			};

			behavior3DTypeButtons.AddChild(solidBehaviorButton);

			editButton = new TextButton("Edit".Localize(), theme)
			{
				BackgroundColor = theme.MinimalShade,
				Margin = theme.ButtonSpacing
			};
			editButton.Click += async (s, e) =>
			{
				BedConfig bed;

				var partPreviewContent = this.Parents<PartPreviewContent>().FirstOrDefault();
				partPreviewContent.CreatePartTab(
					"New Part",
					bed = new BedConfig(),
					theme);

				await bed.LoadContent(
					new EditContext()
					{
						ContentStore = ApplicationController.Instance.Library.PlatingHistory,
						SourceItem = new InMemoryItem(this.item),
					});
			};
			behavior3DTypeButtons.AddChild(editButton);

			editorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};

			var scrollable = new ScrollableWidget(true)
			{
				Name = "editorPanel",
				Margin = new BorderDouble(top: 10),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};

			scrollable.AddChild(editorPanel);
			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;

			this.AddChild(scrollable);

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

			this.editorPanel.RemoveAllChildren();

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

			if (mappedEditors != null)
			{
				var dropDownList = new DropDownList("", ActiveTheme.Instance.PrimaryTextColor, maxHeight: 300)
				{
					HAnchor = HAnchor.Stretch
				};

				foreach (IObject3DEditor editor in mappedEditors)
				{
					MenuItem menuItem = dropDownList.AddItem(editor.Name);
					menuItem.Selected += (s, e2) =>
					{
						ShowObjectEditor(editor);
					};
				}

				editorPanel.AddChild(dropDownList);

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

				int selectedIndex = 0;
				for (int i = 0; i < dropDownList.MenuItems.Count; i++)
				{
					if (dropDownList.MenuItems[i].Text == firstEditor.Name)
					{
						selectedIndex = i;
						break;
					}
				}

				dropDownList.SelectedIndex = selectedIndex;

				ShowObjectEditor(firstEditor);
			}
		}

		private GuiWidget activeEditorWidget;
		private TextButton editButton;

		private void ShowObjectEditor(IObject3DEditor editor)
		{
			if (editor == null)
			{
				return;
			}

			activeEditorWidget?.Close();

			var newEditor = editor.Create(scene.SelectedItem, view3DWidget, theme);
			newEditor.HAnchor = HAnchor.Stretch;
			newEditor.VAnchor = VAnchor.Fit;

			editorPanel.AddChild(newEditor);

			activeEditorWidget = newEditor;
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