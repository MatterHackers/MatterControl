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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartTabPage : TabPage
	{
		// TODO: Don't change casing... almost certainly none of these should be exposed
		internal View3DWidget view3DWidget;
		internal BedConfig sceneContext;
		internal PrinterConfig printer;

		protected ViewControls3D viewControls3D;
		protected ThemeConfig theme;
		protected GuiWidget view3DContainer;
		protected FlowLayoutWidget topToBottom;
		protected FlowLayoutWidget leftToRight;
		protected LibraryListView favoritesBar;

		public PartTabPage(PrinterConfig printer, BedConfig sceneContext, ThemeConfig theme, string tabTitle)
			: base (tabTitle)
		{
			this.sceneContext = sceneContext;
			this.theme = theme;
			this.BackgroundColor = theme.BackgroundColor;
			this.Padding = 0;
			this.printer = printer;

			bool isPrinterType = this is PrinterTabPage;

			var favoritesBarAndView3DWidget = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			viewControls3D = new ViewControls3D(sceneContext, theme, sceneContext.Scene.UndoBuffer, isPrinterType, !(this is PrinterTabPage))
			{
				VAnchor = VAnchor.Top | VAnchor.Fit,
				HAnchor = HAnchor.Left | HAnchor.Stretch,
				Visible = true,
			};
			theme.ApplyBottomBorder(viewControls3D, shadedBorder: (this is PrinterTabPage)); // Shade border if toolbar is secondary rather than primary

			viewControls3D.ResetView += (sender, e) =>
			{
				if (view3DWidget.Visible)
				{
					this.view3DWidget.ResetView();
				}
			};
			viewControls3D.ExtendOverflowMenu = this.GetViewControls3DOverflowMenu;
			viewControls3D.OverflowButton.Name = "View3D Overflow Menu";

			// The 3D model view
			view3DWidget = new View3DWidget(
				printer,
				sceneContext,
				viewControls3D,
				theme,
				this,
				editorType: (isPrinterType) ? MeshViewerWidget.EditorType.Printer : MeshViewerWidget.EditorType.Part);

			viewControls3D.SetView3DWidget(view3DWidget);

			this.AddChild(topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			});

			topToBottom.AddChild(leftToRight = new FlowLayoutWidget()
			{
				Name = "View3DContainerParent",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			});

			view3DContainer = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			var toolbarAndView3DWidget = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			toolbarAndView3DWidget.AddChild(viewControls3D);

			var favoritesBarContext = new LibraryConfig()
			{
				ActiveContainer = ApplicationController.Instance.Library.RootLibaryContainer
			};

			var leftBar = new GuiWidget()
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Fit,
				Border = new BorderDouble(top: 1, right: 1),
				BorderColor = theme.BorderColor20,
			};
			favoritesBarAndView3DWidget.AddChild(leftBar);

			bool expanded = UserSettings.Instance.get(UserSettingsKey.FavoritesBarExpansion) != "0";

			favoritesBar = new LibraryListView(favoritesBarContext, theme)
			{
				Name = "LibraryView",
				// Drop containers
				ContainerFilter = (container) => false,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Stretch,
				AllowContextMenu = false,

				// restore to state for favorites bar size
				Width = expanded ? 55 : 33,
				ListContentView = new IconView(theme, expanded ? 48 : 24)
				{
					VAnchor = VAnchor.Fit | VAnchor.Top
				},
			};
			leftBar.AddChild(favoritesBar);

			favoritesBar.ScrollArea.VAnchor = VAnchor.Fit;

			var expandedImage = AggContext.StaticData.LoadIcon("expand.png", 16, 16, theme.InvertIcons);
			var collapsedImage = AggContext.StaticData.LoadIcon("collapse.png", 16, 16, theme.InvertIcons);

			var expandBarButton = new IconButton(expanded ? collapsedImage : expandedImage, theme)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Absolute | VAnchor.Bottom,
				Margin = new BorderDouble(bottom: 3, top: 3),
				Height = theme.ButtonHeight - 6,
				Width = theme.ButtonHeight - 6
			};

			expandBarButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				expanded = !expanded;

				UserSettings.Instance.set(UserSettingsKey.FavoritesBarExpansion, expanded ? "1" : "0");

				favoritesBar.ListContentView = new IconView(theme, expanded ? 48 : 24);
				favoritesBar.Width = expanded ? 55 : 33;
				expandBarButton.SetIcon(expanded ? collapsedImage : expandedImage);
				expandBarButton.Invalidate();

				favoritesBar.Reload().ConfigureAwait(false);
			});
			leftBar.AddChild(expandBarButton);

			favoritesBar.Margin = new BorderDouble(bottom: expandBarButton.Height + expandBarButton.Margin.Height);

			favoritesBarAndView3DWidget.AddChild(view3DWidget);
			toolbarAndView3DWidget.AddChild(favoritesBarAndView3DWidget);

			view3DContainer.AddChild(toolbarAndView3DWidget);

			leftToRight.AddChild(view3DContainer);

			if (sceneContext.World.RotationMatrix == Matrix4X4.Identity)
			{
				this.view3DWidget.ResetView();
			}

			this.AnchorAll();
		}

		public override void OnFocusChanged(EventArgs e)
		{
			base.OnFocusChanged(e);
			view3DWidget.Focus();
		}

		protected virtual void GetViewControls3DOverflowMenu(PopupMenu popupMenu)
		{
			view3DWidget.ShowOverflowMenu(popupMenu);
		}
	}
}
