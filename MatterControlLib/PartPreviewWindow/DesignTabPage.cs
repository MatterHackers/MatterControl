﻿/*
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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class DesignTabPage : TabPage
	{
		// TODO: Don't change casing... almost certainly none of these should be exposed
		internal View3DWidget view3DWidget;
		internal ISceneContext sceneContext;
		protected ViewToolBarControls viewToolBarControls;
		protected ThemeConfig theme;
		protected GuiWidget view3DContainer;
		protected FlowLayoutWidget topToBottom;
		protected FlowLayoutWidget leftToRight;
		protected LibraryListView favoritesBar;

		public DesignTabPage(PartWorkspace workspace, ThemeConfig theme, string tabTitle)
			: base(tabTitle)
		{
			this.sceneContext = workspace.SceneContext;
			this.theme = theme;
			this.BackgroundColor = theme.BackgroundColor;
			this.Padding = 0;
			this.Workspace = workspace;

			var favoritesBarAndView3DWidget = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			viewToolBarControls = new ViewToolBarControls(workspace, theme, sceneContext.Scene.UndoBuffer)
			{
				VAnchor = VAnchor.Top | VAnchor.Fit,
				HAnchor = HAnchor.Left | HAnchor.Stretch,
				Visible = true,
			};

			// Shade border if toolbar is secondary rather than primary
			theme.ApplyBottomBorder(viewToolBarControls, shadedBorder: false);

			viewToolBarControls.ResetView += (sender, e) =>
			{
				if (view3DWidget.Visible)
				{
					this.view3DWidget.ResetView();
				}
			};

			// The 3D model view
			view3DWidget = new View3DWidget(
				null,
				sceneContext,
				viewToolBarControls,
				theme,
				this);

			// add in the task display
			view3DWidget.AddChild(new RunningTasksWidget(theme, null)
			{
				MinimumSize = new Vector2(100, 0),
				Margin = new BorderDouble(9, 0, 0, 9),
				VAnchor = VAnchor.Top | VAnchor.Fit,
				HAnchor = HAnchor.Left | HAnchor.Fit,
			});

			viewToolBarControls.SetView3DWidget(view3DWidget);

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
			toolbarAndView3DWidget.AddChild(viewToolBarControls);

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
				// HAnchor = HAnchor.Fit,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Stretch,
				AllowContextMenu = false,
				ActiveSort = SortKey.ModifiedDate,
				Ascending = true,
				// restore to state for favorites bar size
				Width = expanded ? 55 * GuiWidget.DeviceScale : 33 * GuiWidget.DeviceScale,
				ListContentView = new IconView(theme, expanded ? 48 * GuiWidget.DeviceScale : 24 * GuiWidget.DeviceScale)
				{
					VAnchor = VAnchor.Fit | VAnchor.Top
				},
			};

			// favoritesBar.ScrollArea.HAnchor = HAnchor.Fit;
			favoritesBar.ListContentView.HAnchor = HAnchor.Fit;
			leftBar.AddChild(favoritesBar);

			void UpdateWidth(object s, EventArgs e)
			{
				if (s is GuiWidget widget)
				{
					favoritesBar.Width = widget.Width;
				}
			}

			favoritesBar.ListContentView.BoundsChanged += UpdateWidth;

			favoritesBar.ScrollArea.VAnchor = VAnchor.Fit;

			favoritesBar.VerticalScrollBar.Show = ScrollBar.ShowState.Never;

			var expandedImage = StaticData.Instance.LoadIcon("expand.png", 16, 16).GrayToColor(theme.TextColor);
			var collapsedImage = StaticData.Instance.LoadIcon("collapse.png", 16, 16).GrayToColor(theme.TextColor);

			var expandBarButton = new ThemedIconButton(expanded ? collapsedImage : expandedImage, theme)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Absolute | VAnchor.Bottom,
				Margin = new BorderDouble(bottom: 3, top: 3),
				Height = theme.ButtonHeight - 6 * GuiWidget.DeviceScale,
				Width = theme.ButtonHeight - 6 * GuiWidget.DeviceScale,
				ToolTipText = expanded ? "Reduced Width".Localize() : "Expand Width".Localize(),
			};

			expandBarButton.Click += (s, e) => UiThread.RunOnIdle(async () =>
			{
				expanded = !expanded;

				// remove from the one we are deleting
				favoritesBar.ListContentView.BoundsChanged -= UpdateWidth;
				UserSettings.Instance.set(UserSettingsKey.FavoritesBarExpansion, expanded ? "1" : "0");
				favoritesBar.ListContentView = new IconView(theme, expanded ? 48 * GuiWidget.DeviceScale : 24 * GuiWidget.DeviceScale);
				favoritesBar.ListContentView.HAnchor = HAnchor.Fit;
				// add to the one we created
				favoritesBar.ListContentView.BoundsChanged += UpdateWidth;
				expandBarButton.SetIcon(expanded ? collapsedImage : expandedImage);
				expandBarButton.Invalidate();
				expandBarButton.ToolTipText = expanded ? "Reduced Width".Localize() : "Expand Width".Localize();

				await favoritesBar.Reload();
				UpdateWidth(favoritesBar.ListContentView, null);
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

		public PartWorkspace Workspace { get; }

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
