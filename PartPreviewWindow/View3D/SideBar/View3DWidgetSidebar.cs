/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class View3DWidgetSidebar : FlowLayoutWidget
	{
		private View3DWidget view3DWidget;

		// TODO: Remove debugging variables and draw functions once drag items are positioning correctly
		private Vector2 mouseMovePosition;
		private RectangleDouble meshViewerPosition;
		private FlowLayoutWidget buttonPanel;

		public View3DWidgetSidebar(View3DWidget view3DWidget, double buildHeight)
			: base(FlowDirection.TopToBottom)
		{
			this.view3DWidget = view3DWidget;
			this.Width = 200;

			var ExpandMenuOptionFactory = view3DWidget.ExpandMenuOptionFactory;

			buttonPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.FitToChildren
			};
			this.AddChild(buttonPanel);
			this.Padding = new BorderDouble(6, 6);
			this.Margin = new BorderDouble(0, 1);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.VAnchor = VAnchor.ParentBottomTop;
		}

		// InitializeComponent is called after the Sidebar property has been assigned as SidebarPlugins
		// are passed an instance of the View3DWidget and expect to be able to access the Sidebar to
		// call create button methods
		public void InitializeComponents()
		{
			HashSet<IObject3DEditor> mappedEditors;

			var objectEditorsByType = new Dictionary<Type, HashSet<IObject3DEditor>>();

			// TODO: Consider only loading once into a static
			var objectEditors = new PluginFinder<IObject3DEditor>().Plugins;
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

			view3DWidget.objectEditors = objectEditors;
			view3DWidget.objectEditorsByType = objectEditorsByType;
		}

		public GuiWidget CreateAddButton(string buttonLabel, Func<IObject3D> itemCreator)
		{
			GuiWidget addItemButton = CreateButtonState(
				buttonLabel,
				ImageBuffer.CreateScaledImage(StaticData.Instance.LoadImage(Path.Combine("Icons", "part_icon_transparent_40x40.png")), 64, 64), 
				ActiveTheme.Instance.PrimaryBackgroundColor, 
				ActiveTheme.Instance.PrimaryTextColor);
			addItemButton.Margin = new BorderDouble(3);

			addItemButton.MouseDown += (sender, e) =>
			{
				view3DWidget.DragDropSource = itemCreator();
			};

			addItemButton.MouseMove += (sender, mouseArgs) =>
			{
				var screenSpaceMousePosition = addItemButton.TransformToScreenSpace(mouseArgs.Position);
				view3DWidget.AltDragOver(screenSpaceMousePosition);
			};

			addItemButton.MouseUp += (sender, mouseArgs) =>
			{
				if (addItemButton.LocalBounds.Contains(mouseArgs.Position) && view3DWidget.DragDropSource != null)
				{
					// Button click within the bounds of this control - Insert item at the best open position
					PlatingHelper.MoveToOpenPosition(view3DWidget.DragDropSource, view3DWidget.Scene);
					view3DWidget.InsertNewItem(view3DWidget.DragDropSource);
				}
				else if (view3DWidget.DragDropSource != null && view3DWidget.Scene.Children.Contains(view3DWidget.DragDropSource))
				{
					// Drag release outside the bounds of this control and not within the scene - Remove inserted item
					//
					// Mouse and widget positions
					var screenSpaceMousePosition = addItemButton.TransformToScreenSpace(mouseArgs.Position);
					meshViewerPosition = this.view3DWidget.meshViewerWidget.TransformToScreenSpace(view3DWidget.meshViewerWidget.LocalBounds);

					// If the mouse is not within the meshViewer, remove the inserted drag item
					if (!meshViewerPosition.Contains(screenSpaceMousePosition))
					{
						view3DWidget.Scene.ModifyChildren(children => children.Remove(view3DWidget.DragDropSource));
						view3DWidget.Scene.ClearSelection();
					}
					else
					{
						// Create and push the undo operation
						view3DWidget.AddUndoOperation(
							new InsertCommand(view3DWidget, view3DWidget.DragDropSource));
					}
				}

				view3DWidget.DragDropSource = null;
			};

			return addItemButton;
		}

		private static FlowLayoutWidget CreateButtonState(string buttonLabel, ImageBuffer buttonImage, RGBA_Bytes color, RGBA_Bytes textColor)
		{
			FlowLayoutWidget flowLayout = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				BackgroundColor = color,
			};
			flowLayout.AddChild(new ImageWidget(buttonImage)
			{
				Margin = new BorderDouble(0, 5, 0, 0),
				BackgroundColor = RGBA_Bytes.Gray,
				HAnchor = HAnchor.ParentCenter,
				Selectable = false,
			});
			flowLayout.AddChild(new TextWidget(buttonLabel, 0, 0, 9, Agg.Font.Justification.Center, textColor)
			{
				HAnchor = HAnchor.ParentCenter,
				Selectable = false,
			});
			return flowLayout;
		}
	}
}