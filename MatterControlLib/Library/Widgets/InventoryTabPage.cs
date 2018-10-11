/*
Copyright (c) 2018, John Lewin
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

using System.Linq;
using System.Threading.Tasks;

using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class InventoryTabPage : GuiWidget
	{
		private GuiWidget searchInput;

		private ThemeConfig theme;
		private OverflowBar navBar;
		private GuiWidget searchButton;
		private TreeView treeView;

		public InventoryTabPage(ThemeConfig theme)
		{
			this.theme = theme;
			this.Padding = 0;
			this.AnchorAll();

			var allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

			navBar = new OverflowBar(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Visible = false
			};
			allControls.AddChild(navBar);
			theme.ApplyBottomBorder(navBar);

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
					searchPanel.Visible = true;
					searchInput.Focus();
				}
			};
			navBar.AddChild(searchButton);

			PopupMenuButton viewOptionsButton;

			navBar.AddChild(
				viewOptionsButton = new PopupMenuButton(
					new ImageWidget(AggContext.StaticData.LoadIcon("fa-sort_16.png", 32, 32, theme.InvertIcons))
					{
						//VAnchor = VAnchor.Center
					},
					theme)
				{
					AlignToRightEdge = true,
					Name = "Print Library View Options"
				});

			viewOptionsButton.DynamicPopupContent = () =>
			{
				var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

				popupMenu.CreateMenuItem("xxx");

				return popupMenu;
			};

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

			treeView = new InventoryTreeView(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Width = 300,
				Margin = 5
			};

			treeView.NodeMouseDoubleClick += (s, e) =>
			{
				if (e is MouseEventArgs mouseEvent
					&& s is GuiWidget clickedWidget
					&& mouseEvent.Button == MouseButtons.Left
						&& mouseEvent.Clicks == 2)
				{
					if (treeView?.SelectedNode.Tag is PrinterInfo printerInfo)
					{
						// Open printer
						PrinterDetails.SwitchPrinters(printerInfo.ID);
					}
				}
			};

			treeView.NodeMouseClick += (s, e) =>
			{
				if (e is MouseEventArgs mouseEvent
					&& s is GuiWidget clickedWidget
					&& mouseEvent.Button == MouseButtons.Right)
				{
					UiThread.RunOnIdle(() =>
					{
						var menu = new PopupMenu(ApplicationController.Instance.MenuTheme);

						var openMenuItem = menu.CreateMenuItem("Open".Localize());
						openMenuItem.Click += (s2, e2) =>
						{
							if (treeView?.SelectedNode.Tag is PrinterInfo printerInfo)
							{
									// Open printer
									PrinterDetails.SwitchPrinters(printerInfo.ID);
							}
						};

						menu.CreateHorizontalLine();

						var deleteMenuItem = menu.CreateMenuItem("Delete".Localize());
						deleteMenuItem.Click += (s2, e2) =>
						{
								// Delete printer
								StyledMessageBox.ShowMessageBox(
									(deletePrinter) =>
									{
								if (deletePrinter)
								{
									if (treeView.SelectedNode.Tag is PrinterInfo printerInfo)
									{
										ProfileManager.Instance.DeletePrinter(printerInfo.ID, true);
									}
								}
							},
									"Are you sure you want to delete your currently selected printer?".Localize(),
									"Delete Printer?".Localize(),
									StyledMessageBox.MessageType.YES_NO,
									"Delete Printer".Localize());
						};


						var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
						systemWindow.ShowPopup(
							new MatePoint(clickedWidget)
							{
								Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
								AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
							},
							new MatePoint(menu)
							{
								Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
								AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
							},
							altBounds: new RectangleDouble(mouseEvent.X + 1, mouseEvent.Y + 1, mouseEvent.X + 1, mouseEvent.Y + 1));
					});
				}
			};


			treeView.ScrollArea.HAnchor = HAnchor.Stretch;

			treeView.AfterSelect += async (s, e) =>
			{
				if (treeView.SelectedNode.Tag is PrinterInfo printerInfo)
				{
					horizontalSplitter.Panel2.CloseAllChildren();
					horizontalSplitter.Panel2.AddChild(new PrinterDetails(printerInfo, theme)
					{
						HAnchor = HAnchor.MaxFitOrStretch,
						VAnchor = VAnchor.Stretch,
						Padding = theme.DefaultContainerPadding
					});
				}
			};
			horizontalSplitter.Panel1.AddChild(treeView);

			horizontalSplitter.Panel2.AddChild(new GuiWidget()
			{
				HAnchor =HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				BackgroundColor = theme.AccentMimimalOverlay
			});

			allControls.AnchorAll();

			this.AddChild(allControls);
		}

		private async Task GetExpansionItems(ILibraryItem containerItem, ContainerTreeNode treeNode)
		{
			if (containerItem is ILibraryContainerLink containerLink)
			{
				// Prevent invalid assignment of container.Parent due to overlapping load attempts that
				// would otherwise result in containers with self referencing parent properties
				//if (loadingContainerLink != containerLink)
				//{
				//	loadingContainerLink = containerLink;

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

						if (treeNode.NodeParent is ContainerTreeNode parentNode)
						{
							container.Parent = parentNode.Container;
						}

						foreach (var childContainer in container.ChildContainers)
						{
							treeNode.Nodes.Add(CreateTreeNode(childContainer));
						}

						treeNode.Container = container;

						treeNode.AlwaysExpandable = treeNode.Nodes.Count > 0;
						treeNode.Expandable = treeNode.Nodes.Count > 0;
						treeNode.Expanded = treeNode.Nodes.Count > 0;

						treeNode.Invalidate();

						this.BackgroundColor = Color.Transparent;

						//	container.Parent = ActiveContainer;
						// SetActiveContainer(container);
					}
				}
				catch { }
				finally
				{
					// Clear the loading guard and any completed load attempt
					// loadingContainerLink = null;
				}
				///////////////////}
			}
		}

		private TreeNode CreateTreeNode(ILibraryItem containerItem)
		{
			var treeNode = new ContainerTreeNode(theme)
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
				16,
				16,
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
				ApplicationController.Instance.Library.ActiveContainer.KeywordFilter = searchInput.Text.Trim();
			});
		}

		private void ClearSearch()
		{
		}
	}
}
