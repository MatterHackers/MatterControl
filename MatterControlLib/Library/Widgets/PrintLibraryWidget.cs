/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class PrintLibraryWidget : GuiWidget, IIgnoredPopupChild
	{
		private FlowLayoutWidget buttonPanel;
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

		public PrintLibraryWidget(MainViewWidget mainViewWidget, ThemeConfig theme, PopupMenuButton popupMenuButton)
		{
			this.theme = theme;
			this.mainViewWidget = mainViewWidget;
			this.Padding = 0;
			this.AnchorAll();

			var allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var libaryContext = ApplicationController.Instance.Library;

			libraryView = new LibraryListView(libaryContext, theme)
			{
				Name = "LibraryView",
				// Drop containers if ShowContainers != 1
				ContainerFilter = (container) => UserSettings.Instance.ShowContainers,
				BackgroundColor = theme.BackgroundColor,
				Border = new BorderDouble(top: 1)
			};

			libraryView.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;

			ApplicationController.Instance.Library.ContainerChanged += Library_ContainerChanged;

			navBar = new OverflowBar(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			allControls.AddChild(navBar);
			theme.ApplyBottomBorder(navBar);

			var toolbar = new OverflowBar(AggContext.StaticData.LoadIcon("fa-sort_16.png", 32, 32, theme.InvertIcons), theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Name = "Folders Toolbar"
			};

			theme.ApplyBottomBorder(toolbar, shadedBorder: true);

			toolbar.OverflowButton.Name = "Print Library View Options";
			toolbar.Padding = theme.ToolbarPadding;

			toolbar.ExtendOverflowMenu = (popupMenu) =>
			{
				var siblingList = new List<GuiWidget>();

				popupMenu.CreateBoolMenuItem(
					"Date Created".Localize(),
					() => libraryView.ActiveSort == LibraryListView.SortKey.CreatedDate,
					(v) => libraryView.ActiveSort = LibraryListView.SortKey.CreatedDate,
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);

				popupMenu.CreateBoolMenuItem(
					"Date Modified".Localize(),
					() => libraryView.ActiveSort == LibraryListView.SortKey.ModifiedDate,
					(v) => libraryView.ActiveSort = LibraryListView.SortKey.ModifiedDate,
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);

				popupMenu.CreateBoolMenuItem(
					"Name".Localize(),
					() => libraryView.ActiveSort == LibraryListView.SortKey.Name,
					(v) => libraryView.ActiveSort = LibraryListView.SortKey.Name,
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);

				popupMenu.CreateSeparator();

				siblingList = new List<GuiWidget>();

				popupMenu.CreateBoolMenuItem(
					"Ascending".Localize(),
					() => libraryView.Ascending,
					(v) => libraryView.Ascending = true,
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);

				popupMenu.CreateBoolMenuItem(
					"Descending".Localize(),
					() => !libraryView.Ascending,
					(v) => libraryView.Ascending = false,
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);
			};

			allControls.AddChild(toolbar);

			var showFolders = new ExpandCheckboxButton("Folders".Localize(), theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Center,
				Margin = theme.ButtonSpacing,
				Name = "Show Folders Toggle",
				Checked = UserSettings.Instance.ShowContainers,
			};
			showFolders.SetIconMargin(theme.ButtonSpacing);
			showFolders.CheckedStateChanged += async (s, e) =>
			{
				UserSettings.Instance.set(UserSettingsKey.ShowContainers, showFolders.Checked ? "1" : "0");
				await libraryView.Reload();
			};
			toolbar.AddChild(showFolders);

			PopupMenuButton viewMenuButton;

			toolbar.AddChild(
				viewMenuButton = new PopupMenuButton(
					new ImageWidget(AggContext.StaticData.LoadIcon("mi-view-list_10.png", 32, 32, theme.InvertIcons))
					{
						//VAnchor = VAnchor.Center
					},
					theme)
				{
					AlignToRightEdge = true
				});

			viewMenuButton.DynamicPopupContent = () =>
			{
				var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

				var listView = this.libraryView;

				var siblingList = new List<GuiWidget>();

				popupMenu.CreateBoolMenuItem(
					"View List".Localize(),
					() => ApplicationController.Instance.ViewState.LibraryViewMode == ListViewModes.RowListView,
					(isChecked) =>
					{
						ApplicationController.Instance.ViewState.LibraryViewMode = ListViewModes.RowListView;
						listView.ListContentView = new RowListView(theme);
						listView.Reload().ConfigureAwait(false);
					},
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);
#if DEBUG
				popupMenu.CreateBoolMenuItem(
					"View XSmall Icons".Localize(),
					() => ApplicationController.Instance.ViewState.LibraryViewMode == ListViewModes.IconListView18,
					(isChecked) =>
					{
						ApplicationController.Instance.ViewState.LibraryViewMode = ListViewModes.IconListView18;
						listView.ListContentView = new IconListView(theme, 18);
						listView.Reload().ConfigureAwait(false);
					},
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);

				popupMenu.CreateBoolMenuItem(
					"View Small Icons".Localize(),
					() => ApplicationController.Instance.ViewState.LibraryViewMode == ListViewModes.IconListView70,
					(isChecked) =>
					{
						ApplicationController.Instance.ViewState.LibraryViewMode = ListViewModes.IconListView70;
						listView.ListContentView = new IconListView(theme, 70);
						listView.Reload().ConfigureAwait(false);
					},
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);
#endif
				popupMenu.CreateBoolMenuItem(
					"View Icons".Localize(),
					() => ApplicationController.Instance.ViewState.LibraryViewMode == ListViewModes.IconListView,
					(isChecked) =>
					{
						ApplicationController.Instance.ViewState.LibraryViewMode = ListViewModes.IconListView;
						listView.ListContentView = new IconListView(theme);
						listView.Reload().ConfigureAwait(false);
					},
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);

				popupMenu.CreateBoolMenuItem(
					"View Large Icons".Localize(),
					() => ApplicationController.Instance.ViewState.LibraryViewMode == ListViewModes.IconListView256,
					(isChecked) =>
					{
						ApplicationController.Instance.ViewState.LibraryViewMode = ListViewModes.IconListView256;
						listView.ListContentView = new IconListView(theme, 256);
						listView.Reload().ConfigureAwait(false);
					},
					useRadioStyle: true,
					siblingRadioButtonList: siblingList);

				return popupMenu;
			};

			breadCrumbWidget = new FolderBreadCrumbWidget(libaryContext, theme);
			navBar.AddChild(breadCrumbWidget);

			var searchPanel = new SearchInputBox(theme)
			{
				Visible = false,
				Margin = new BorderDouble(10, 0, 5, 0),
			};
			searchPanel.searchInput.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				this.PerformSearch();
			};
			searchPanel.ResetButton.Click += (s, e) =>
			{
				breadCrumbWidget.Visible = true;
				searchPanel.Visible = false;

				searchPanel.searchInput.Text = "";

				this.ClearSearch();
			};

			// Store a reference to the input field
			this.searchInput = searchPanel.searchInput;

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
					searchContainer = ApplicationController.Instance.Library.ActiveContainer;

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
		}

		private void PerformSearch()
		{
			UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.Library.ActiveContainer.KeywordFilter = searchInput.Text.Trim();
			});
		}

		private void ClearSearch()
		{
			if (searchContainer == null)
			{
				return;
			}

			UiThread.RunOnIdle(() =>
			{
				searchContainer.KeywordFilter = "";

				// Restore the original ActiveContainer before search started - some containers may change context
				ApplicationController.Instance.Library.ActiveContainer = searchContainer;

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

			searchInput.Text = activeContainer.KeywordFilter;
			breadCrumbWidget.SetContainer(activeContainer);

			activeContainer.ContentChanged += UpdateStatus;

			searchButton.Enabled = activeContainer.Parent != null;

			UpdateStatus(null, null);
		}

		private void UpdateStatus(object sender, EventArgs e)
		{
			string message = this.libraryView.ActiveContainer?.StatusMessage;
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
			buttonPanel.RemoveAllChildren();

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
			if (libraryView?.ActiveContainer != null)
			{
				libraryView.ActiveContainer.ContentChanged -= UpdateStatus;
				ApplicationController.Instance.Library.ContainerChanged -= Library_ContainerChanged;
			}

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
							&& string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase)));
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
			LibraryWidget.CreateMenuActions(libraryView, menuActions, mainViewWidget, theme, allowPrint: true);

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

	public class SearchInputBox : GuiWidget
	{
		internal MHTextEditWidget searchInput;
		public GuiWidget ResetButton { get; }

		public SearchInputBox(ThemeConfig theme, string emptyText = null)
		{
			this.VAnchor = VAnchor.Center | VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;

			searchInput = new MHTextEditWidget("", theme, messageWhenEmptyAndNotSelected: emptyText ?? "Search".Localize())
			{
				Name = "Search Library Edit",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center
			};
			this.AddChild(searchInput);

			var resetButton = theme.CreateSmallResetButton();
			resetButton.HAnchor |= HAnchor.Right;
			resetButton.VAnchor |= VAnchor.Center;
			resetButton.Name = "Close Search";
			resetButton.ToolTipText = "Clear".Localize();

			this.AddChild(resetButton);

			this.ResetButton = resetButton;
		}

		public override string Text
		{
			get => searchInput.ActualTextEditWidget.Text;
			set => searchInput.ActualTextEditWidget.Text = value;
		}
	}
}
