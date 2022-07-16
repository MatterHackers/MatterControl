/*
Copyright (c) 2022, John Lewin, Lars Brubaker
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using static MatterHackers.MatterControl.Library.Widgets.PrintLibraryWidget;

namespace MatterHackers.MatterControl.Library.Widgets
{
	public class LibraryWidget : GuiWidget
	{
		private readonly ILibraryContext libraryContext;
		private readonly LibraryListView libraryView;

		private readonly List<LibraryAction> menuActions = new List<LibraryAction>();

		private readonly FolderBreadCrumbWidget breadCrumbWidget;
		private readonly GuiWidget searchInput;
		private ILibraryContainer searchContainer;

		private MainViewWidget mainViewWidget;
		private readonly ThemeConfig theme;
		private readonly OverflowBar navBar;
		private readonly GuiWidget searchButton;
		private readonly TreeView libraryTreeView;

		public bool ShowContainers { get; private set; } = true;

		public LibraryWidget(MainViewWidget mainViewWidget, ThemeConfig theme)
		{
			this.theme = theme;
			this.mainViewWidget = mainViewWidget;
			this.Padding = 0;
			this.AnchorAll();

			var allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

			libraryContext = ApplicationController.Instance.LibraryTabContext;

			libraryView = new LibraryListView(libraryContext, theme)
			{
				Name = "LibraryView",
				// Drop containers if ShowContainers != 1
				ContainerFilter = (container) => this.ShowContainers,
				BackgroundColor = theme.BackgroundColor,
				Border = new BorderDouble(top: 1),
				DoubleClickBehavior = LibraryListView.DoubleClickBehaviors.PreviewItem
			};

			navBar = new OverflowBar(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			theme.ApplyBottomBorder(navBar);

			breadCrumbWidget = new FolderBreadCrumbWidget(libraryContext, theme);
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
					searchContainer = libraryContext.ActiveContainer;

					breadCrumbWidget.Visible = false;
					searchPanel.Visible = true;
					searchInput.Focus();
				}
			};
			navBar.AddChild(searchButton);

			navBar.AddChild(CreateViewOptionsMenuButton(theme, libraryView, (show) => ShowContainers = show, () => ShowContainers));

			navBar.AddChild(CreateSortingMenuButton(theme, libraryView));

			allControls.AddChild(navBar);

			var horizontalSplitter = new Splitter()
			{
				SplitterDistance = UserSettings.Instance.LibraryViewWidth,
				SplitterSize = theme.SplitterWidth,
				SplitterBackground = theme.SplitterBackground
			};
			horizontalSplitter.AnchorAll();

			horizontalSplitter.DistanceChanged += (s, e) =>
			{
				UserSettings.Instance.LibraryViewWidth = horizontalSplitter.SplitterDistance;
			};

			allControls.AddChild(horizontalSplitter);

			libraryTreeView = new TreeView(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = 5
			};
			libraryTreeView.AfterSelect += async (s, e) =>
			{
				if (libraryTreeView.SelectedNode is ContainerTreeNode treeNode)
				{
					if (!treeNode.ContainerAcquired)
					{
						await this.EnsureExpanded(treeNode.Tag as ILibraryItem, treeNode);
					}

					if (treeNode.ContainerAcquired)
					{
						libraryContext.ActiveContainer = treeNode.Container;
					}
				}
			};
			horizontalSplitter.Panel1.AddChild(libraryTreeView);

			var rootColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(left: 10)
			};
			libraryTreeView.AddChild(rootColumn);

			if (AppContext.IsLoading)
			{
				ApplicationController.StartupActions.Add(new ApplicationController.StartupAction()
				{
					Title = "Initializing Library".Localize(),
					Priority = 0,
					Action = () =>
					{
						this.LoadRootLibraryNodes(rootColumn);
					}
				});
			}
			else
			{
				this.LoadRootLibraryNodes(rootColumn);
			}

			horizontalSplitter.Panel2.AddChild(libraryView);

			allControls.AnchorAll();

			this.AddChild(allControls);

			// Register listeners
			libraryView.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
			libraryContext.ContainerChanged += Library_ContainerChanged;
		}

		public static GuiWidget CreateSortingMenuButton(ThemeConfig theme, LibraryListView libraryView)
		{
			var viewOptionsButton = new PopupMenuButton(
				new ImageWidget(StaticData.Instance.LoadIcon("fa-sort_16.png", 32, 32).SetToColor(theme.TextColor)), theme)
			{
				AlignToRightEdge = true,
				Name = "Print Library View Options",
				ToolTipText = "Sorting".Localize()
			};

			viewOptionsButton.DynamicPopupContent = () =>
			{
				var popupMenu = new PopupMenu(theme);
				CreateSortingMenu(popupMenu, theme, libraryView);
				return popupMenu;
			};

			return viewOptionsButton;
		}

		public static PopupMenu CreateSortingMenu(PopupMenu popupMenu, ThemeConfig theme, LibraryListView libraryView)
		{
			var siblingList = new List<GuiWidget>();

			popupMenu.CreateBoolMenuItem(
				"Date Created".Localize(),
				() => libraryView.ActiveSort.HasFlag(SortKey.CreatedDate),
				(v) => libraryView.ActiveSort = SortKey.CreatedDate,
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			popupMenu.CreateBoolMenuItem(
				"Date Modified".Localize(),
				() => libraryView.ActiveSort.HasFlag(SortKey.ModifiedDate),
				(v) => libraryView.ActiveSort = SortKey.ModifiedDate,
				useRadioStyle: true,
				siblingRadioButtonList: siblingList);

			popupMenu.CreateBoolMenuItem(
				"Name".Localize(),
				() => libraryView.ActiveSort.HasFlag(SortKey.Name),
				(v) => libraryView.ActiveSort = SortKey.Name,
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

			return popupMenu;
		}

		public static GuiWidget CreateViewOptionsMenuButton(ThemeConfig theme,
			LibraryListView libraryView,
			Action<bool> showContainers,
			Func<bool> containersShown)
		{
			var viewMenuButton = new PopupMenuButton(
				new ImageWidget(StaticData.Instance.LoadIcon("mi-view-list_10.png", 32, 32).SetToColor(theme.TextColor))
				{
					// VAnchor = VAnchor.Center
				},
				theme)
			{
				AlignToRightEdge = true,
				ToolTipText = "View Settings".Localize()
			};

			viewMenuButton.DynamicPopupContent = () =>
			{
				var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

				var listView = libraryView;

				var siblingList = new List<GuiWidget>();

				popupMenu.CreateBoolMenuItem(
					"Show Folders".Localize(),
					() => containersShown(),
					(isChecked) =>
					{
						showContainers(isChecked);
						listView.Reload().ConfigureAwait(false);
					});

				popupMenu.CreateSeparator();

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

			return viewMenuButton;
		}

		private void LoadRootLibraryNodes(FlowLayoutWidget rootColumn)
		{
			var rootLibraryContainer = this.GetRootContainer();

			foreach (var libraryContainerLink in rootLibraryContainer.ChildContainers.OrderBy(t => t.Name))
			{
				if (libraryContainerLink.IsVisible)
				{
					var rootNode = this.CreateTreeNode(libraryContainerLink, rootLibraryContainer);
					rootNode.TreeView = libraryTreeView;

					rootColumn.AddChild(rootNode);
				}
			}
		}

		private ILibraryContainer GetRootContainer()
		{
			// Walk up to the root node
			var context = libraryContext.ActiveContainer;
			while (context.Parent != null)
			{
				context = context.Parent;
			}

			return context;
		}

		private async Task GetExpansionItems(ILibraryItem containerItem, ContainerTreeNode treeNode)
		{
			if (containerItem is ILibraryContainerLink containerLink)
			{
				try
				{
					// Container items
					var container = await containerLink.GetContainer(null);
					if (container != null)
					{
						await Task.Run(() =>
						{
							container.Load();
						});

						if (treeNode is ContainerTreeNode containerNode)
						{
							container.Parent = containerNode.ParentContainer;
						}

						foreach (var childContainer in container.ChildContainers)
						{
							treeNode.Nodes.Add(CreateTreeNode(childContainer, container));
						}

						treeNode.Container = container;

						treeNode.AlwaysExpandable = treeNode.Nodes.Count > 0;
						treeNode.Expandable = treeNode.Nodes.Count > 0;
						treeNode.Expanded = treeNode.Nodes.Count > 0;

						treeNode.Invalidate();
					}
				}
				catch { }
			}
		}

		private TreeNode CreateTreeNode(ILibraryItem containerItem, ILibraryContainer parentContainer)
		{
			var treeNode = new ContainerTreeNode(theme, parentContainer)
			{
				Text = containerItem.Name,
				Tag = containerItem,
				AlwaysExpandable = true
			};

			ApplicationController.Instance.Library.LoadItemThumbnail(
				(icon) =>
				{
					treeNode.Image = icon.SetPreMultiply();
				},
				null,
				containerItem,
				null,
				20,
				20,
				theme).ConfigureAwait(false);

			treeNode.ExpandedChanged += (s, e) =>
			{
				this.EnsureExpanded(containerItem, treeNode).ConfigureAwait(false);
			};

			return treeNode;
		}

		public async Task EnsureExpanded(ILibraryItem libraryItem, ContainerTreeNode treeNode)
		{
			if (!treeNode.ContainerAcquired)
			{
				await GetExpansionItems(libraryItem, treeNode).ConfigureAwait(false);
			}
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
			var activeContainer = this.libraryView.ActiveContainer;

			bool containerSupportsEdits = activeContainer is ILibraryWritableContainer;

			var owningNode = libraryTreeView.SelectedNode?.Parents<ContainerTreeNode>().Where(p => p.Container == activeContainer).FirstOrDefault();
			if (owningNode != null)
			{
				libraryTreeView.SelectedNode = owningNode;
			}

			// searchInput.Text = activeContainer.KeywordFilter;
			breadCrumbWidget.SetContainer(activeContainer);

			searchButton.Enabled = activeContainer.Parent != null;
		}

		public static void CreateMenuActions(LibraryListView libraryView, List<LibraryAction> menuActions, ILibraryContext libraryContext, MainViewWidget mainViewWidget, ThemeConfig theme, bool allowPrint)
		{
			menuActions.Add(new LibraryAction(ActionScope.ListView)
			{
				Icon = StaticData.Instance.LoadIcon("cube.png", 16, 16).SetToColor(theme.TextColor),
				Title = "Add".Localize(),
				ToolTipText = "Add an.stl, .obj, .3mf, .amf, .gcode or.zip file to the Library".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					UiThread.RunOnIdle(() =>
					{
						AggContext.FileDialogs.OpenFileDialog(
							new OpenFileDialogParams(ApplicationSettings.OpenDesignFileParams, multiSelect: true),
							(openParams) =>
							{
								if (openParams.FileNames != null)
								{
									if (libraryView.ActiveContainer is ILibraryWritableContainer writableContainer
										&& openParams.FileNames.Length > 0)
									{
										writableContainer.Add(openParams.FileNames.Select(f => new FileSystemFileItem(f)));
									}
								}
							});
					});
				},
				IsEnabled = (s, l) => libraryView.ActiveContainer is ILibraryWritableContainer
			});

			menuActions.Add(new LibraryAction(ActionScope.ListView)
			{
				Title = "Create Folder".Localize() + "...",
				Icon = StaticData.Instance.LoadIcon("fa-folder-new_16.png", 16, 16).SetToColor(theme.TextColor),
				Action = (selectedLibraryItems, listView) =>
				{
					DialogWindow.Show(
						new InputBoxPage(
							"Create Folder".Localize(),
							"Folder Name".Localize(),
							"",
							"Enter New Name Here".Localize(),
							"Create".Localize(),
							(newName) =>
							{
								if (!string.IsNullOrEmpty(newName)
									&& libraryView.ActiveContainer is ILibraryWritableContainer writableContainer)
								{
									writableContainer.Add(new[]
									{
									new CreateFolderItem() { Name = newName }
									});
								}
							}));
				},
				IsEnabled = (s, l) =>
				{
					return libraryView.ActiveContainer is ILibraryWritableContainer writableContainer
						&& writableContainer?.AllowAction(ContainerActions.AddContainers) == true;
				}
			});

			menuActions.Add(new LibraryAction(ActionScope.ListView)
			{
				Title = "Enter Share Code".Localize() + "...",
				Icon = StaticData.Instance.LoadIcon("enter-code.png", 16, 16).SetToColor(theme.TextColor),
				Action = (selectedLibraryItems, listView) =>
				{
					UiThread.RunOnIdle(() =>
					{
						// Implementation already does RunOnIdle
						ApplicationController.Instance.EnterShareCode?.Invoke();
					});
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					return true;
				}
			});

			if (allowPrint)
			{
				menuActions.Add(new LibraryAction(ActionScope.ListItem)
				{
					Title = "Print".Localize(),
					Action = (selectedLibraryItems, listView) =>
					{
						// TODO: Sort out the right way to have an ActivePrinter context that looks and behaves correctly
						var activeContext = ApplicationController.Instance.DragDropData;
						var printer = activeContext.View3DWidget.Printer;

						switch (selectedLibraryItems.FirstOrDefault())
						{
							case SDCardFileItem sdcardItem:
								// TODO: Confirm SD printing?
								// TODO: Need to rewrite library menu item validation can write one off validations like below so we don't end up here
								//  - ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_sd_card_reader)
								printer.Connection.StartSdCardPrint(sdcardItem.Name.ToLower());
								break;
							case FileSystemFileItem fileItem when Path.GetExtension(fileItem.FileName).IndexOf(".gco", StringComparison.OrdinalIgnoreCase) == 0:
								if (printer != null)
								{
									UiThread.RunOnIdle(async () =>
									{
										await printer.Bed.StashAndPrintGCode(fileItem);
									});
								}

								break;
							default:
							// TODO: Otherwise add the selected items to the plate and print the plate?
								if (printer != null)
								{
									UiThread.RunOnIdle(async () =>
									{
										await printer.Bed.StashAndPrint(selectedLibraryItems);
									});
								}

								break;
						}
					},
					IsEnabled = (selectedListItems, listView) =>
					{
						var communicationState = ApplicationController.Instance.DragDropData?.View3DWidget?.Printer?.Connection.CommunicationState;

						// Singleselect - disallow containers
						return listView.SelectedItems.Count == 1
								&& selectedListItems.FirstOrDefault()?.Model is ILibraryItem firstItem
								&& !(firstItem is ILibraryContainer)
								&& (communicationState == CommunicationStates.Connected
									|| communicationState == CommunicationStates.FinishedPrint)
								&& listView.SelectedItems.All(i => !(i.Model is ILibraryContainerLink));
					}
				});
			}

			menuActions.Add(new MenuSeparator("Open"));

			// open menu item
			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Open".Localize(),
				Icon = StaticData.Instance.LoadIcon("cube.png", 16, 16).SetToColor(theme.TextColor),
				Action = (selectedLibraryItems, listView) =>
				{
					listView.SelectedItems.FirstOrDefault()?.OnDoubleClick(null);
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					if (!OnApplicationLibraryTab())
                    {
						return false;
                    }

					// Single select
					var only1 = listView.SelectedItems.Count == 1;
					// mcx only - disallow containers and protected items
					var isAsset = selectedListItems.FirstOrDefault()?.Model is ILibraryItem libraryItem
								&& !(libraryItem is ILibraryContainer) // containers are also items
								&& !libraryItem.IsProtected
								// and we can only edit it if it is an mcx (can't edit stls)
								&& libraryItem is ILibraryAsset asset && asset.ContentType == "mcx";

					// Check if it is writeable
					var writableContainer = !libraryContext.ActiveContainer.IsProtected
								&& libraryContext.ActiveContainer is ILibraryWritableContainer;

					var isFolder = listView.SelectedItems.FirstOrDefault()?.Model is DynamicContainerLink;
					return only1 && ((isAsset && writableContainer) || isFolder);
				}
			});

			// Open a copy menu item
			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Open a copy".Localize(),
				Icon = StaticData.Instance.LoadIcon("cube_add.png", 16, 16).SetToColor(theme.TextColor),
				Action = (selectedLibraryItems, listView) =>
				{
					ApplicationController.Instance.OpenIntoNewTab(selectedLibraryItems);
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					if (!OnApplicationLibraryTab())
					{
						return false;
					}

					var isFolder = listView.SelectedItems.FirstOrDefault()?.Model is DynamicContainerLink;

					return listView.SelectedItems.Count == 1
						&& (listView.SelectedItems.FirstOrDefault().Container is ILibraryContainerLink
							|| listView.SelectedItems.FirstOrDefault().Container is ILibraryContainer)
						&& !isFolder;
				}
			});

			// edit menu item
			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Add to Bed".Localize(),
				Icon = StaticData.Instance.LoadIcon("bed_add.png", 16, 16).SetToColor(theme.TextColor),
				Action = async (selectedLibraryItems, listView) =>
				{
					var activeContext = ApplicationController.Instance.DragDropData;
					var printer = activeContext.View3DWidget.Printer;

					if (listView.SelectedItems.Count == 1 &&
						selectedLibraryItems.FirstOrDefault() is ILibraryAssetStream assetStream
						&& assetStream.ContentType == "gcode")
					{
						await ApplicationController.Instance.Tasks.Execute(
							"Loading".Localize() + "...",
							null,
							async (reporter, cancellationTokenSource) =>
							{
								var progressStatus = new ProgressStatus();

								// Change loaded scene to new context
								await printer.Bed.LoadContent(
									new EditContext()
									{
										SourceItem = assetStream,
										// No content store for GCode
										ContentStore = null
									},
									(progress, message) =>
									{
										progressStatus.Progress0To1 = progress;
										progressStatus.Status = message;
										reporter.Report(progressStatus);
									}).ConfigureAwait(false);
							});
					}
					else
					{
						activeContext.SceneContext.AddToPlate(selectedLibraryItems);
					}

					if (OnApplicationLibraryTab())
					{
						ApplicationController.Instance.BlinkTab(
							ApplicationController.Instance.MainView.TabControl.AllTabs.FirstOrDefault(t => t.TabContent is DesignTabPage));
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					var isFolder = listView.SelectedItems.FirstOrDefault()?.Model is DynamicContainerLink;

					// Multiselect - disallow containers, require View3DWidget context
					return ApplicationController.Instance.DragDropData.View3DWidget != null
						&& listView.SelectedItems.Any()
						&& listView.SelectedItems.All(i => !(i.Model is ILibraryContainerLink))
						&& !isFolder
						&& ApplicationController.Instance.MainView.TabControl.AllTabs.Any(t => t.TabContent is DesignTabPage);
				}
			});

			menuActions.Add(new MenuSeparator("Export"));

			// export menu item
			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Export".Localize(),
				Icon = StaticData.Instance.LoadIcon("cube_export.png", 16, 16).SetToColor(theme.TextColor),
				Action = (selectedLibraryItems, listView) =>
				{
					ApplicationController.Instance.ExportLibraryItems(libraryView.SelectedItems.Select(item => item.Model));
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect - disallow containers
					return listView.SelectedItems.Any()
						&& listView.SelectedItems.All(i => !(i.Model is ILibraryContainerLink));
				},
			});

			// share menu item
			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Share".Localize() + "...",
				Icon = StaticData.Instance.LoadIcon("share.png", 16, 16).SetToColor(theme.TextColor),
				Action = (selectedLibraryItems, listView) =>
				{
					// Previously - shareFromLibraryButton_Click
					// TODO: Should be rewritten to Register from cloudlibrary, include logic to add to library as needed
					ApplicationController.Instance.ShareLibraryItem(libraryView.SelectedItems.Select(i => i.Model).FirstOrDefault());
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Singleselect - disallow containers and protected items
					return listView.SelectedItems.Count == 1
						&& selectedListItems.FirstOrDefault()?.Model is ILibraryItem firstItem
						&& listView.ActiveContainer.GetType().Name.IndexOf("Cloud", StringComparison.OrdinalIgnoreCase) >= 0
						&& !(firstItem is ILibraryContainer)
						&& !firstItem.IsProtected;
				}
			});

			menuActions.Add(new MenuSeparator("Rename"));
	
			// rename menu item
			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Rename".Localize(),
				Icon = StaticData.Instance.LoadIcon("icon_edit.png", 16, 16).SetToColor(theme.TextColor),
				Action = (selectedLibraryItems, listView) =>
				{
					if (libraryView.SelectedItems.Count == 1)
					{
						var selectedItem = libraryView.SelectedItems.FirstOrDefault();
						if (selectedItem == null)
						{
							return;
						}

						selectedItem.Model.Rename();
						libraryView.SelectedItems.Clear();
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Singleselect, WritableContainer - disallow protected items
					return listView.SelectedItems.Count == 1
						&& selectedListItems.FirstOrDefault()?.Model is ILibraryItem firstItem
						&& !firstItem.IsProtected
						&& libraryContext.ActiveContainer is ILibraryWritableContainer;
				}
			});

			// move menu item
			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Move".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					var partItems = selectedLibraryItems.Where(item => item is ILibraryAssetStream || item is ILibraryContainerLink);
					if (partItems.Any()
						&& libraryView.ActiveContainer is ILibraryWritableContainer sourceContainer)
					{
						DialogWindow.Show(new MoveItemPage((destinationContainer, newName) =>
						{
							destinationContainer.Move(partItems, sourceContainer);

							// Discover if item was moved to an already loaded and now stale view on an ancestor and force reload
							var openParent = libraryContext.ActiveContainer.Ancestors().FirstOrDefault(c => c.ID == destinationContainer.ID);
							if (openParent != null)
							{
								// TODO: Consider changing this brute force approach to instead mark as dirty and allow Activate base method to reload if dirty
								Task.Run(() => openParent.Load());
							}

							libraryView.SelectedItems.Clear();
						}));
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect, WritableContainer - disallow protected
					return listView.SelectedItems.Any()
						&& listView.SelectedItems.All(i => !i.Model.IsProtected
						&& libraryContext.ActiveContainer is ILibraryWritableContainer);
				}
			});

			// show on disk menu item
			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Show in Explorer".Localize(),
				// Icon = StaticData.Instance.LoadIcon("remove.png", 16, 16).SetToColor(theme.TextColor),
				Action = (selectedLibraryItems, listView) =>
				{
					if (AggContext.OperatingSystem == OSType.Windows)
					{
						if (AggContext.OperatingSystem == OSType.Windows
							&& listView.SelectedItems.Count() == 1)
						{
							if (listView.SelectedItems.FirstOrDefault().Model is FileSystemFileItem fileItem)
							{
								Process.Start("explorer.exe", $"/select, \"{fileItem.FilePath}\"");
							}
							else if (listView.SelectedItems.FirstOrDefault().Model is FileSystemContainer.DirectoryContainerLink container)
							{
								Process.Start("explorer.exe", $"/select, \"{container.FilePath}\"");
							}
							else if (listView.SelectedItems.FirstOrDefault().Model is LocalZipContainerLink zipContainer)
							{
								Process.Start("explorer.exe", $"/select, \"{zipContainer.FilePath}\"");
							}
						}
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					if (AggContext.OperatingSystem == OSType.Windows
						&& listView.SelectedItems.Count() == 1
						&& listView.SelectedItems.FirstOrDefault().Model is FileSystemItem)
					{
						return true;
					}

					return false;
				}
			});

			// remove menu item
			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Remove".Localize(),
				Icon = StaticData.Instance.LoadIcon("remove.png", 16, 16).SetToColor(theme.TextColor),
				Action = (selectedLibraryItems, listView) =>
				{
					// Previously - deleteFromLibraryButton_Click

					// ask before remove
					var libraryItems = libraryView.SelectedItems.Select(p => p.Model);
					if (libraryItems.Any())
					{
						if (libraryView.ActiveContainer is ILibraryWritableContainer container)
						{
							StyledMessageBox.ShowMessageBox(
								(doDelete) =>
								{
									if (doDelete)
									{
										container.Remove(libraryItems);
										libraryView.SelectedItems.Clear();
									}
								},
								"Are you sure you want to remove the currently selected items?".Localize(),
								"Remove Items?".Localize(),
								StyledMessageBox.MessageType.YES_NO,
								"Remove".Localize());
						}
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect, WritableContainer - disallow protected
					return listView.SelectedItems.Any()
						&& listView.SelectedItems.All(i => !i.Model.IsProtected
						&& libraryContext.ActiveContainer is ILibraryWritableContainer);
				}
			});

			// Extension point - RegisteredLibraryActions not defined in this file/assembly can insert here via this named token
			menuActions.AddRange(ApplicationController.Instance.RegisteredLibraryActions("StandardLibraryOperations"));

			menuActions.Add(new MenuSeparator("Other"));

			foreach (var extension in ApplicationController.Instance.Library.MenuExtensions)
			{
				menuActions.Add(extension);
			}

			menuActions.Add(new LibraryAction(ActionScope.ListItem)
			{
				Title = "Open Package".Localize(),
				Action = async (selectedItems, listView) =>
				{
					var firstItem = selectedItems.First();

					if (firstItem is ILibraryAsset libraryAsset)
					{
						var object3D = await libraryAsset.CreateContent(null);

						var container = new McxContainer(object3D);
						container.Load();

						container.Parent = libraryContext.ActiveContainer;

						libraryContext.ActiveContainer = container;
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					return listView.SelectedItems.Count == 1
					&& selectedListItems.FirstOrDefault()?.Model is ILibraryAsset libraryAsset
					&& libraryAsset.ContentType == "mcx";
				}
			});

			libraryView.MenuActions = menuActions;
		}

        private static bool OnApplicationLibraryTab()
        {
			if (ApplicationController.Instance.MainTabKey == "Library")
            {
				return true;
            }
		
			return false;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			libraryView.SelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
			libraryContext.ContainerChanged -= Library_ContainerChanged;

			mainViewWidget = null;

			base.OnClosed(e);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.XButton1)
			{
				// user pressed the back button
				NavigateBack();
			}

			base.OnMouseDown(mouseEvent);
		}

		public void NavigateBack()
		{
			breadCrumbWidget.NavigateBack();
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
			LibraryWidget.CreateMenuActions(libraryView, menuActions, libraryContext, mainViewWidget, theme, allowPrint: false);

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
	}
}
