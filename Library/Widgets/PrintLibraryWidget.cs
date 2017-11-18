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
using MatterHackers.DataConverters3D;
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
		private Button addToLibraryButton;
		private Button createFolderButton;
		private FlowLayoutWidget buttonPanel;
		private ListView libraryView;
		private GuiWidget providerMessageContainer;
		private TextWidget providerMessageWidget;

		private OverflowMenu overflowMenu;

		//private DropDownMenu actionMenu;
		private List<PrintItemAction> menuActions = new List<PrintItemAction>();

		private FolderBreadCrumbWidget breadCrumbWidget;
		private GuiWidget searchInput;
		private ILibraryContainer searchContainer;

		private PartPreviewContent partPreviewContent;

		private ThemeConfig theme;

		public PrintLibraryWidget(PartPreviewContent partPreviewContent, ThemeConfig theme)
		{
			this.theme = theme;
			this.partPreviewContent = partPreviewContent;
			this.Padding = 0;
			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;
			this.AnchorAll();

			var allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

			libraryView = new ListView(ApplicationController.Instance.Library)
			{
				BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor,
				// Drop containers if ShowContainers != 1
				ContainerFilter = (container) => UserSettings.Instance.get("ShowContainers") == "1"
			};

			ApplicationController.Instance.Library.ActiveViewWidget = libraryView;

			libraryView.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;

			ApplicationController.Instance.Library.ContainerChanged += Library_ContainerChanged;

			var navBar = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch
			};
			allControls.AddChild(navBar);

			var showFolders = new ExpandCheckboxButton("Folders".Localize())
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(left: 2, bottom: 2, top: 6), // Same padding as toolbar above
				Name = "Show Folders Toggle",
				Checked = UserSettings.Instance.get("ShowContainers") == "1",
				BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor
			};
			showFolders.CheckedStateChanged += async (s, e) =>
			{
				UserSettings.Instance.set("ShowContainers", showFolders.Checked ? "1" : "0");
				libraryView.Reload();
			};
			allControls.AddChild(showFolders);

			breadCrumbWidget = new FolderBreadCrumbWidget(libraryView);
			navBar.AddChild(breadCrumbWidget);

			var buttonFactory = theme.SmallMarginButtonFactory;

			var searchPanel = new SearchInputBox()
			{
				Visible = false,
				Margin = new BorderDouble(10, 0, 5, 0)
			};
			searchPanel.searchInput.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				this.PerformSearch();
			};
			searchPanel.resetButton.Click += (s, e) =>
			{
				breadCrumbWidget.Visible = true;
				searchPanel.Visible = false;

				searchPanel.searchInput.Text = "";

				this.ClearSearch();
			};

			// Store a reference to the input field
			this.searchInput = searchPanel.searchInput;

			navBar.AddChild(searchPanel);

			var searchButton = theme.ButtonFactory.GenerateIconButton(AggContext.StaticData.LoadIcon("icon_search_24x24.png", 16, 16, IconColor.Theme));
			searchButton.Name = "Search Library Button";
			searchButton.ToolTipText = "Search".Localize();
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

			overflowMenu = new OverflowMenu(IconColor.Theme)
			{
				VAnchor = VAnchor.Center,
				AlignToRightEdge = true,
				Name = "Print Library Overflow Menu",
			};
			navBar.AddChild(overflowMenu);

			allControls.AddChild(libraryView);

			buttonPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Padding = ApplicationController.Instance.Theme.ToolbarPadding,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor,
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

			EnableMenus();
		}

		private void Library_ContainerChanged(object sender, ContainerChangedEventArgs e)
		{
			// Release
			if (e.PreviousContainer != null)
			{
				e.PreviousContainer.ContentChanged -= UpdateStatus;
			}

			var activeContainer = this.libraryView.ActiveContainer;

			var writableContainer = activeContainer as ILibraryWritableContainer;

			bool containerSupportsEdits = activeContainer is ILibraryWritableContainer;

			addToLibraryButton.Enabled = containerSupportsEdits;
			createFolderButton.Enabled = containerSupportsEdits && writableContainer?.AllowAction(ContainerActions.AddContainers) == true;

			searchInput.Text = activeContainer.KeywordFilter;
			breadCrumbWidget.SetBreadCrumbs(activeContainer);

			activeContainer.ContentChanged += UpdateStatus;

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
			var textImageButtonFactory = ApplicationController.Instance.Theme.SmallMarginButtonFactory;

			buttonPanel.RemoveAllChildren();

			// the add button
			addToLibraryButton = textImageButtonFactory.Generate("Add".Localize(), AggContext.StaticData.LoadIcon("cube.png", IconColor.Theme));
			addToLibraryButton.Enabled = false; // The library selector (the first library selected) is protected so we can't add to it. 
			addToLibraryButton.ToolTipText = "Add an .stl, .obj, .amf, .gcode or .zip file to the Library".Localize();
			addToLibraryButton.Name = "Library Add Button";
			addToLibraryButton.Margin = new BorderDouble(0, 0, 3, 0);
			addToLibraryButton.Click += (sender, e) => UiThread.RunOnIdle(() =>
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
			buttonPanel.AddChild(addToLibraryButton);

			// the create folder button
			createFolderButton = textImageButtonFactory.Generate("Create Folder".Localize());
			createFolderButton.Enabled = false; // Disabled until changed by the ActiveContainer
			createFolderButton.Name = "Create Folder From Library Button";
			createFolderButton.Margin = new BorderDouble(0, 0, 3, 0);
			createFolderButton.Click += (sender, e) =>
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

			};
			buttonPanel.AddChild(createFolderButton);

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
				Title = "Print",
				Action = (selectedLibraryItems, listView) =>
				{
					// TODO: Sort out the right way to have an ActivePrinter context that looks and behaves correctly
					var activeContext = ApplicationController.Instance.DragDropData;
					var printer = activeContext.Printer;
					//var printerTabPage = activeContext.View3DWidget.Parents<PrinterTabPage>().FirstOrDefault();

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
									printer.Bed.ClearPlate();

									AddToPlate(selectedLibraryItems);

									var context = printer.Bed.EditContext;

									await ApplicationController.Instance.PrintPart(
										context.PartFilePath,
										context.GCodeFilePath,
										context.SourceItem.Name,
										printer,
										activeContext.View3DWidget,
										null);
								});
							}
							break;
					}

				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Singleselect - disallow containers
					return listView.SelectedItems.Count == 1
						&& selectedListItems.FirstOrDefault()?.Model is ILibraryItem firstItem
						&& !(firstItem is ILibraryContainer)
						&& ApplicationController.Instance.DragDropData?.Printer?.Connection.CommunicationState == CommunicationStates.Connected;
				}
			});

			// edit menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Add to Plate".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					AddToPlate(selectedLibraryItems);
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect - disallow containers
					return listView.SelectedItems.All(i => !(i.Model is ILibraryContainer));
				}		
			});

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
							bed = new BedConfig(
								new EditContext()
								{
									ContentStore = writableContainer,
									SourceItem = firstItem
								}), 
							theme);

						// Load content after UI widgets to support progress notification during acquire/load
						await bed.LoadContent();

						if (newTab.TabContent is PartTabPage partTab)
						{
							// TODO: Restore ability to render progress loading
						}
					}
				},
				IsEnabled = (selectedListItems, listView) =>
				{
					// Singleselect, WritableContainer - disallow containers and protected items
					return listView.SelectedItems.Count == 1
						&& selectedListItems.FirstOrDefault()?.Model is ILibraryItem firstItem
						&& !(firstItem is ILibraryContainer)
						&& !firstItem.IsProtected
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
				Action = (selectedLibraryItems, listView) => moveInLibraryButton_Click(selectedLibraryItems, null),
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect, WritableContainer - disallow protected
					return listView.SelectedItems.All(i => !i.Model.IsProtected
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
					return listView.SelectedItems.All(i => !i.Model.IsProtected
						&& ApplicationController.Instance.Library.ActiveContainer is ILibraryWritableContainer);
				}
			});

			menuActions.Add(new MenuSeparator("Classic Queue"));

			// add to queue menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Add to Queue".Localize(),
				Action = (selectedLibraryItems, listView) => addToQueueButton_Click(selectedLibraryItems, null),
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect - disallow containers
					return listView.SelectedItems.All(i => !(i.Model is ILibraryContainer));
				},
			});

			// export menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Export".Localize(),
				Action = (selectedLibraryItems, listView) => exportButton_Click(selectedLibraryItems, null),
				IsEnabled = (selectedListItems, listView) =>
				{
					// Multiselect - disallow containers
					return listView.SelectedItems.All(i => !(i.Model is ILibraryContainer));
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
							var printItems = selectedLibraryItems.OfType<ILibraryContentStream>();
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
						return listView.SelectedItems.All(i => !(i.Model is ILibraryContainer));
					}
				});
			}
#endif

			menuActions.Add(new MenuSeparator("ListView Options"));
			menuActions.Add(new PrintItemAction()
			{
				Title = "View List".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					listView.ListContentView = new RowListView();
					listView.Reload().ConfigureAwait(false);
				},
				IsEnabled = (selectedListItems, listView) => true
			});

			menuActions.Add(new PrintItemAction()
			{
				Title = "View XSmall Icons".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					listView.ListContentView = new IconListView(18);
					listView.Reload().ConfigureAwait(false);
				},
				IsEnabled = (selectedListItems, listView) => true
			});


			menuActions.Add(new PrintItemAction()
			{
				Title = "View Small Icons".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					listView.ListContentView = new IconListView(70);
					listView.Reload().ConfigureAwait(false);
				},
				IsEnabled = (selectedListItems, listView) => true
			});

			menuActions.Add(new PrintItemAction()
			{
				Title = "View Icons".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					listView.ListContentView = new IconListView();
					listView.Reload().ConfigureAwait(false);
				},
				IsEnabled = (selectedListItems, listView) => true
			});

			menuActions.Add(new PrintItemAction()
			{
				Title = "View Large Icons".Localize(),
				Action = (selectedLibraryItems, listView) =>
				{
					listView.ListContentView = new IconListView(256);
					listView.Reload().ConfigureAwait(false);
				},
				IsEnabled = (selectedListItems, listView) => true
			});
		}

		private static void AddToPlate(IEnumerable<ILibraryItem> selectedLibraryItems)
		{
			var context = ApplicationController.Instance.DragDropData;
			var scene = context.SceneContext.Scene;
			scene.Children.Modify(list =>
			{
				list.Add(
					new InsertionGroup(
						selectedLibraryItems,
						context.View3DWidget,
						scene,
						context.SceneContext.BedCenter,
						dragOperationActive: () => false));
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
				menuAction.MenuItem.Enabled = libraryView.SelectedItems.Count > 0
					&& menuAction.IsEnabled(libraryView.SelectedItems, libraryView);
			}
		}

		private void deleteFromLibraryButton_Click(object sender, EventArgs e)
		{
			var libraryItems = libraryView.SelectedItems.Select(p => p.Model);
			if (libraryItems.Any())
			{
				var container = libraryView.ActiveContainer as ILibraryWritableContainer;
				if (container != null)
				{
					container.Remove(libraryItems);
				}
			}

			libraryView.SelectedItems.Clear();
		}

		private void moveInLibraryButton_Click(object sender, EventArgs e)
		{
			// TODO: If we don't filter to non-container content here, then the providers could be passed a container to move to some other container
			var partItems = libraryView.SelectedItems.Where(item => item is ILibraryContentItem);
			if (partItems.Count() > 0)
			{
				// If all selected items are LibraryRowItemParts, then we can invoke the batch remove functionality (in the Cloud library scenario)
				// and perform all moves as part of a single request, with a single notification from Socketeer

				var container = libraryView.ActiveContainer as ILibraryWritableContainer;
				if (container != null)
				{
					throw new NotImplementedException("Library Move not implemented");
					// TODO: Implement move
					container.Move(partItems.Select(p => p.Model), null);
				}
			}

			libraryView.SelectedItems.Clear();
		}

		private void shareFromLibraryButton_Click(object sender, EventArgs e)
		{
			// TODO: Should be rewritten to Register from cloudlibrary, include logic to add to library as needed
			throw new NotImplementedException();

			if (libraryView.SelectedItems.Count == 1)
			{
				var partItem = libraryView.SelectedItems.Select(i => i.Model).FirstOrDefault();
				if (partItem != null)
				{
					//libraryView.ActiveContainer.ShareItem(partItem, "something");
				}
			}
		}

		private void exportButton_Click(object sender, EventArgs e)
		{
			//Open export options
			var exportPage = new ExportPrintItemPage(libraryView.SelectedItems.Select(item => item.Model));

			DialogWindow.Show(exportPage);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.DragFiles?.Count > 0)
			{
				if (libraryView?.ActiveContainer?.IsProtected == false)
				{
					foreach (string file in mouseEvent.DragFiles)
					{
						string extension = Path.GetExtension(file).ToUpper();
						if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
							|| extension == ".GCODE"
							|| extension == ".ZIP")
						{
							mouseEvent.AcceptDrop = true;
						}
					}
				}
			}

			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y)
				&& mouseEvent.DragFiles?.Count > 0)
			{
				if (libraryView != null
					&& !libraryView.ActiveContainer.IsProtected)
				{
					// TODO: Consider reusing common accept drop logic
					//mouseEvent.AcceptDrop = mouseEvent.DragFiles.TrueForAll(filePath => ApplicationController.Instance.IsLoadableFile(filePath));

					foreach (string file in mouseEvent.DragFiles)
					{
						string extension = Path.GetExtension(file).ToUpper();
						if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
							|| extension == ".GCODE"
							|| extension == ".ZIP")
						{
							mouseEvent.AcceptDrop = true;
							break;
						}
					}
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

			var popupMenu = new PopupMenu(theme);

			// Create menu items in the DropList for each element in this.menuActions
			foreach (var menuAction in menuActions)
			{
				if (menuAction is MenuSeparator)
				{
					popupMenu.CreateHorizontalLine();
				}
				else
				{
					var menuItem = popupMenu.CreateMenuItem((string)menuAction.Title);
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

			EnableMenus();

			overflowMenu.PopupContent = popupMenu;

			base.OnLoad(args);
		}
		private class SearchInputBox : GuiWidget
		{
			internal MHTextEditWidget searchInput;
			internal Button resetButton;

			public SearchInputBox()
			{
				this.VAnchor = VAnchor.Center | VAnchor.Fit;
				this.HAnchor = HAnchor.Stretch;

				searchInput = new MHTextEditWidget(messageWhenEmptyAndNotSelected: "Search Library".Localize())
				{
					Name = "Search Library Edit",
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Center
				};
				this.AddChild(searchInput);

				resetButton = ApplicationController.Instance.Theme.CreateSmallResetButton();
				resetButton.HAnchor = HAnchor.Right | HAnchor.Fit;
				resetButton.VAnchor = VAnchor.Center | VAnchor.Fit;
				resetButton.Name = "Close Search";
				resetButton.ToolTipText = "Clear".Localize();

				this.AddChild(resetButton);
			}
		}
	}
}
