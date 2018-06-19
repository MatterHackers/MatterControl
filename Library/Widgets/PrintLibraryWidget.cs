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

#if !__ANDROID__
using Markdig.Wpf;
#endif

using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class PrintLibraryWidget : GuiWidget
	{
		private FlowLayoutWidget buttonPanel;
		private ListView libraryView;
		private GuiWidget providerMessageContainer;
		private TextWidget providerMessageWidget;

		private List<PrintItemAction> menuActions = new List<PrintItemAction>();

		private FolderBreadCrumbWidget breadCrumbWidget;
		private GuiWidget searchInput;
		private ILibraryContainer searchContainer;

		private PartPreviewContent partPreviewContent;
		private ThemeConfig theme;
		private OverflowBar navBar;
		private GuiWidget searchButton;

		public PrintLibraryWidget(PartPreviewContent partPreviewContent, ThemeConfig theme)
		{
			this.theme = theme;
			this.partPreviewContent = partPreviewContent;
			this.Padding = 0;
			this.AnchorAll();

			var allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

			libraryView = new ListView(ApplicationController.Instance.Library, theme)
			{
				Name = "LibraryView",
				// Drop containers if ShowContainers != 1
				ContainerFilter = (container) => UserSettings.Instance.get(UserSettingsKey.ShowContainers) == "1",
				BackgroundColor = theme.ActiveTabColor,
				//BorderColor = theme.MinimalShade,
				Border = new BorderDouble(top: 1)
			};

			ApplicationController.Instance.Library.ActiveViewWidget = libraryView;

			libraryView.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;

			ApplicationController.Instance.Library.ContainerChanged += Library_ContainerChanged;

			navBar = new OverflowBar(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			allControls.AddChild(navBar);

			navBar.OverflowButton.BeforePopup += (s, e) =>
			{
				this.EnableMenus();
			};

			allControls.AddChild(new HorizontalLine(20), 1);

			var toolbar = new OverflowBar(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Name = "Folders Toolbar"
			};

			// Change the overflow button to a sort icon
			var firstChild = toolbar.OverflowButton.Children<ImageWidget>().FirstOrDefault();
			firstChild.Image = AggContext.StaticData.LoadIcon("fa-sort_16.png", 32, 32, theme.InvertIcons);

			toolbar.OverflowButton.Name = "Print Library View Options";
			toolbar.Padding = theme.ToolbarPadding;

			toolbar.ExtendOverflowMenu = (popupMenu) =>
			{
				var siblingList = new List<GuiWidget>();

				popupMenu.CreateBoolMenuItem(
					"Date Created".Localize(),
					() => libraryView.ActiveSort == ListView.SortKey.CreatedDate,
					(v) => libraryView.ActiveSort = ListView.SortKey.CreatedDate,
					useRadioStyle: true,
					SiblingRadioButtonList: siblingList);

				popupMenu.CreateBoolMenuItem(
					"Date Modified".Localize(),
					() => libraryView.ActiveSort == ListView.SortKey.ModifiedDate,
					(v) => libraryView.ActiveSort = ListView.SortKey.ModifiedDate,
					useRadioStyle: true,
					SiblingRadioButtonList: siblingList);

				popupMenu.CreateBoolMenuItem(
					"Name".Localize(),
					() => libraryView.ActiveSort == ListView.SortKey.Name,
					(v) => libraryView.ActiveSort = ListView.SortKey.Name,
					useRadioStyle: true,
					SiblingRadioButtonList: siblingList);

				popupMenu.CreateHorizontalLine();

				siblingList = new List<GuiWidget>();

				popupMenu.CreateBoolMenuItem(
					"Ascending".Localize(),
					() => libraryView.Ascending,
					(v) => libraryView.Ascending = true,
					useRadioStyle: true,
					SiblingRadioButtonList: siblingList);

				popupMenu.CreateBoolMenuItem(
					"Descending".Localize(),
					() => !libraryView.Ascending,
					(v) => libraryView.Ascending = false,
					useRadioStyle: true,
					SiblingRadioButtonList: siblingList);
			};

			allControls.AddChild(toolbar);

			var showFolders = new ExpandCheckboxButton("Folders".Localize(), theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Center,
				Margin = theme.ButtonSpacing,
				Name = "Show Folders Toggle",
				Checked = UserSettings.Instance.get(UserSettingsKey.ShowContainers) == "1",
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
					SiblingRadioButtonList: siblingList);
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
					SiblingRadioButtonList: siblingList);


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
					SiblingRadioButtonList: siblingList);
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
					SiblingRadioButtonList: siblingList);

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
					SiblingRadioButtonList: siblingList);

				return popupMenu;
			};

			breadCrumbWidget = new FolderBreadCrumbWidget(libraryView);
			navBar.AddChild(breadCrumbWidget);

			var searchPanel = new SearchInputBox()
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
				TextColor = ActiveTheme.Instance.SecondaryTextColor,
				Margin = new BorderDouble(6),
				AutoExpandBoundsToText = true,
			};
			providerMessageContainer.AddChild(providerMessageWidget);
		}

		private void CreateMenuActions()
		{
			menuActions.Add(new PrintItemAction()
			{
				Icon = AggContext.StaticData.LoadIcon("cube.png", 16, 16, ApplicationController.Instance.MenuTheme.InvertIcons),
				Title = "Add".Localize(),
				ToolTipText = "Add an.stl, .obj, .amf, .gcode or.zip file to the Library".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					UiThread.RunOnIdle(() =>
					{
						AggContext.FileDialogs.OpenFileDialog(
							new OpenFileDialogParams(ApplicationSettings.OpenPrintableFileParams, multiSelect: true),
							(openParams) =>
							{
								if (openParams.FileNames != null)
								{
									var writableContainer = this.libraryView.ActiveContainer as ILibraryWritableContainer;
									if (writableContainer != null
										&& openParams.FileNames.Length > 0)
									{
										writableContainer.Add(openParams.FileNames.Select(f => new FileSystemFileItem(f)));
									}
								}
							});
					});
				},
				IsEnabled = (s, l) => this.libraryView.ActiveContainer is ILibraryWritableContainer
			});

			menuActions.Add(new PrintItemAction()
			{
				Title = "Create Folder".Localize(),
				Icon = AggContext.StaticData.LoadIcon("fa-folder-new_16.png", 16, 16, ApplicationController.Instance.MenuTheme.InvertIcons),
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
									&& this.libraryView.ActiveContainer is ILibraryWritableContainer writableContainer)
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
					return this.libraryView.ActiveContainer is ILibraryWritableContainer writableContainer
						&& writableContainer?.AllowAction(ContainerActions.AddContainers) == true;
				}
			});

			menuActions.Add(new PrintItemAction()
			{

				Title = "Print".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					// TODO: Sort out the right way to have an ActivePrinter context that looks and behaves correctly
					var activeContext = ApplicationController.Instance.DragDropData;
					var printer = activeContext.Printer;

					switch (selectedLibraryItems.FirstOrDefault())
					{
						case SDCardFileItem sdcardItem:
							// TODO: Confirm SD printing?
							// TODO: Need to rewrite library menu item validation can write one off validations like below so we don't end up here
							//  - ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_sd_card_reader)
							printer.Connection.StartSdCardPrint(sdcardItem.Name.ToLower());
							break;
						case FileSystemFileItem fileItem when Path.GetExtension(fileItem.FileName).ToUpper() == ".GCODE":
							//ApplicationController.Instance.ActivePrintItem = new PrintItemWrapper(new PrintItem(fileItem.Name, fileItem.Path));
							break;
						default:
							//TODO: Otherwise add the selected items to the plate and print the plate?
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
					var communicationState = ApplicationController.Instance.DragDropData?.Printer?.Connection.CommunicationState;

					// Singleselect - disallow containers
					return listView.SelectedItems.Count == 1
						&& selectedListItems.FirstOrDefault()?.Model is ILibraryItem firstItem
						&& !(firstItem is ILibraryContainer)
						&& (communicationState == CommunicationStates.Connected
							|| communicationState == CommunicationStates.FinishedPrint);
				}
			});

			// edit menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Add to Plate".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					// TODO: Sort out the right way to have an ActivePrinter context that looks and behaves correctly
					var activeContext = ApplicationController.Instance.DragDropData;
					activeContext.SceneContext.AddToPlate(selectedLibraryItems);
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect - disallow containers
					return listView.SelectedItems.Any()
						&& listView.SelectedItems.All(i => !(i.Model is ILibraryContainer));
				}
			});

#if !__ANDROID__
			// edit menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "MarkDown".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					DialogWindow.Show<MarkdownPage>();
				},
				IsEnabled = (selectedListItems, listView) => true
			});
#endif

			// edit menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Edit".Localize(),
				Action = async (selectedLibraryItems, listView) =>
				{
					if (selectedLibraryItems.FirstOrDefault() is ILibraryItem firstItem
						&& ApplicationController.Instance.Library.ActiveContainer is ILibraryWritableContainer writableContainer)
					{
						BedConfig bed;

						var newTab = partPreviewContent.CreatePartTab(
							firstItem.Name,
							bed = new BedConfig(),
							theme);

						// Load content after UI widgets to support progress notification during acquire/load
						await bed.LoadContent(
							new EditContext()
							{
								ContentStore = writableContainer,
								SourceItem = firstItem
							});

						if (newTab.TabContent is PartTabPage partTab)
						{
							// TODO: Restore ability to render progress loading
						}
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Singleselect, WritableContainer, mcx only - disallow containers and protected items
					return listView.SelectedItems.Count == 1
						&& selectedListItems.FirstOrDefault()?.Model is ILibraryItem firstItem
						&& !(firstItem is ILibraryContainer)
						&& !firstItem.IsProtected
						&& firstItem is ILibraryAsset asset && asset.ContentType == "mcx"
						&& ApplicationController.Instance.Library.ActiveContainer is ILibraryWritableContainer;
				}
			});

			// rename menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Rename".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					if (libraryView.SelectedItems.Count == 1)
					{
						var selectedItem = libraryView.SelectedItems.FirstOrDefault();
						if (selectedItem == null)
						{
							return;
						}

						DialogWindow.Show(
							new InputBoxPage(
								"Rename Item".Localize(),
								"Name".Localize(),
								selectedItem.Model.Name,
								"Enter New Name Here".Localize(),
								"Rename".Localize(),
								(newName) =>
								{
									var model = libraryView.SelectedItems.FirstOrDefault()?.Model;
									if (model != null)
									{
										var container = libraryView.ActiveContainer as ILibraryWritableContainer;
										if (container != null)
										{
											container.Rename(model, newName);
											libraryView.SelectedItems.Clear();
										}
									}
								}));
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Singleselect, WritableContainer - disallow protected items
					return listView.SelectedItems.Count == 1
						&& selectedListItems.FirstOrDefault()?.Model is ILibraryItem firstItem
						&& !firstItem.IsProtected
						&& ApplicationController.Instance.Library.ActiveContainer is ILibraryWritableContainer;
				}
			});

			// move menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Move".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					var partItems = selectedLibraryItems.Where(item => item is ILibraryAssetStream || item is ILibraryContainerLink);
					if (partItems.Any()
						&& libraryView.ActiveContainer is ILibraryWritableContainer sourceContainer)
					{
						DialogWindow.Show(new MoveItemPage((newName, destinationContainer) =>
						{
							destinationContainer.Move(partItems, sourceContainer);
							libraryView.SelectedItems.Clear();
						}));
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect, WritableContainer - disallow protected
					return listView.SelectedItems.Any()
						&& listView.SelectedItems.All(i => !i.Model.IsProtected
						&& ApplicationController.Instance.Library.ActiveContainer is ILibraryWritableContainer);
				}
			});

			// remove menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Remove".Localize(),
				Action = (selectedLibraryItems, listView) => deleteFromLibraryButton_Click(selectedLibraryItems, null),
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect, WritableContainer - disallow protected
					return listView.SelectedItems.Any()
						&& listView.SelectedItems.All(i => !i.Model.IsProtected
						&& ApplicationController.Instance.Library.ActiveContainer is ILibraryWritableContainer);
				}
			});

			menuActions.Add(new MenuSeparator("Export"));

			// export menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Export".Localize(),
				Action = (selectedLibraryItems, listView) => exportButton_Click(selectedLibraryItems, null),
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect - disallow containers
					return listView.SelectedItems.Any()
						&& listView.SelectedItems.All(i => !(i.Model is ILibraryContainer));
				},
			});

			// share menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Share".Localize(),
				Action = (selectedLibraryItems, listView) => shareFromLibraryButton_Click(selectedLibraryItems, null),
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

			// Extension point - RegisteredLibraryActions not defined in this file/assembly can insert here via this named token
			menuActions.AddRange(ApplicationController.Instance.RegisteredLibraryActions("StandardLibraryOperations"));

#if !__ANDROID__
			menuActions.Add(new MenuSeparator("Other"));

			// PDF export is limited to Windows
			if (AggContext.OperatingSystem == OSType.Windows)
			{
				menuActions.Add(new PrintItemAction()
				{
					Title = "Create Part Sheet".Localize(),
					Action = (selectedLibraryItems, listView) =>
					{
						UiThread.RunOnIdle(() =>
						{
							var printItems = selectedLibraryItems.OfType<ILibraryAssetStream>();
							if (printItems.Any())
							{
								AggContext.FileDialogs.SaveFileDialog(
									new SaveFileDialogParams("Save Parts Sheet|*.pdf")
									{
										ActionButtonLabel = "Save Parts Sheet".Localize(),
										Title = ApplicationController.Instance.ProductName + " - " + "Save".Localize()
									},
									(saveParams) =>
									{
										if (!string.IsNullOrEmpty(saveParams.FileName))
										{
											var feedbackWindow = new SavePartsSheetFeedbackWindow(
												printItems.Count(),
												printItems.FirstOrDefault()?.Name,
												ActiveTheme.Instance.PrimaryBackgroundColor);

											var currentPartsInQueue = new PartsSheet(printItems, saveParams.FileName);
											currentPartsInQueue.UpdateRemainingItems += feedbackWindow.StartingNextPart;
											currentPartsInQueue.DoneSaving += feedbackWindow.DoneSaving;

											feedbackWindow.ShowAsSystemWindow();

											currentPartsInQueue.SaveSheets();
										}
									});
							}
						});
					},
					IsEnabled = (selectedListItems, listView) =>
					{
						// Multiselect - disallow containers
						return listView.SelectedItems.Any()
							&& listView.SelectedItems.All(i => !(i.Model is ILibraryContainer));
					}
				});
			}
#endif

			menuActions.Add(new PrintItemAction()
			{
				Title = "Open Package".Localize(),
				Action = (selectedItems, listView) =>
				{
					var firstItem = selectedItems.First();

					if (firstItem is ILibraryAsset libraryAsset)
					{
						var container = new McxContainer(libraryAsset);
						container.Load();

						container.Parent = ApplicationController.Instance.Library.ActiveContainer;

						ApplicationController.Instance.Library.ActiveContainer = container;
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					return listView.SelectedItems.Count == 1
					&& selectedListItems.FirstOrDefault()?.Model is ILibraryAsset libraryAsset
					&& libraryAsset.ContentType == "mcx";
				}
			});
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if (libraryView?.ActiveContainer != null)
			{
				libraryView.ActiveContainer.ContentChanged -= UpdateStatus;
				ApplicationController.Instance.Library.ContainerChanged -= Library_ContainerChanged;
			}

			base.OnClosed(e);
		}

		private async void addToQueueButton_Click(object sender, EventArgs e)
		{
			var selectedItems = libraryView.SelectedItems.Select(o => o.Model);
			if (selectedItems.Any())
			{
				await PrintQueueContainer.AddAllItems(selectedItems);
			}
		}

		private void EnableMenus()
		{
			foreach (var menuAction in menuActions.Where(m => m.MenuItem != null))
			{
				menuAction.MenuItem.Enabled = menuAction.IsEnabled(libraryView.SelectedItems, libraryView);
			}
		}

		private void deleteFromLibraryButton_Click(object sender, EventArgs e)
		{
			// ask before remove
			var libraryItems = libraryView.SelectedItems.Select(p => p.Model);
			if (libraryItems.Any())
			{
				if (libraryView.ActiveContainer is ILibraryWritableContainer container)
				{
					if (container is FileSystemContainer)
					{
						container.Remove(libraryItems);
						libraryView.SelectedItems.Clear();
					}
					else
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
			}
		}

		private void shareFromLibraryButton_Click(object sender, EventArgs e)
		{
			// TODO: Should be rewritten to Register from cloudlibrary, include logic to add to library as needed

			ApplicationController.Instance.ShareLibraryItem(libraryView.SelectedItems.Select(i => i.Model).FirstOrDefault());
		}

		private void exportButton_Click(object sender, EventArgs e)
		{
			//Open export options
			var exportPage = new ExportPrintItemPage(libraryView.SelectedItems.Select(item => item.Model));

			DialogWindow.Show(exportPage);
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
			CreateMenuActions();

			navBar.OverflowButton.Name = "Print Library Overflow Menu";
			navBar.ExtendOverflowMenu = (popupMenu) =>
			{
				// Create menu items in the DropList for each element in this.menuActions
				foreach (var menuAction in menuActions)
				{
					if (menuAction is MenuSeparator)
					{
						popupMenu.CreateHorizontalLine();
					}
					else
					{
						var menuItem = popupMenu.CreateMenuItem(menuAction.Title, menuAction.Icon);
						menuItem.Name = $"{menuAction.Title} Menu Item";

						menuItem.Enabled = menuAction.Action != null;
						menuItem.ClearRemovedFlag();
						menuItem.Click += (s, e) =>
						{
							menuAction.Action?.Invoke(libraryView.SelectedItems.Select(i => i.Model), libraryView);
						};

						// Store a reference to the newly created MenuItem back on the MenuAction definition
						menuAction.MenuItem = menuItem;
					}
				}
			};

			base.OnLoad(args);
		}

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
		public Button ResetButton { get; }

		public SearchInputBox()
		{
			this.VAnchor = VAnchor.Center | VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;

			searchInput = new MHTextEditWidget(messageWhenEmptyAndNotSelected: "Search".Localize())
			{
				Name = "Search Library Edit",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center
			};
			this.AddChild(searchInput);

			var resetButton = ApplicationController.Instance.Theme.CreateSmallResetButton();
			resetButton.HAnchor = HAnchor.Right | HAnchor.Fit;
			resetButton.VAnchor = VAnchor.Center | VAnchor.Fit;
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
