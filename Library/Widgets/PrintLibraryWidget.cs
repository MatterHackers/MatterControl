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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class PrintLibraryWidget : GuiWidget
	{
		private static CreateFolderWindow createFolderWindow = null;
		private static RenameItemWindow renameItemWindow = null;
		private ExportToFolderFeedbackWindow exportingWindow = null;
		private TextImageButtonFactory textImageButtonFactory;
		private TextImageButtonFactory editButtonFactory;

		private FolderBreadCrumbWidget breadCrumbWidget;

		private Button addToLibraryButton;
		private Button createFolderButton;
		private Button enterEditModeButton;
		private FlowLayoutWidget buttonPanel;
		private MHTextEditWidget searchInput;
		private ListView libraryView;
		private GuiWidget providerMessageContainer;
		private TextWidget providerMessageWidget;

		private OverflowDropdown overflowDropdown;

		//private DropDownMenu actionMenu;
		private List<PrintItemAction> menuActions = new List<PrintItemAction>();

		public PrintLibraryWidget()
		{
			this.Padding = new BorderDouble(top: 3);

			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;
			this.AnchorAll();

			textImageButtonFactory = new TextImageButtonFactory()
			{
				borderWidth = 0,
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.TabLabelUnselected,
				disabledFillColor = new RGBA_Bytes()
			};

			editButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.TabLabelUnselected,
				disabledFillColor = new RGBA_Bytes(),
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				borderWidth = 0,
				Margin = new BorderDouble(10, 0)
			};

			var allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

			// Create search panel
			{
				var searchPanel = new FlowLayoutWidget()
				{
					BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay,
					HAnchor = HAnchor.ParentLeftRight,
					Padding = new BorderDouble(0),
					Visible = false // TODO: Restore ASAP
				};

				enterEditModeButton = editButtonFactory.Generate("Edit".Localize(), centerText: true);
				enterEditModeButton.Name = "Library Edit Button";
				searchPanel.AddChild(enterEditModeButton);

				searchInput = new MHTextEditWidget(messageWhenEmptyAndNotSelected: "Search Library".Localize())
				{
					Name = "Search Library Edit",
					Margin = new BorderDouble(0, 3, 0, 0),
					HAnchor = HAnchor.ParentLeftRight,
					VAnchor = VAnchor.ParentCenter
				};
				searchInput.ActualTextEditWidget.EnterPressed += (s, e) => PerformSearch();
				searchPanel.AddChild(searchInput);

				// TODO: We should describe the intent of setting to zero and immediately restoring to the original value. Not clear, looks pointless
				double oldWidth = editButtonFactory.FixedWidth;
				editButtonFactory.FixedWidth = 0;

				Button searchButton = editButtonFactory.Generate("Search".Localize(), centerText: true);
				searchButton.Name = "Search Library Button";
				searchButton.Click += (s, e) => PerformSearch();
				editButtonFactory.FixedWidth = oldWidth;
				searchPanel.AddChild(searchButton);

				allControls.AddChild(searchPanel);
			}

			libraryView = new ListView(ApplicationController.Instance.Library)
			{
				BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor
			};

			libraryView.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;

			ApplicationController.Instance.Library.ContainerChanged += Library_ContainerChanged;

			var breadCrumbBar = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Padding = new BorderDouble(bottom: 2)
			};

			breadCrumbWidget = new FolderBreadCrumbWidget(libraryView);
			breadCrumbBar.AddChild(breadCrumbWidget);

			overflowDropdown = new OverflowDropdown(allowLightnessInvert: true)
			{
				AlignToRightEdge = true,
				Name = "Print Library Overflow Menu"
			};
			breadCrumbBar.AddChild(overflowDropdown);

			allControls.AddChild(breadCrumbBar);

			allControls.AddChild(libraryView);

			buttonPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Padding = new BorderDouble(0, 3),
				MinimumSize = new Vector2(0, 46),
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor
			};
			AddLibraryButtonElements();
			allControls.AddChild(buttonPanel);

			allControls.AnchorAll();

			this.AddChild(allControls);
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
				e.PreviousContainer.Reloaded -= UpdateStatus;
			}

			var activeContainer = this.libraryView.ActiveContainer;


			var writableContainer = activeContainer as ILibraryWritableContainer;

			bool containerSupportsEdits = activeContainer is ILibraryWritableContainer;

			addToLibraryButton.Enabled = containerSupportsEdits;
			createFolderButton.Enabled = containerSupportsEdits && writableContainer?.AllowAction(ContainerActions.AddContainers) == true;

			searchInput.Text = activeContainer.KeywordFilter;
			breadCrumbWidget.SetBreadCrumbs(activeContainer);

			activeContainer.Reloaded += UpdateStatus;

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

			// the add button
			addToLibraryButton = textImageButtonFactory.Generate("Add".Localize(), "icon_circle_plus.png");
			addToLibraryButton.Enabled = false; // The library selector (the first library selected) is protected so we can't add to it. 
			addToLibraryButton.ToolTipText = "Add an .stl, .amf, .gcode or .zip file to the Library".Localize();
			addToLibraryButton.Name = "Library Add Button";
			buttonPanel.AddChild(addToLibraryButton);
			addToLibraryButton.Margin = new BorderDouble(0, 0, 3, 0);
			addToLibraryButton.Click += (sender, e) => UiThread.RunOnIdle(() =>
			{
				FileDialog.OpenFileDialog(
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

			// the create folder button
			createFolderButton = textImageButtonFactory.Generate("Create Folder".Localize());
			createFolderButton.Enabled = false; // The library selector (the first library selected) is protected so we can't add to it.
			createFolderButton.Name = "Create Folder From Library Button";
			createFolderButton.Margin = new BorderDouble(0, 0, 3, 0);
			createFolderButton.Click += (sender, e) =>
			{
				if (createFolderWindow == null)
				{
					createFolderWindow = new CreateFolderWindow((result) =>
					{
						if (!string.IsNullOrEmpty(result.newName)
							&& this.libraryView.ActiveContainer is ILibraryWritableContainer writableContainer)
						{
							writableContainer.Add(new[] { new DynamicContainerLink(result.newName, null) });
						}
					});
					createFolderWindow.Closed += (sender2, e2) => { createFolderWindow = null; };
				}
				else
				{
					createFolderWindow.BringToFront();
				}
			};
			buttonPanel.AddChild(createFolderButton);

			if (OemSettings.Instance.ShowShopButton)
			{
				var shopButton = textImageButtonFactory.Generate("Buy Materials".Localize(), StaticData.Instance.LoadIcon("icon_shopping_cart_32x32.png", 32, 32));
				shopButton.ToolTipText = "Shop online for printing materials".Localize();
				shopButton.Name = "Buy Materials Button";
				shopButton.Margin = new BorderDouble(0, 0, 3, 0);
				shopButton.Click += (sender, e) =>
				{
					double activeFilamentDiameter = 0;
					if (ActiveSliceSettings.Instance.PrinterSelected)
					{
						activeFilamentDiameter = 3;
						if (ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter) < 2)
						{
							activeFilamentDiameter = 1.75;
						}
					}

					MatterControlApplication.Instance.LaunchBrowser("http://www.matterhackers.com/mc/store/redirect?d={0}&clk=mcs&a={1}".FormatWith(activeFilamentDiameter, OemSettings.Instance.AffiliateCode));
				};
				buttonPanel.AddChild(shopButton);
			}

			// add in the message widget
			providerMessageContainer = new GuiWidget()
			{
				VAnchor = VAnchor.FitToChildren | VAnchor.ParentTop,
				HAnchor = HAnchor.ParentLeftRight,
				Visible = false,
			};
			buttonPanel.AddChild(providerMessageContainer, -1);

			providerMessageWidget = new TextWidget("")
			{
				PointSize = 8,
				HAnchor = HAnchor.ParentRight,
				VAnchor = VAnchor.ParentBottom,
				TextColor = ActiveTheme.Instance.SecondaryTextColor,
				Margin = new BorderDouble(6),
				AutoExpandBoundsToText = true,
			};
			providerMessageContainer.AddChild(providerMessageWidget);
		}

		private void CreateMenuActions()
		{
			// edit menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Add to Plate".Localize(),
				AllowMultiple = true,
				AllowProtected = true,
				AllowContainers = false,
				Action = async (selectedLibraryItems, listView) =>
				{
					var itemsToAdd = new List<IObject3D>();

					var library = ApplicationController.Instance.Library;

					foreach (var item in selectedLibraryItems.Where(o => o is ILibraryContentStream))
					{
						var contentProvider = library.GetContentProvider(item) as ISceneContentProvider;
						var result = contentProvider?.CreateItem(item, null);

						// TODO: Bounding box should be part of IObject3D state so we don't have this constraint
						// We must wait for the mesh to load so we can use it's bounds below to calculate its position
						await result.MeshLoaded;

						if (result?.Object3D != null)
						{
							itemsToAdd.Add(result.Object3D);
						}
					}

					ApplicationController.Instance.ActiveView3DWidget.partHasBeenEdited = true;

					var scene = ApplicationController.Instance.ActiveView3DWidget.Scene;
					scene.ModifyChildren(children =>
					{
						foreach (var sceneItem in itemsToAdd)
						{
							PlatingHelper.MoveToOpenPosition(sceneItem, children);
							children.Add(sceneItem);
						}
					});
				}
			});

			// edit menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Edit".Localize(),
				AllowMultiple = false,
				AllowProtected = false,
				AllowContainers = false,
				Action = (selectedLibraryItems, listView) =>
				{
					throw new NotImplementedException();
					/* editButton_Click(s, null) */
				}
			});

			// rename menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Rename".Localize(),
				AllowMultiple = false,
				AllowProtected = false,
				AllowContainers = true,
				Action = (selectedLibraryItems, listView) => renameFromLibraryButton_Click(selectedLibraryItems, null),
			});

			// move menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Move".Localize(),
				AllowMultiple = true,
				AllowProtected = false,
				AllowContainers = true,
				Action = (selectedLibraryItems, listView) => moveInLibraryButton_Click(selectedLibraryItems, null),
			});

			// remove menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Remove".Localize(),
				AllowMultiple = true,
				AllowProtected = false,
				AllowContainers = true,
				Action = (selectedLibraryItems, listView) => deleteFromLibraryButton_Click(selectedLibraryItems, null),
			});

			menuActions.Add(new MenuSeparator("Classic Queue"));

			// add to queue menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Add to Queue".Localize(),
				AllowMultiple = true,
				AllowProtected = true,
				AllowContainers = false,
				Action = (selectedLibraryItems, listView) => addToQueueButton_Click(selectedLibraryItems, null),
			});

			// export menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Export".Localize(),
				AllowMultiple = false,
				AllowProtected = true,
				AllowContainers = false,
				Action = (selectedLibraryItems, listView) => exportButton_Click(selectedLibraryItems, null),
			});

			// share menu item
			menuActions.Add(new PrintItemAction()
			{
				Title = "Share".Localize(),
				AllowMultiple = false,
				AllowProtected = false,
				AllowContainers = false,
				Action = (selectedLibraryItems, listView) => shareFromLibraryButton_Click(selectedLibraryItems, null),
			});

			// Extension point - RegisteredLibraryActions not defined in this file/assembly can insert here via this named token
			menuActions.AddRange(ApplicationController.Instance.RegisteredLibraryActions("StandardLibraryOperations"));

#if !__ANDROID__
			menuActions.Add(new MenuSeparator("Design"));
			menuActions.Add(new PrintItemAction()
			{
				Title = "Export to Zip".Localize(),
				AllowMultiple = true,
				AllowProtected = true,
				AllowContainers = false,
				Action = (selectedLibraryItems, listView) =>
				{
					var streamItems = selectedLibraryItems.OfType<ILibraryContentStream>();
					if (streamItems.Any())
					{
						UiThread.RunOnIdle(() =>
						{
							var project = new ProjectFileHandler(streamItems);
							project.SaveAs();
						});
					}
				},
			});

			menuActions.Add(new MenuSeparator("G-Code"));
			menuActions.Add(new PrintItemAction()
			{
				Title = "Export to Folder or SD Card".Localize(),
				AllowMultiple = true,
				AllowProtected = false,
				AllowContainers = false,
				Action = (selectedLibraryItems, listView) =>
				{
					if (!ActiveSliceSettings.Instance.PrinterSelected)
					{
						UiThread.RunOnIdle(() =>
						{
							// MustSelectPrinterMessage
							StyledMessageBox.ShowMessageBox(
								null,
								"Before you can export printable files, you must select a printer.".Localize(),
								"Please select a printer".Localize());
						});
					}
					else
					{
						UiThread.RunOnIdle(SelectLocationToExportGCode);
					}
				}
			});

			menuActions.Add(new MenuSeparator("Other"));

			// PDF export is limited to Windows
			if (OsInformation.OperatingSystem == OSType.Windows)
			{
				menuActions.Add(new PrintItemAction()
				{
					Title = "Create Part Sheet".Localize(),
					AllowMultiple = true,
					AllowProtected = true,
					AllowContainers = false,
					Action = (selectedLibraryItems, listView) =>
					{
						UiThread.RunOnIdle(() =>
						{
							var printItems = selectedLibraryItems.OfType<ILibraryContentStream>();
							if (printItems.Any())
							{
								FileDialog.SaveFileDialog(
									new SaveFileDialogParams("Save Parts Sheet|*.pdf")
									{
										ActionButtonLabel = "Save Parts Sheet".Localize(),
										Title = "MatterControl".Localize() + ": " + "Save".Localize()
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
					}
				});
			}
#endif

			menuActions.Add(new MenuSeparator("ListView Options"));
			menuActions.Add(new PrintItemAction()
			{
				Title = "View List".Localize(),
				AlwaysEnabled = true,
				Action = (selectedLibraryItems, listView) =>
				{
					listView.ListContentView = new RowListView();
				},
			});

			menuActions.Add(new PrintItemAction()
			{
				Title = "View Icons".Localize(),
				AlwaysEnabled = true,
				Action = (selectedLibraryItems, listView) =>
				{
					listView.ListContentView = new IconListView();
				},
			});

			menuActions.Add(new PrintItemAction()
			{
				Title = "View Large Icons".Localize(),
				AlwaysEnabled = true,
				Action = (selectedLibraryItems, listView) =>
				{
					listView.ListContentView = new IconListView()
					{
						ThumbWidth = 256,
						ThumbHeight = 256,
					};
				},
			});
		}

		private void SelectLocationToExportGCode()
		{
			/*
			FileDialog.SelectFolderDialog(
				new SelectFolderDialogParams("Select Location To Save Files")
				{
					ActionButtonLabel = "Export".Localize(),
					Title = "MatterControl: Select A Folder"
				},
				(openParams) =>
				{
					string path = openParams.FolderPath;
					if (path != null && path != "")
					{
						List<PrintItem> parts = QueueData.Instance.CreateReadOnlyPartList(true);
						if (parts.Count > 0)
						{
							if (exportingWindow == null)
							{
								exportingWindow = new ExportToFolderFeedbackWindow(parts.Count, parts[0].Name, ActiveTheme.Instance.PrimaryBackgroundColor);
								exportingWindow.Closed += (s, e) =>
								{
									this.exportingWindow = null;
								};
								exportingWindow.ShowAsSystemWindow();
							}
							else
							{
								exportingWindow.BringToFront();
							}

							var exportToFolderProcess = new ExportToFolderProcess(parts, path);
							exportToFolderProcess.StartingNextPart += exportingWindow.StartingNextPart;
							exportToFolderProcess.UpdatePartStatus += exportingWindow.UpdatePartStatus;
							exportToFolderProcess.DoneSaving += exportingWindow.DoneSaving;
							exportToFolderProcess.Start();
						}
					}
				}); */
		}
		
		private void renameFromLibraryButton_Click(IEnumerable<ILibraryItem> items, object p)
		{
			if (libraryView.SelectedItems.Count == 1)
			{
				var selectedItem = libraryView.SelectedItems.FirstOrDefault();
				if (selectedItem == null)
				{
					return;
				}

				if (renameItemWindow == null)
				{
					renameItemWindow = new RenameItemWindow(
						selectedItem.Model.Name,
						(returnInfo) =>
						{
							var model = libraryView.SelectedItems.FirstOrDefault()?.Model;
							if (model != null)
							{
								var container = libraryView.ActiveContainer as ILibraryWritableContainer;
								if (container != null)
								{
									container.Rename(model, returnInfo.newName);
									libraryView.SelectedItems.Clear();
								}
							}
						});

					renameItemWindow.Closed += (s, e) => renameItemWindow = null;
				}
				else
				{
					renameItemWindow.BringToFront();
				}
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if (libraryView?.ActiveContainer != null)
			{
				libraryView.ActiveContainer.Reloaded -= UpdateStatus;
				ApplicationController.Instance.Library.ContainerChanged -= Library_ContainerChanged;
			}

			base.OnClosed(e);
		}

		private void PerformSearch()
		{
			UiThread.RunOnIdle(() =>
			{
				libraryView.ActiveContainer.KeywordFilter = searchInput.Text.Trim();
				breadCrumbWidget.SetBreadCrumbs(libraryView.ActiveContainer);
			});
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
			foreach (var menuAction in menuActions)
			{
				var menuItem = menuAction.MenuItem;

				if (menuAction.AlwaysEnabled)
				{
					menuItem.Enabled = true;
					continue;
				}

				menuItem.Enabled = menuAction.Action != null && libraryView.SelectedItems.Count > 0;

				if (!menuAction.AllowMultiple)
				{
					menuItem.Enabled &= libraryView.SelectedItems.Count == 1;
				}

				if (!menuAction.AllowProtected)
				{
					menuItem.Enabled &= libraryView.SelectedItems.All(i => !i.Model.IsProtected);
				}

				if (!menuAction.AllowContainers)
				{
					menuItem.Enabled &= libraryView.SelectedItems.All(i => !(i.Model is ILibraryContainer));
				}
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
			if (libraryView.SelectedItems.Count == 1)
			{
				var libraryItem = libraryView.SelectedItems.Select(i => i.Model).FirstOrDefault();
				if (libraryItem != null)
				{
					throw new NotImplementedException("Export not implemented");

					// TODO: Implement
					//ApplicationController.OpenExportWindow(await this.GetPrintItemWrapperAsync());
				}
			}
		}

		/*
		public async Task<PrintItemWrapper> GetPrintItemWrapperAsync()
		{
			return await libraryProvider.GetPrintItemWrapperAsync(this.ItemIndex);
		} */

		// TODO: We've discussed not doing popup edit in a new window. That's what this did, not worth porting yet...
		/*
		private void editButton_Click(object sender, EventArgs e)
		{
			//Open export options
			if (libraryDataView.SelectedItems.Count == 1)
			{

				OpenPartViewWindow(PartPreviewWindow.View3DWidget.OpenMode.Editing);

				LibraryRowItem libraryItem = libraryDataView.SelectedItems[0];
				libraryItem.Edit();
			}
		} */

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

			var popupContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			
			// Create menu items in the DropList for each element in this.menuActions
			foreach (var menuAction in menuActions)
			{
				MenuItem menuItem;

				if (menuAction is MenuSeparator)
				{
					menuItem = overflowDropdown.CreateHorizontalLine();
				}
				else
				{
					menuItem = overflowDropdown.CreateMenuItem((string)menuAction.Title);
					menuItem.Name = $"{menuAction.Title} Menu Item";
				}

				menuItem.Enabled = menuAction.Action != null;
				menuItem.ClearRemovedFlag();
				menuItem.Click += (s, e) =>
				{
					menuAction.Action?.Invoke(libraryView.SelectedItems.Select(i => i.Model), libraryView);
				};

				// Store a reference to the newly created MenuItem back on the MenuAction definition
				menuAction.MenuItem = menuItem;

				popupContainer.AddChild(menuItem);
			}

			EnableMenus();

			overflowDropdown.PopupContent = popupContainer;

			base.OnLoad(args);
		}
	}
}
