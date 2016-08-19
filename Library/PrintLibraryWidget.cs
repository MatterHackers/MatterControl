/*
Copyright (c) 2014, Kevin Pope
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.CustomWidgets.LibrarySelector;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.PrintLibrary
{
	internal class MenuEnableData
	{
		internal bool multipleItems;
		internal bool protectedItems;
		internal bool collectionItems;
        internal bool shareItems;
		internal MenuItem menuItemToChange;

		internal MenuEnableData(MenuItem menuItemToChange, bool multipleItems, bool protectedItems, bool collectionItems, bool shareItems = false)
		{
			this.menuItemToChange = menuItemToChange;
			this.multipleItems = multipleItems;
			this.protectedItems = protectedItems;
			this.collectionItems = collectionItems;
            this.shareItems = shareItems;
		}
	}

	public class PrintLibraryWidget : GuiWidget
	{
		private static CreateFolderWindow createFolderWindow = null;
		private static RenameItemWindow renameItemWindow = null;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private TextImageButtonFactory editButtonFactory = new TextImageButtonFactory();
		private TextWidget navigationLabel;

		FlowLayoutWidget breadCrumbAndActionBar;
		private FolderBreadCrumbWidget breadCrumbWidget;
		private List<MenuEnableData> actionMenuEnableData = new List<MenuEnableData>();

		private Button addToLibraryButton;
		private Button createFolderButton;
		private Button enterEditModeButton;
		private Button leaveEditModeButton;
        private FlowLayoutWidget buttonPanel;
		private MHTextEditWidget searchInput;
		private LibraryDataView libraryDataView;
		private GuiWidget providerMessageContainer;
		private TextWidget providerMessageWidget;

		private GuiWidget searchPanel;

		static PrintLibraryWidget currentPrintLibraryWidget;

		public static void Reload()
		{
			// Unhook events and close active instance
			if (currentPrintLibraryWidget.libraryDataView != null)
			{
				currentPrintLibraryWidget.libraryDataView.SelectedItems.OnAdd -= currentPrintLibraryWidget.onLibraryItemsSelected;
				currentPrintLibraryWidget.libraryDataView.SelectedItems.OnRemove -= currentPrintLibraryWidget.onLibraryItemsSelected;

				currentPrintLibraryWidget.CloseAllChildren();
			}

			// Load and initialize
			currentPrintLibraryWidget.LoadContent();
			currentPrintLibraryWidget.libraryDataView.SelectedItems.OnAdd += currentPrintLibraryWidget.onLibraryItemsSelected;
			currentPrintLibraryWidget.libraryDataView.SelectedItems.OnRemove += currentPrintLibraryWidget.onLibraryItemsSelected;
		}

		public PrintLibraryWidget()
		{
			currentPrintLibraryWidget = this;
			Reload();
		}

		private void LoadContent()
		{
			this.Padding = new BorderDouble(3);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.AnchorAll();

			textImageButtonFactory.borderWidth = 0;

			editButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			editButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			editButtonFactory.disabledTextColor = ActiveTheme.Instance.TabLabelUnselected;
			editButtonFactory.disabledFillColor = new RGBA_Bytes();
			editButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			editButtonFactory.borderWidth = 0;
			editButtonFactory.Margin = new BorderDouble(10, 0);

			FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				enterEditModeButton = editButtonFactory.Generate("Edit".Localize(), centerText: true);
				enterEditModeButton.Click += enterEditModeButtonClick;

				leaveEditModeButton = editButtonFactory.Generate("Done".Localize(), centerText: true);
				leaveEditModeButton.Click += leaveEditModeButtonClick;

				// make sure the buttons are the same size even when localized
				if (leaveEditModeButton.Width < enterEditModeButton.Width)
				{
					editButtonFactory.FixedWidth = enterEditModeButton.Width;
					leaveEditModeButton = editButtonFactory.Generate("Done".Localize(), centerText: true);
					leaveEditModeButton.Click += leaveEditModeButtonClick;
				}
				else
				{
					editButtonFactory.FixedWidth = leaveEditModeButton.Width;
					enterEditModeButton = editButtonFactory.Generate("Edit".Localize(), centerText: true);
					enterEditModeButton.Click += enterEditModeButtonClick;
				}

				enterEditModeButton.Name = "Library Edit Button";

				leaveEditModeButton.Visible = false;

				FlowLayoutWidget navigationPanel = new FlowLayoutWidget();
				navigationPanel.HAnchor = HAnchor.ParentLeftRight;
				navigationPanel.Padding = new BorderDouble(0);
				navigationPanel.BackgroundColor = ActiveTheme.Instance.TransparentLightOverlay;

				navigationLabel = new TextWidget("My Library".Localize(), pointSize: 14);
				navigationLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
				navigationLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;

				navigationPanel.AddChild(new GuiWidget(50, 0)); //Add this as temporary balance to edit buttons
				navigationPanel.AddChild(new HorizontalSpacer());
				navigationPanel.AddChild(navigationLabel);
				navigationPanel.AddChild(new HorizontalSpacer());

				buttonPanel = new FlowLayoutWidget();
				buttonPanel.HAnchor = HAnchor.ParentLeftRight;
				buttonPanel.Padding = new BorderDouble(0, 3);
				buttonPanel.MinimumSize = new Vector2(0, 46);

				AddLibraryButtonElements();

				//allControls.AddChild(navigationPanel);
				searchPanel = CreateSearchPannel();
				allControls.AddChild(searchPanel);

				libraryDataView = new LibraryDataView();
				breadCrumbWidget = new FolderBreadCrumbWidget(libraryDataView.SetCurrentLibraryProvider, libraryDataView.CurrentLibraryProvider);
				FlowLayoutWidget breadCrumbSpaceHolder = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.ParentLeftRight,
				};
				breadCrumbSpaceHolder.AddChild(breadCrumbWidget);
				libraryDataView.ChangedCurrentLibraryProvider += breadCrumbWidget.SetBreadCrumbs;

				libraryDataView.ChangedCurrentLibraryProvider += LibraryProviderChanged;
				breadCrumbAndActionBar = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.ParentLeftRight,
				};

				breadCrumbAndActionBar.AddChild(breadCrumbSpaceHolder);
				breadCrumbAndActionBar.AddChild(CreateActionsMenu());

				allControls.AddChild(breadCrumbAndActionBar);

				allControls.AddChild(libraryDataView);
				allControls.AddChild(buttonPanel);
			}

			allControls.AnchorAll();

			this.AddChild(allControls);
		}

		private GuiWidget CreateSearchPannel()
		{
			GuiWidget searchPanel = new FlowLayoutWidget();
			searchPanel.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
			searchPanel.HAnchor = HAnchor.ParentLeftRight;
			searchPanel.Padding = new BorderDouble(0);
			{
				searchInput = new MHTextEditWidget(messageWhenEmptyAndNotSelected: "Search Library".Localize());
				searchInput.Name = "Search Library Edit";
				searchInput.Margin = new BorderDouble(0, 3, 0, 0);
				searchInput.HAnchor = HAnchor.ParentLeftRight;
				searchInput.VAnchor = VAnchor.ParentCenter;
				searchInput.ActualTextEditWidget.EnterPressed += new KeyEventHandler(searchInputEnterPressed);

				double oldWidth = editButtonFactory.FixedWidth;
				editButtonFactory.FixedWidth = 0;
				Button searchButton = editButtonFactory.Generate(LocalizedString.Get("Search"), centerText: true);
				searchButton.Name = "Search Library Button";
				searchButton.Click += searchButtonClick;
				editButtonFactory.FixedWidth = oldWidth;

				searchPanel.AddChild(enterEditModeButton);
				searchPanel.AddChild(leaveEditModeButton);
				searchPanel.AddChild(searchInput);
				searchPanel.AddChild(searchButton);
			}

			searchPanel.Visible = false;
			return searchPanel;
		}

		private GuiWidget CreateActionsMenu()
		{
			var actionMenu = new DropDownMenu("Action".Localize() + "... ");
			actionMenu.AlignToRightEdge = true;
			actionMenu.NormalColor = new RGBA_Bytes();
			actionMenu.BorderWidth = 1;
			actionMenu.BorderColor = new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 100);
			actionMenu.MenuAsWideAsItems = false;
			actionMenu.VAnchor = VAnchor.ParentBottomTop;
			actionMenu.Margin = new BorderDouble(3);
			actionMenu.Padding = new BorderDouble(10);
			actionMenu.Name = "LibraryActionMenu";

			CreateActionMenuItems(actionMenu);
			return actionMenu;
		}

		public string ProviderMessage
		{
			get { return providerMessageWidget.Text; }
			set
			{
				if (value != "")
				{
					providerMessageWidget.Text = value;
					providerMessageContainer.Visible = true;
				}
				else
				{
					providerMessageContainer.Visible = false;
				}
			}
		}

		private void LibraryProviderChanged(LibraryProvider previousLibraryProvider, LibraryProvider currentLibraryProvider)
		{
			if (currentLibraryProvider.IsProtected())
			{
				addToLibraryButton.Enabled = false;
				createFolderButton.Enabled = false;
				searchPanel.Visible = false;
				DoLeaveEditMode();
			}
			else
			{
				addToLibraryButton.Enabled = true;
				createFolderButton.Enabled = true;
				searchPanel.Visible = true;
			}

			if (previousLibraryProvider != null)
			{
				previousLibraryProvider.KeywordFilter = "";
				previousLibraryProvider.DataReloaded -= UpdateStatus;
			}

			searchInput.Text = currentLibraryProvider.KeywordFilter;
			breadCrumbWidget.SetBreadCrumbs(null, this.libraryDataView.CurrentLibraryProvider);

			currentLibraryProvider.DataReloaded += UpdateStatus;

			UpdateStatus(null, null);
		}

		void UpdateStatus(object sender, EventArgs e)
		{
			if (this.libraryDataView.CurrentLibraryProvider != null)
			{
				this.ProviderMessage = this.libraryDataView.CurrentLibraryProvider.StatusMessage;
			}
		}

        private void AddLibraryButtonElements()
		{
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.TabLabelUnselected;
			textImageButtonFactory.disabledFillColor = new RGBA_Bytes();
			buttonPanel.RemoveAllChildren();
			// the add button
			{
				addToLibraryButton = textImageButtonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
				addToLibraryButton.Enabled = false; // The library selector (the first library selected) is protected so we can't add to it. 
				addToLibraryButton.ToolTipText = "Add an .stl, .amf, .gcode or .zip file to the Library".Localize();
				addToLibraryButton.Name = "Library Add Button";
				buttonPanel.AddChild(addToLibraryButton);
				addToLibraryButton.Margin = new BorderDouble(0, 0, 3, 0);
				addToLibraryButton.Click += (sender, e) => UiThread.RunOnIdle(importToLibraryloadFile_ClickOnIdle);
			}

			// the create folder button
			{
				createFolderButton = textImageButtonFactory.Generate(LocalizedString.Get("Create Folder"));
				createFolderButton.Enabled = false; // The library selector (the first library selected) is protected so we can't add to it.
				createFolderButton.Name = "Create Folder From Library Button";
				buttonPanel.AddChild(createFolderButton);
				createFolderButton.Margin = new BorderDouble(0, 0, 3, 0);
				createFolderButton.Click += (sender, e) =>
				{
					if (createFolderWindow == null)
					{
						createFolderWindow = new CreateFolderWindow((returnInfo) =>
						{
							this.libraryDataView.CurrentLibraryProvider.AddCollectionToLibrary(returnInfo.newName);
						});
						createFolderWindow.Closed += (sender2, e2) => { createFolderWindow = null; };
					}
					else
					{
						createFolderWindow.BringToFront();
					}
				};
			}

			// add in the message widget
			{
				providerMessageWidget = new TextWidget("")
				{
					PointSize = 8,
					HAnchor = HAnchor.ParentRight,
					VAnchor = VAnchor.ParentBottom,
					TextColor = ActiveTheme.Instance.SecondaryTextColor,
					Margin = new BorderDouble(6),
					AutoExpandBoundsToText = true,
				};

				providerMessageContainer = new GuiWidget()
				{
					VAnchor = VAnchor.FitToChildren | VAnchor.ParentTop,
					HAnchor = HAnchor.ParentLeftRight,
					Visible = false,
				};

				providerMessageContainer.AddChild(providerMessageWidget);
				buttonPanel.AddChild(providerMessageContainer, -1);
			}
		}

		List<PrintItemAction> menuItems = new List<PrintItemAction>();
		private void CreateActionMenuItems(DropDownMenu actionMenu)
		{
			actionMenu.SelectionChanged += (sender, e) =>
			{
				string menuSelection = ((DropDownMenu)sender).SelectedValue;
				foreach (var menuItem in menuItems)
				{
					if (menuItem.Title == menuSelection)
					{
						menuItem.Action?.Invoke(null, null);
					}
				}
			};

			// edit menu item
			menuItems.Add(new PrintItemAction()
			{
				Title = "Edit".Localize(),
				Action = (s, e) => editButton_Click(s, null)
			});
			actionMenuEnableData.Add(new MenuEnableData(
				actionMenu.AddItem(menuItems[menuItems.Count - 1].Title),
				false, false, false));

			actionMenu.AddHorizontalLine();

			// rename menu item
			menuItems.Add(new PrintItemAction()
			{
				Title = "Rename".Localize(),
				Action = (s, e) => renameFromLibraryButton_Click(s, null)
			});
			actionMenuEnableData.Add(new MenuEnableData(actionMenu.AddItem(menuItems[menuItems.Count - 1].Title), false, false, true));

			// move menu item
			menuItems.Add(new PrintItemAction()
			{
				Title = "Move".Localize(),
				Action = (s, e) => moveInLibraryButton_Click(s, null)
			});
			//actionMenuEnableData.Add(new MenuEnableData(actionMenu.AddItem(menuItems[menuItems.Count - 1].Title), true, false, true));

			// remove menu item
			menuItems.Add(new PrintItemAction()
			{
				Title = "Remove".Localize(),
				Action = (s, e) => deleteFromLibraryButton_Click(s, null)
			});
			actionMenuEnableData.Add(new MenuEnableData(
				actionMenu.AddItem(menuItems[menuItems.Count - 1].Title),
				true, false, true));

			actionMenu.AddHorizontalLine();

			// add to queue menu item
			menuItems.Add(new PrintItemAction()
			{
				Title = "Add to Queue".Localize(),
				Action = (s, e) => addToQueueButton_Click(s, null)
			});
			actionMenuEnableData.Add(new MenuEnableData(
				actionMenu.AddItem(menuItems[menuItems.Count - 1].Title),
				true, true, false));

			// export menu item
			menuItems.Add(new PrintItemAction()
			{
				Title = "Export".Localize(),
				Action = (s, e) => exportButton_Click(s, null)
			});
			actionMenuEnableData.Add(new MenuEnableData(
				actionMenu.AddItem(menuItems[menuItems.Count - 1].Title),
				false, false, false));

			// share menu item
			menuItems.Add(new PrintItemAction()
			{
				Title = "Share".Localize(),
				Action = (s, e) => shareFromLibraryButton_Click(s, null)
			});
			actionMenuEnableData.Add(new MenuEnableData(
				actionMenu.AddItem(menuItems[menuItems.Count - 1].Title),
				false, false, false, true));

			SetActionMenuStates();
		}

		private void renameFromLibraryButton_Click(IEnumerable<QueueRowItem> s, object p)
		{
			if (libraryDataView.SelectedItems.Count == 1)
			{
				if (renameItemWindow == null)
				{
					LibraryRowItem rowItem = libraryDataView.SelectedItems[0];
					LibraryRowItemPart partItem = rowItem as LibraryRowItemPart;
					LibraryRowItemCollection collectionItem = rowItem as LibraryRowItemCollection;

					string currentName = libraryDataView.SelectedItems[0].ItemName;

					renameItemWindow = new RenameItemWindow(currentName, (returnInfo) =>
					{
						if (partItem != null)
						{
							libraryDataView.CurrentLibraryProvider.RenameItem(partItem.ItemIndex, returnInfo.newName);
						}
						else if (collectionItem != null)
						{
							libraryDataView.CurrentLibraryProvider.RenameCollection(collectionItem.CollectionIndex, returnInfo.newName);
						}

						libraryDataView.ClearSelectedItems();
					});

					renameItemWindow.Closed += (sender2, e2) => { renameItemWindow = null; };
				}
				else
				{
					renameItemWindow.BringToFront();
				}

				/*
				LibraryDataView.CurrentLibraryProvider.RenameCollection(collectionIndex, newName);

				LibraryRowItem libraryItem = libraryDataView.SelectedItems[0];
				libraryItem.RenameThisInPrintLibrary(newName.Data);

				 */
			}
		}

		private event EventHandler unregisterEvents;

		public override void OnClosed(EventArgs e)
		{
			if (this.libraryDataView != null
				&& this.libraryDataView.CurrentLibraryProvider != null)
			{
				this.libraryDataView.CurrentLibraryProvider.DataReloaded -= UpdateStatus;
			}

			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private void searchInputEnterPressed(object sender, KeyEventArgs keyEvent)
		{
			searchButtonClick(null, null);
		}

		private void enterEditModeButtonClick(object sender, EventArgs e)
		{
			breadCrumbWidget.Visible = false;
			enterEditModeButton.Visible = false;
			leaveEditModeButton.Visible = true;
			libraryDataView.EditMode = true;
		}

		private void leaveEditModeButtonClick(object sender, EventArgs e)
		{
			DoLeaveEditMode();
		}

		void DoLeaveEditMode()
		{
			breadCrumbWidget.Visible = true;
			enterEditModeButton.Visible = true;
			leaveEditModeButton.Visible = false;
			libraryDataView.EditMode = false;
		}

		private void searchButtonClick(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				string searchText = searchInput.Text.Trim();

				libraryDataView.CurrentLibraryProvider.KeywordFilter = searchText;

				breadCrumbWidget.SetBreadCrumbs(null, this.libraryDataView.CurrentLibraryProvider);
			});
		}

		private void addToQueueButton_Click(object sender, EventArgs e)
		{
			foreach (LibraryRowItem item in libraryDataView.SelectedItems)
			{
				item.AddToQueue();
			}
			libraryDataView.ClearSelectedItems();
		}

		private void onLibraryItemsSelected(object sender, EventArgs e)
		{
			SetActionMenuStates();
		}

		private void SetActionMenuStates()
		{
			int selectedCount = libraryDataView.SelectedItems.Count;

			for (int menuIndex = 0; menuIndex < actionMenuEnableData.Count; menuIndex++)
			{
				bool enabledStateToSet = (selectedCount > 0);
				if ((selectedCount > 1 && !actionMenuEnableData[menuIndex].multipleItems))
				{
					enabledStateToSet = false;
				}
				else
				{
					if (!actionMenuEnableData[menuIndex].protectedItems)
					{
						// so we can show for multi items lets check for protected items
						for (int itemIndex = 0; itemIndex < libraryDataView.SelectedItems.Count; itemIndex++)
						{
							if (libraryDataView.SelectedItems[itemIndex].Protected)
							{
								enabledStateToSet = false;
							}
						}
					}

					if (!actionMenuEnableData[menuIndex].collectionItems)
					{
						// so we can show for multi items lets check for protected items
						for (int itemIndex = 0; itemIndex < libraryDataView.SelectedItems.Count; itemIndex++)
						{
							bool isColection = (libraryDataView.SelectedItems[itemIndex] as LibraryRowItemCollection) != null;
							if (isColection)
							{
								enabledStateToSet = false;
							}
						}
					}
				}

				if (actionMenuEnableData[menuIndex].shareItems)
				{
					if (libraryDataView?.CurrentLibraryProvider != null)
					{
						if (!libraryDataView.CurrentLibraryProvider.CanShare)
						{
							enabledStateToSet = false;
						}
					}
				}

				actionMenuEnableData[menuIndex].menuItemToChange.Enabled = enabledStateToSet;
			}
		}

		public int SortRowItemsOnIndex(LibraryRowItem x, LibraryRowItem y)
		{
			int xIndex = libraryDataView.GetItemIndex(x);
			int yIndex = libraryDataView.GetItemIndex(y);

			return xIndex.CompareTo(yIndex);
		}

		private void deleteFromLibraryButton_Click(object sender, EventArgs e)
		{
			libraryDataView.SelectedItems.Sort(SortRowItemsOnIndex);

			IEnumerable<LibraryRowItem> partItems = libraryDataView.SelectedItems.Where(item => item is LibraryRowItemPart);
			if(partItems.Count() == libraryDataView.SelectedItems.Count)
			{
				// If all selected items are LibraryRowItemParts, then we can invoke the batch remove functionality (in the Cloud library scenario)
				// and perform all deletes as part of a single request, with a single notification from Socketeer
				var indexesToRemove = partItems.Cast<LibraryRowItemPart>().Select(l => l.ItemIndex).ToArray();
				libraryDataView.CurrentLibraryProvider.RemoveItems(indexesToRemove);
			}
			else
			{
				// Otherwise remove each item last to first
				for (int i = libraryDataView.SelectedItems.Count - 1; i >= 0; i--)
				{
					LibraryRowItem item = libraryDataView.SelectedItems[i];
					item.RemoveFromCollection();
				}
			}

			libraryDataView.ClearSelectedItems();
		}

		private void moveInLibraryButton_Click(object sender, EventArgs e)
		{
			libraryDataView.SelectedItems.Sort(SortRowItemsOnIndex);

			IEnumerable<LibraryRowItem> partItems = libraryDataView.SelectedItems.Where(item => item is LibraryRowItemPart);
			if (partItems.Count() > 0)
			{
				// If all selected items are LibraryRowItemParts, then we can invoke the batch remove functionality (in the Cloud library scenario)
				// and perform all moves as part of a single request, with a single notification from Socketeer
				var indexesToRemove = partItems.Cast<LibraryRowItemPart>().Select(l => l.ItemIndex).ToArray();
				libraryDataView.CurrentLibraryProvider.MoveItems(indexesToRemove);
			}

			libraryDataView.ClearSelectedItems();
		}

		private void shareFromLibraryButton_Click(object sender, EventArgs e)
        {
            if (libraryDataView.SelectedItems.Count == 1)
            {
                LibraryRowItem rowItem = libraryDataView.SelectedItems[0];
                LibraryRowItemPart partItem = rowItem as LibraryRowItemPart;
                if (partItem != null)
                {
                    libraryDataView.CurrentLibraryProvider.ShareItem(partItem.ItemIndex);
                }
            }
        }

		private void exportButton_Click(object sender, EventArgs e)
		{
			//Open export options
			if (libraryDataView.SelectedItems.Count == 1)
			{
				LibraryRowItem libraryItem = libraryDataView.SelectedItems[0];
				libraryItem.Export();
			}
		}

		private void editButton_Click(object sender, EventArgs e)
		{
			//Open export options
			if (libraryDataView.SelectedItems.Count == 1)
			{
				LibraryRowItem libraryItem = libraryDataView.SelectedItems[0];
				libraryItem.Edit();
			}
		}

		public override void OnDragEnter(FileDropEventArgs fileDropEventArgs)
		{
			if (libraryDataView != null
				&& libraryDataView.CurrentLibraryProvider != null
				&& !libraryDataView.CurrentLibraryProvider.IsProtected())
			{
				foreach (string file in fileDropEventArgs.DroppedFiles)
				{
					string extension = Path.GetExtension(file).ToUpper();
					if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
						|| extension == ".GCODE"
						|| extension == ".ZIP")
					{
						fileDropEventArgs.AcceptDrop = true;
					}
				}
			}
			base.OnDragEnter(fileDropEventArgs);
		}

		public override void OnDragOver(FileDropEventArgs fileDropEventArgs)
		{
			if (libraryDataView != null
				&& libraryDataView.CurrentLibraryProvider != null
				&& !libraryDataView.CurrentLibraryProvider.IsProtected())
			{
				foreach (string file in fileDropEventArgs.DroppedFiles)
				{
					string extension = Path.GetExtension(file).ToUpper();
					if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
						|| extension == ".GCODE"
						|| extension == ".ZIP")
					{
						fileDropEventArgs.AcceptDrop = true;
						break;
					}
				}
			}
			base.OnDragOver(fileDropEventArgs);
		}

		public override void OnDragDrop(FileDropEventArgs fileDropEventArgs)
		{
			if (libraryDataView != null
				&& libraryDataView.CurrentLibraryProvider != null
				&& !libraryDataView.CurrentLibraryProvider.IsProtected())
			{
				libraryDataView.CurrentLibraryProvider.AddFilesToLibrary(fileDropEventArgs.DroppedFiles);
			}

			base.OnDragDrop(fileDropEventArgs);
		}

		private void importToLibraryloadFile_ClickOnIdle()
		{
			OpenFileDialogParams openParams = new OpenFileDialogParams(ApplicationSettings.OpenPrintableFileParams, multiSelect: true);
			FileDialog.OpenFileDialog(openParams, onLibraryLoadFileSelected);
		}

		private void onLibraryLoadFileSelected(OpenFileDialogParams openParams)
		{
			if (openParams.FileNames != null)
			{
				this.libraryDataView.CurrentLibraryProvider.AddFilesToLibrary(openParams.FileNames, null);
			}
		}
	}
}
