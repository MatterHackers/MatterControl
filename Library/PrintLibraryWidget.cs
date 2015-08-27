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
using System.Text;

namespace MatterHackers.MatterControl.PrintLibrary
{
	internal class ButtonEnableData
	{
		internal bool multipleItems;
		internal bool protectedItems;

		internal ButtonEnableData(bool multipleItems, bool protectedItems)
		{
			this.multipleItems = multipleItems;
			this.protectedItems = protectedItems;
		}
	}

	public class PrintLibraryWidget : GuiWidget
	{
		private static CreateFolderWindow createFolderWindow = null;
		private static RenameItemWindow renameItemWindow = null;
		private static TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private TextImageButtonFactory editButtonFactory = new TextImageButtonFactory();
		private TextWidget navigationLabel;

		private FlowLayoutWidget itemOperationButtons;
		private FolderBreadCrumbWidget breadCrumbWidget;
		private List<ButtonEnableData> editButtonsEnableData = new List<ButtonEnableData>();

		private static Button addToLibraryButton;
		private Button enterEditModeButton;
		private Button leaveEditModeButton;
        private static FlowLayoutWidget buttonPanel;
		private MHTextEditWidget searchInput;
		private LibraryDataView libraryDataView;

		static PrintLibraryWidget currentPrintLibraryWidget;

		public PrintLibraryWidget()
		{
			currentPrintLibraryWidget = this;
			SetDisplayAttributes();

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

				FlowLayoutWidget searchPanel = new FlowLayoutWidget();
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
				CreateEditBarButtons();

				//allControls.AddChild(navigationPanel);
				allControls.AddChild(searchPanel);
				allControls.AddChild(itemOperationButtons);

				libraryDataView = new LibraryDataView();
				breadCrumbWidget = new FolderBreadCrumbWidget(libraryDataView.SetCurrentLibraryProvider, libraryDataView.CurrentLibraryProvider);
				libraryDataView.ChangedCurrentLibraryProvider += breadCrumbWidget.SetBreadCrumbs;

				libraryDataView.ChangedCurrentLibraryProvider += ClearSearchWidget;

				allControls.AddChild(breadCrumbWidget);

				allControls.AddChild(libraryDataView);
				allControls.AddChild(buttonPanel);
			}
			allControls.AnchorAll();

			this.AddChild(allControls);

			AddHandlers();
		}

		private void ClearSearchWidget(LibraryProvider previousLibraryProvider, LibraryProvider currentLibraryProvider)
		{
			previousLibraryProvider.KeywordFilter = "";
			searchInput.Text = currentLibraryProvider.KeywordFilter;
			breadCrumbWidget.SetBreadCrumbs(null, currentPrintLibraryWidget.libraryDataView.CurrentLibraryProvider);
		}

        private static void AddLibraryButtonElements()
        {
            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
		    textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
		    textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
            buttonPanel.RemoveAllChildren();	
            // the add button
			{
				addToLibraryButton = textImageButtonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
				addToLibraryButton.ToolTipText = "Add an .stl, .amf, .gcode or .zip file to the Library".Localize();
				buttonPanel.AddChild(addToLibraryButton);
				addToLibraryButton.Margin = new BorderDouble(0, 0, 3, 0);
				addToLibraryButton.Click += (sender, e) => UiThread.RunOnIdle(importToLibraryloadFile_ClickOnIdle);
			}

			// the create folder button
			{
				Button createFolderButton = textImageButtonFactory.Generate(LocalizedString.Get("Create Folder"));
				createFolderButton.Name = "Create Folder Button";
				buttonPanel.AddChild(createFolderButton);
				createFolderButton.Margin = new BorderDouble(0, 0, 3, 0);
				createFolderButton.Click += (sender, e) =>
				{
					if (createFolderWindow == null)
					{
						createFolderWindow = new CreateFolderWindow((returnInfo) =>
						{
							currentPrintLibraryWidget.libraryDataView.CurrentLibraryProvider.AddCollectionToLibrary(returnInfo.newName);
						});
						createFolderWindow.Closed += (sender2, e2) => { createFolderWindow = null; };
					}
					else
					{
						createFolderWindow.BringToFront();
					}
				};
			}

			//Add extra buttons (ex. from plugins) if available
            if (privateAddLibraryButton != null)
            {
                privateAddLibraryButton(buttonPanel);
            }

        }

        public delegate void AddLibraryButtonDelegate(GuiWidget extraButtonContainer);

        private static event AddLibraryButtonDelegate privateAddLibraryButton;
        public static event AddLibraryButtonDelegate AddLibraryButton
        {
            add
            {
                privateAddLibraryButton += value;
                // and create button container right away
                AddLibraryButtonElements();
            }

            remove
            {
                privateAddLibraryButton -= value;
            }
        }
        
		private void CreateEditBarButtons()
		{
			itemOperationButtons = new FlowLayoutWidget();
			itemOperationButtons.Visible = false;
			itemOperationButtons.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
			itemOperationButtons.HAnchor = HAnchor.Max_FitToChildren_ParentWidth;
			double oldWidth = editButtonFactory.FixedWidth;
			editButtonFactory.FixedWidth = 0;

			Button exportItemButton = editButtonFactory.Generate("Export".Localize());
			exportItemButton.Margin = new BorderDouble(3, 0);
			exportItemButton.Click += exportButton_Click;
			editButtonsEnableData.Add(new ButtonEnableData(false, false));
			itemOperationButtons.AddChild(exportItemButton);

			Button editItemButton = editButtonFactory.Generate("Edit".Localize());
			editItemButton.Margin = new BorderDouble(3, 0);
			editItemButton.Click += editButton_Click;
			editButtonsEnableData.Add(new ButtonEnableData(false, false));
			itemOperationButtons.AddChild(editItemButton);

			// add the remove button
			{
				Button removeFromLibraryButton = editButtonFactory.Generate("Remove".Localize());
				removeFromLibraryButton.Margin = new BorderDouble(3, 0);
				removeFromLibraryButton.Click += deleteFromLibraryButton_Click;
				editButtonsEnableData.Add(new ButtonEnableData(true, false));
				itemOperationButtons.AddChild(removeFromLibraryButton);
			}

			// add a rename button
			{
				Button renameFromLibraryButton = editButtonFactory.Generate("Rename".Localize());
				renameFromLibraryButton.Margin = new BorderDouble(3, 0);
				editButtonsEnableData.Add(new ButtonEnableData(false, false));
				itemOperationButtons.AddChild(renameFromLibraryButton);

				renameFromLibraryButton.Click += (sender, e) =>
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
				};
			}

			Button addToQueueButton = editButtonFactory.Generate("Add to Queue".Localize());
			addToQueueButton.Margin = new BorderDouble(3, 0);
			addToQueueButton.Click += addToQueueButton_Click;
			editButtonsEnableData.Add(new ButtonEnableData(true, true));
			itemOperationButtons.AddChild(addToQueueButton);

			editButtonFactory.FixedWidth = oldWidth;
		}

		private event EventHandler unregisterEvents;

		private void AddHandlers()
		{
			libraryDataView.SelectedItems.OnAdd += onLibraryItemsSelected;
			libraryDataView.SelectedItems.OnRemove += onLibraryItemsSelected;
		}

		public override void OnClosed(EventArgs e)
		{
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
			itemOperationButtons.Visible = true;
			enterEditModeButton.Visible = false;
			leaveEditModeButton.Visible = true;
			libraryDataView.EditMode = true;
		}

		private void leaveEditModeButtonClick(object sender, EventArgs e)
		{
			breadCrumbWidget.Visible = true;
			itemOperationButtons.Visible = false;
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

				breadCrumbWidget.SetBreadCrumbs(null, currentPrintLibraryWidget.libraryDataView.CurrentLibraryProvider);
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
			SetEditButtonsStates();
		}

		private async void SetEditButtonsStates()
		{
			int selectedCount = libraryDataView.SelectedItems.Count;

			for(int buttonIndex=0; buttonIndex<itemOperationButtons.Children.Count; buttonIndex++)
			{
				bool enabledStateToSet = (selectedCount > 0);
				var child = itemOperationButtons.Children[buttonIndex];
				var button = child as Button;
				if (button != null)
				{
					if ((selectedCount > 1 && !editButtonsEnableData[buttonIndex].multipleItems))
					{
						enabledStateToSet = false;
					}
					else
					{
						bool enabledState = enabledStateToSet;

						if (!editButtonsEnableData[buttonIndex].protectedItems)
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
					}

					button.Enabled = enabledStateToSet;
				}
			}
		}

		private void SetDisplayAttributes()
		{
			this.Padding = new BorderDouble(3);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.AnchorAll();
		}

		private void deleteFromLibraryButton_Click(object sender, EventArgs e)
		{
			foreach (LibraryRowItem item in libraryDataView.SelectedItems)
			{
				item.RemoveFromCollection();
			}

			libraryDataView.ClearSelectedItems();
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
			base.OnDragEnter(fileDropEventArgs);
		}

		public override void OnDragOver(FileDropEventArgs fileDropEventArgs)
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
			base.OnDragOver(fileDropEventArgs);
		}

		public override void OnDragDrop(FileDropEventArgs fileDropEventArgs)
		{
			libraryDataView.CurrentLibraryProvider.AddFilesToLibrary(fileDropEventArgs.DroppedFiles);

			base.OnDragDrop(fileDropEventArgs);
		}

		private static void importToLibraryloadFile_ClickOnIdle()
		{
			OpenFileDialogParams openParams = new OpenFileDialogParams(ApplicationSettings.OpenPrintableFileParams, multiSelect: true);
			FileDialog.OpenFileDialog(openParams, onLibraryLoadFileSelected);
		}

		private static void onLibraryLoadFileSelected(OpenFileDialogParams openParams)
		{
			if (openParams.FileNames != null)
			{
				currentPrintLibraryWidget.libraryDataView.CurrentLibraryProvider.AddFilesToLibrary(openParams.FileNames, null);
			}
		}
	}
}