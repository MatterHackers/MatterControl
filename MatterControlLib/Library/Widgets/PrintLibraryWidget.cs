/*
Copyright (c) 2019, Kevin Pope, John Lewin
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
using System.Linq;

using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl.Library.Widgets
{
	public class PrintLibraryWidget : GuiWidget, IIgnoredPopupChild
	{
		private FlowLayoutWidget buttonPanel;
		private ILibraryContext libraryContext;
		private LibraryListView libraryView;
		private GuiWidget providerMessageContainer;
		private TextWidget providerMessageWidget;

		private List<LibraryAction> menuActions = new List<LibraryAction>();

		private FolderBreadCrumbWidget breadCrumbWidget;
		private GuiWidget searchInput;
		private ILibraryContainer searchContainer;

		private MainViewWidget mainViewWidget;
		private ThemeConfig theme;
		private OverflowBar navBar;
		private GuiWidget searchButton;

		public bool ShowContainers { get; private set; } = true;

		public PrintLibraryWidget(MainViewWidget mainViewWidget, PartWorkspace workspace,  ThemeConfig theme, Color libraryBackground, PopupMenuButton popupMenuButton)
		{
			this.theme = theme;
			this.mainViewWidget = mainViewWidget;
			this.Padding = 0;
			this.AnchorAll();

			var allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

			libraryContext = workspace.LibraryView;

			libraryView = new LibraryListView(libraryContext, theme)
			{
				Name = "LibraryView",
				// Drop containers if ShowContainers != 1
				ContainerFilter = (container) => this.ShowContainers,
				BackgroundColor = libraryBackground,
				Border = new BorderDouble(top: 1)
			};

			navBar = new OverflowBar(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			allControls.AddChild(navBar);
			theme.ApplyBottomBorder(navBar);

			var toolbar = new OverflowBar(StaticData.Instance.LoadIcon("fa-sort_16.png", 32, 32).SetToColor(theme.TextColor), theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Name = "Folders Toolbar",
			};

			toolbar.OverflowButton.ToolTipText = "Sorting".Localize();

			theme.ApplyBottomBorder(toolbar, shadedBorder: true);

			toolbar.OverflowButton.Name = "Print Library View Options";
			toolbar.Padding = theme.ToolbarPadding;

			toolbar.ExtendOverflowMenu = (popupMenu) => LibraryWidget.CreateSortingMenu(popupMenu, theme, libraryView);

			allControls.AddChild(toolbar);

			toolbar.AddChild(new HorizontalSpacer());

			toolbar.AddChild(LibraryWidget.CreateViewOptionsMenuButton(theme,
				libraryView,
				(show) => ShowContainers = show,
				() => ShowContainers));

			breadCrumbWidget = new FolderBreadCrumbWidget(workspace.LibraryView, theme);
			navBar.AddChild(breadCrumbWidget);

			var searchPanel = new TextEditWithInlineCancel(theme)
			{
				Visible = false,
				Margin = new BorderDouble(10, 0, 5, 0),
			};
			searchPanel.TextEditWidget.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				this.PerformSearch();
			};
			searchPanel.ResetButton.Click += (s, e) =>
			{
				breadCrumbWidget.Visible = true;
				searchPanel.Visible = false;

				searchPanel.TextEditWidget.Text = "";

				this.ClearSearch();
			};

			// Store a reference to the input field
			this.searchInput = searchPanel.TextEditWidget;

			navBar.AddChild(searchPanel);

			searchButton = theme.CreateSearchButton();
			searchButton.Enabled = false;
			searchButton.Name = "Search Library Button";
			searchButton.Click += (s, e) =>
			{
				if (searchPanel.Visible)
				{
					PerformSearch();
				}
				else
				{
					searchContainer = libraryView.ActiveContainer;

					breadCrumbWidget.Visible = false;
					searchPanel.Visible = true;
					searchInput.Focus();
				}
			};
			navBar.AddChild(searchButton);

			allControls.AddChild(libraryView);

			buttonPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Padding = theme.ToolbarPadding,
			};
			AddLibraryButtonElements();
			allControls.AddChild(buttonPanel);

			allControls.AnchorAll();

			this.AddChild(allControls);

			// Register listeners
			libraryView.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
			libraryContext.ContainerChanged += Library_ContainerChanged;
		}

		private void PerformSearch()
		{
			UiThread.RunOnIdle(() =>
			{
				if (libraryContext.ActiveContainer.CustomSearch is ICustomSearch customSearch)
				{
					// Do custom search
					customSearch.ApplyFilter(searchInput.Text.Trim(), libraryContext);
				}
				else
				{
					// Do basic filtering
					// filter the view with a predicate, applying the active sort
					libraryView.ApplyFilter(searchInput.Text.Trim());
				}
			});
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.XButton1)
			{
				// user pressed the back button
				breadCrumbWidget.NavigateBack();
			}

			base.OnMouseDown(mouseEvent);
		}

		private void ClearSearch()
		{
			if (searchContainer == null)
			{
				return;
			}

			UiThread.RunOnIdle(() =>
			{
				if (libraryContext.ActiveContainer.CustomSearch is ICustomSearch customSearch)
				{
					// Clear custom search
					customSearch.ClearFilter();

					// Restore the original ActiveContainer before search started - some containers may change context
					libraryContext.ActiveContainer = searchContainer;
				}
				else
				{
					// Clear basic filtering
					libraryView.ClearFilter();
				}

				searchContainer = null;
			});
		}

		private void SelectedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
			{
				foreach (var item in libraryView.Items)
				{
					item.ViewWidget.IsSelected = false;
				}
			}

			if (e.OldItems != null)
			{
				foreach (var item in e.OldItems.OfType<ListViewItem>())
				{
					item.ViewWidget.IsSelected = false;
				}
			}

			if (e.NewItems != null)
			{
				foreach (var item in e.NewItems.OfType<ListViewItem>())
				{
					item.ViewWidget.IsSelected = true;
				}
			}
		}

		private void Library_ContainerChanged(object sender, ContainerChangedEventArgs e)
		{
			// Release
			if (e.PreviousContainer != null)
			{
				e.PreviousContainer.ContentChanged -= UpdateStatus;
			}

			var activeContainer = this.libraryView.ActiveContainer;

			bool containerSupportsEdits = activeContainer is ILibraryWritableContainer;

			// searchInput.Text = activeContainer.KeywordFilter;
			breadCrumbWidget.SetContainer(activeContainer);

			activeContainer.ContentChanged += UpdateStatus;

			searchButton.Enabled = activeContainer.Parent != null;

			UpdateStatus(null, null);
		}

		private void UpdateStatus(object sender, EventArgs e)
		{
			string message = this.libraryView.ActiveContainer?.HeaderMarkdown;
			if (!string.IsNullOrEmpty(message))
			{
				providerMessageWidget.Text = message;
				providerMessageContainer.Visible = true;
			}
			else
			{
				providerMessageContainer.Visible = false;
			}
		}

		private void AddLibraryButtonElements()
		{
			buttonPanel.RemoveChildren();

			// add in the message widget
			providerMessageContainer = new GuiWidget()
			{
				VAnchor = VAnchor.Fit | VAnchor.Top,
				HAnchor = HAnchor.Stretch,
				Visible = false,
			};
			buttonPanel.AddChild(providerMessageContainer, -1);

			providerMessageWidget = new TextWidget("")
			{
				PointSize = 8,
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Bottom,
				TextColor = theme.LightTextColor,
				Margin = new BorderDouble(6),
				AutoExpandBoundsToText = true,
			};
			providerMessageContainer.AddChild(providerMessageWidget);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			libraryView.SelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
			libraryContext.ContainerChanged -= Library_ContainerChanged;
			if (libraryView.ActiveContainer != null)
			{
				libraryView.ActiveContainer.ContentChanged -= UpdateStatus;
			}

			mainViewWidget = null;

			base.OnClosed(e);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y)
				&& mouseEvent.DragFiles?.Count > 0)
			{
				if (libraryView?.ActiveContainer.IsProtected == false)
				{
					// Allow drag-drop if IsLoadable or extension == '.zip'
					mouseEvent.AcceptDrop = mouseEvent.DragFiles?.Count > 0
						&& mouseEvent.DragFiles.TrueForAll(filePath => ApplicationController.Instance.IsLoadableFile(filePath)
							|| (Path.GetExtension(filePath) is string extension
								&& string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
							|| filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase));
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			// TODO: Does this fire when .AcceptDrop is false? Looks like it should
			if (mouseEvent.DragFiles?.Count > 0
				&& libraryView?.ActiveContainer.IsProtected == false)
			{
				var container = libraryView.ActiveContainer as ILibraryWritableContainer;
				container?.Add(mouseEvent.DragFiles.Select(f => new FileSystemFileItem(f)));
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnLoad(EventArgs args)
		{
			// Defer creating menu items until plugins have loaded
			LibraryWidget.CreateMenuActions(libraryView, menuActions, libraryContext, mainViewWidget, theme, allowPrint: true);

			navBar.OverflowButton.Name = "Print Library Overflow Menu";
			navBar.ExtendOverflowMenu = (popupMenu) =>
			{
				// Create menu items in the DropList for each element in this.menuActions
				foreach (var menuAction in menuActions)
				{
					if (menuAction is MenuSeparator)
					{
						popupMenu.CreateSeparator();
					}
					else
					{
						var menuItem = popupMenu.CreateMenuItem(menuAction.Title, menuAction.Icon);
						menuItem.Name = $"{menuAction.Title} Menu Item";
						menuItem.Enabled = menuAction.Action != null && menuAction.IsEnabled(libraryView.SelectedItems, libraryView);
						menuItem.Click += (s, e) =>
						{
							menuAction.Action?.Invoke(libraryView.SelectedItems.Select(i => i.Model), libraryView);
						};
					}
				}
			};

			base.OnLoad(args);
		}

		public bool KeepMenuOpen => this.ContainsFocus;

		public enum ListViewModes
		{
			RowListView,
			IconListView,
			IconListView18,
			IconListView70,
			IconListView256
		}
	}

	public class TextEditWithInlineCancel : GuiWidget
	{
		public ThemedTextEditWidget TextEditWidget { get; }

		public GuiWidget ResetButton { get; }

		public TextEditWithInlineCancel(ThemeConfig theme, string emptyText = null)
		{
			if (emptyText == null)
			{
				emptyText = "Search".Localize();
			}

			this.VAnchor = VAnchor.Center | VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;

			TextEditWidget = new ThemedTextEditWidget("", theme, messageWhenEmptyAndNotSelected: emptyText)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center
			};
			this.AddChild(TextEditWidget);

			this.ResetButton = theme.CreateSmallResetButton();
			ResetButton.HAnchor |= HAnchor.Right;
			ResetButton.VAnchor |= VAnchor.Center;
			ResetButton.Name = "Close Search";
			ResetButton.ToolTipText = "Clear".Localize();

			this.AddChild(ResetButton);
		}

		public override void OnLoad(EventArgs args)
		{
			TextEditWidget.Focus();
			base.OnLoad(args);
		}

		public override string Text
		{
			get => TextEditWidget.ActualTextEditWidget.Text;
			set => TextEditWidget.ActualTextEditWidget.Text = value;
		}
	}
}
