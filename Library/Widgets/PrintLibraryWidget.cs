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
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class PrintLibraryWidget : GuiWidget
	{
		private static CreateFolderWindow createFolderWindow = null;
		private static RenameItemWindow renameItemWindow = null;
		private ExportToFolderFeedbackWindow exportingWindow = null;

		private Button addToLibraryButton;
		private Button createFolderButton;
		private FlowLayoutWidget buttonPanel;
		private ListView libraryView;
		private GuiWidget providerMessageContainer;
		private TextWidget providerMessageWidget;

		private OverflowDropdown overflowDropdown;

		private PopupButton activeContainerPopup;
		private TextWidget activeContainerTitle;

		//private DropDownMenu actionMenu;
		private List<PrintItemAction> menuActions = new List<PrintItemAction>();

		public PrintLibraryWidget()
		{
			this.Padding = 0;

			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;
			this.AnchorAll();

			var allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

			libraryView = new ListView(ApplicationController.Instance.Library)
			{
				BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor,
				ShowContainers = false
			};

			libraryView.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;

			ApplicationController.Instance.Library.ContainerChanged += Library_ContainerChanged;

			var breadCrumbBar = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = ApplicationController.Instance.Theme.ToolbarPadding
			};

			int arrowHeight = 5;

			var directionArrow = new PathStorage();
			directionArrow.MoveTo(-arrowHeight, 0);
			directionArrow.LineTo(arrowHeight, 0);
			directionArrow.LineTo(0, -arrowHeight);

			var buttonView = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				MinimumSize = new Vector2(0, ApplicationController.Instance.Theme.ButtonFactory.FixedHeight)
			};
			buttonView.AfterDraw += (s, e) =>
			{
				e.graphics2D.Render(directionArrow, buttonView.LocalBounds.Right - arrowHeight * 2 - 2, buttonView.LocalBounds.Center.y + arrowHeight / 2, ActiveTheme.Instance.SecondaryTextColor);
			};

			activeContainerTitle = new TextWidget(ApplicationController.Instance.Library.ActiveContainer.Name, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(left: 6),
				VAnchor = VAnchor.Center
			};
			buttonView.AddChild(activeContainerTitle);

			activeContainerPopup = new PopupButton(buttonView)
			{
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Stretch,
				Margin = 0
			};
			activeContainerPopup.DynamicPopupContent = () =>
			{
				var container = new GuiWidget(400, this.Height)
				{
					BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor
				};

				container.AddChild(new ListContainerBrowser(this.libraryView, ApplicationController.Instance.Library)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch
				});

				return container;
			};

			breadCrumbBar.AddChild(activeContainerPopup);

			overflowDropdown = new OverflowDropdown(allowLightnessInvert: true)
			{
				VAnchor = VAnchor.Center,
				AlignToRightEdge = true,
				Name = "Print Library Overflow Menu",
			};
			breadCrumbBar.AddChild(overflowDropdown);

			allControls.AddChild(breadCrumbBar);

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

			activeContainerTitle.Text = activeContainer.Name;

			// searchInput.Text = activeContainer.KeywordFilter;
			//breadCrumbWidget.SetBreadCrumbs(activeContainer);

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
			var textImageButtonFactory = ApplicationController.Instance.Theme.SmallMarginButtonFactory;

			buttonPanel.RemoveAllChildren();

			// the add button
			addToLibraryButton = textImageButtonFactory.Generate("Add".Localize(), "cube.png");
			addToLibraryButton.Enabled = false; // The library selector (the first library selected) is protected so we can't add to it. 
			addToLibraryButton.ToolTipText = "Add an .stl, .amf, .gcode or .zip file to the Library".Localize();
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
				AllowMultiple = false,
				AllowContainers = false,
				AllowProtected = true,
				Action = (selectedLibraryItems, listView) =>
				{
					var firstItem = selectedLibraryItems.FirstOrDefault();
					if (firstItem is SDCardFileItem sdcardItem)
					{
						ApplicationController.Instance.ActivePrintItem = new PrintItemWrapper(new PrintItem(sdcardItem.Name, QueueData.SdCardFileName));
					}
					else if (firstItem is FileSystemFileItem fileItem && Path.GetExtension(fileItem.FileName).ToUpper() == ".GCODE")
					{
						ApplicationController.Instance.ActivePrintItem = new PrintItemWrapper(new PrintItem(fileItem.Name, fileItem.Path));
					}
					else
					{
						//TODO: Otherwise add the selected items to the plate
					}

					ApplicationController.Instance.PrintActivePart();
				}
			});

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

					foreach (var item in selectedLibraryItems)
					{
						if (item is ILibraryContentStream contentModel)
						{
							var contentProvider = library.GetContentProvider(item) as ISceneContentProvider;

							var result = contentProvider?.CreateItem(item, null);

							// Wait for the content to load
							await result.MeshLoaded;

							if (result?.Object3D != null)
							{
								itemsToAdd.Add(result.Object3D);
							}
						}
						else if (item is ILibraryContentItem contentItem)
						{
							var content = await contentItem.GetContent(null);
							if (content != null)
							{
								itemsToAdd.Add(content);
							}
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
				AllowMultiple = true,
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
			menuActions.Add(new MenuSeparator("Other"));

			// PDF export is limited to Windows
			if (AggContext.OperatingSystem == OSType.Windows)
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
								AggContext.FileDialogs.SaveFileDialog(
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
						"Rename Item:".Localize(),
						selectedItem.Model.Name,
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
			var exportPage = new ExportPrintItemPage(libraryView.SelectedItems.Select(item => item.Model));

			string windowTitle = "MatterControl".Localize() + ": " + "Export File".Localize();
			WizardWindow.Show("/ExportPrintItemPage", "", exportPage);
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
					menuItem = OverflowDropdown.CreateHorizontalLine();
				}
				else
				{
					menuItem = OverflowDropdown.CreateMenuItem((string)menuAction.Title);
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
