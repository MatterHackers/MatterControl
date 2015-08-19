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
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class QueueDataWidget : GuiWidget
	{
		public static SendButtonAction sendButtonFunction = null;
		private static Button shopButton;
		Button addToQueueButton;
		private Button createButton;
		private TextImageButtonFactory editButtonFactory = new TextImageButtonFactory();
		private FlowLayoutWidget itemOperationButtons;
		private DropDownMenu moreMenu;
		private List<ButtonEnableData> editButtonsEnableData = new List<ButtonEnableData>();
		private Button enterEditModeButton;
		private ExportPrintItemWindow exportingWindow;
		private bool exportingWindowIsOpen = false;
		private Button leaveEditModeButton;
		private TupleList<string, Func<bool>> menuItems;
		private PluginChooserWindow pluginChooserWindow;
		private QueueDataView queueDataView;
		private QueueOptionsMenu queueMenu;
		private FlowLayoutWidget queueMenuContainer;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		public QueueDataWidget(QueueDataView queueDataView)
		{
			this.queueDataView = queueDataView;

			SetDisplayAttributes();

			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.borderWidth = 0;

			editButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			editButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			editButtonFactory.disabledTextColor = ActiveTheme.Instance.TabLabelUnselected;
			editButtonFactory.disabledFillColor = new RGBA_Bytes();
			editButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			editButtonFactory.borderWidth = 0;
			editButtonFactory.Margin = new BorderDouble(10, 0);
			editButtonFactory.Margin *= TextWidget.GlobalPointSizeScaleRatio;

			FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				enterEditModeButton = editButtonFactory.Generate("Edit".Localize(), centerText: true);
				enterEditModeButton.ToolTipText = "Enter Multi Select mode".Localize();
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

				CreateEditBarButtons();
				leaveEditModeButton.Visible = false;

				FlowLayoutWidget topBarContainer = new FlowLayoutWidget();
				topBarContainer.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
				topBarContainer.HAnchor = HAnchor.ParentLeftRight;
				topBarContainer.Padding = new BorderDouble(0);

				topBarContainer.AddChild(enterEditModeButton);

				topBarContainer.AddChild(leaveEditModeButton);
				topBarContainer.AddChild(new HorizontalSpacer());
				topBarContainer.AddChild(itemOperationButtons);

				// put in the itme edit menu
				{
					moreMenu = new DropDownMenu("More".Localize() + "... ");
					moreMenu.NormalColor = new RGBA_Bytes();
					moreMenu.BorderWidth = 1;
					moreMenu.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
					moreMenu.MenuAsWideAsItems = false;
					moreMenu.VAnchor = VAnchor.ParentBottomTop;
					moreMenu.Margin = new BorderDouble(3, 3);
					moreMenu.AlignToRightEdge = true;

					topBarContainer.AddChild(moreMenu);
					SetMenuItems(moreMenu);
					moreMenu.SelectionChanged += new EventHandler(ItemMenu_SelectionChanged);
				}

				allControls.AddChild(topBarContainer);

				{
					// Ensure the form opens with no rows selected.
					//ActiveQueueList.Instance.ClearSelected();

					allControls.AddChild(queueDataView);
				}

				FlowLayoutWidget buttonPanel1 = new FlowLayoutWidget();
				buttonPanel1.HAnchor = HAnchor.ParentLeftRight;
				buttonPanel1.Padding = new BorderDouble(0, 3);
				buttonPanel1.MinimumSize = new Vector2(0, 46);
				{
					addToQueueButton = textImageButtonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
					addToQueueButton.ToolTipText = "Add an .stl, .amf, .gcode or .zip file to the Queue".Localize();
                    buttonPanel1.AddChild(addToQueueButton);
					addToQueueButton.Margin = new BorderDouble(0, 0, 3, 0);
					addToQueueButton.Click += new EventHandler(addToQueueButton_Click);

					// put in the creator button
					{
						createButton = textImageButtonFactory.Generate(LocalizedString.Get("Create"), "icon_creator_white_32x32.png");
						createButton.ToolTipText = "Choose a Create Tool to generate custom designs".Localize();
                        buttonPanel1.AddChild(createButton);
						createButton.Margin = new BorderDouble(0, 0, 3, 0);
						createButton.Click += (sender, e) =>
						{
							OpenPluginChooserWindow();
						};
					}

					bool touchScreenMode = ActiveTheme.Instance.IsTouchScreen;

					if (!touchScreenMode)
					{
						if (OemSettings.Instance.ShowShopButton)
						{
							shopButton = textImageButtonFactory.Generate(LocalizedString.Get("Buy Materials"), "icon_shopping_cart_32x32.png");
							shopButton.ToolTipText = "Shop online for printing materials".Localize();
							buttonPanel1.AddChild(shopButton);
							shopButton.Margin = new BorderDouble(0, 0, 3, 0);
							shopButton.Click += (sender, e) =>
							{
								double activeFilamentDiameter = 0;
								if (ActivePrinterProfile.Instance.ActivePrinter != null)
								{
									activeFilamentDiameter = 3;
									if (ActiveSliceSettings.Instance.FilamentDiameter < 2)
									{
										activeFilamentDiameter = 1.75;
									}
								}

								MatterControlApplication.Instance.LaunchBrowser("http://www.matterhackers.com/mc/store/redirect?d={0}&clk=mcs&a={1}".FormatWith(activeFilamentDiameter, OemSettings.Instance.AffiliateCode));
							};
						}
					}

					buttonPanel1.AddChild(new HorizontalSpacer());

					queueMenuContainer = new FlowLayoutWidget();
					queueMenuContainer.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
					queueMenu = new QueueOptionsMenu();
					queueMenuContainer.AddChild(queueMenu.MenuDropList);
					if (!touchScreenMode)
					{
						buttonPanel1.AddChild(queueMenuContainer);
					}

					ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent((object sender, EventArgs e) =>
					{
						queueMenuContainer.RemoveAllChildren();
						// the printer changed reload the queueMenue
						queueMenu = new QueueOptionsMenu();
						queueMenuContainer.AddChild(queueMenu.MenuDropList);
					}, ref unregisterEvents);
				}
				allControls.AddChild(buttonPanel1);
			}
			allControls.AnchorAll();

			this.AddChild(allControls);
			AddHandlers();

			//enterEditModeButtonClick(null, null);
		}

		private void CreateEditBarButtons()
		{
			itemOperationButtons = new FlowLayoutWidget();
			double oldWidth = editButtonFactory.FixedWidth;
			editButtonFactory.FixedWidth = 0;

			Button exportItemButton = editButtonFactory.Generate("Export".Localize());
			exportItemButton.Margin = new BorderDouble(3, 0);
			exportItemButton.Click += new EventHandler(exportButton_Click);
			editButtonsEnableData.Add(new ButtonEnableData(false, false));
			itemOperationButtons.AddChild(exportItemButton);

			Button copyItemButton = editButtonFactory.Generate("Copy".Localize());
			copyItemButton.Margin = new BorderDouble(3, 0);
			copyItemButton.Click += new EventHandler(copyButton_Click);
			editButtonsEnableData.Add(new ButtonEnableData(false, true));
			itemOperationButtons.AddChild(copyItemButton);

			Button removeItemButton = editButtonFactory.Generate("Remove".Localize());
			removeItemButton.Margin = new BorderDouble(3, 0);
			removeItemButton.Click += new EventHandler(removeButton_Click);
			editButtonsEnableData.Add(new ButtonEnableData(true, true));
			itemOperationButtons.AddChild(removeItemButton);

			editButtonFactory.FixedWidth = oldWidth;
		}

		public delegate void SendButtonAction(object state, List<PrintItemWrapper> sendItems);

		private event EventHandler unregisterEvents;

		public void CreateCopyInQueue()
		{
			// Guard for single item selection
			if (this.queueDataView.SelectedItems.Count != 1) return;

			var queueRowItem = this.queueDataView.SelectedItems[0];

			var printItemWrapper = queueRowItem.PrintItemWrapper;

			int thisIndexInQueue = QueueData.Instance.GetIndex(printItemWrapper);
			if (thisIndexInQueue != -1 && File.Exists(printItemWrapper.FileLocation))
			{
				string libraryDataPath = ApplicationDataStorage.Instance.ApplicationLibraryDataPath;
				if (!Directory.Exists(libraryDataPath))
				{
					Directory.CreateDirectory(libraryDataPath);
				}

				string newCopyFilename;
				int infiniteBlocker = 0;
				do
				{
					newCopyFilename = Path.Combine(libraryDataPath, Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(printItemWrapper.FileLocation)));
					newCopyFilename = Path.GetFullPath(newCopyFilename);
					infiniteBlocker++;
				} while (File.Exists(newCopyFilename) && infiniteBlocker < 100);

				File.Copy(printItemWrapper.FileLocation, newCopyFilename);

				string newName = printItemWrapper.Name;

				if (!newName.Contains(" - copy"))
				{
					newName += " - copy";
				}
				else
				{
					int index = newName.LastIndexOf(" - copy");
					newName = newName.Substring(0, index) + " - copy";
				}

				int copyNumber = 2;
				string testName = newName;
				string[] itemNames = QueueData.Instance.GetItemNames();
				// figure out if we have a copy already and increment the number if we do
				while (true)
				{
					if (itemNames.Contains(testName))
					{
						testName = "{0} {1}".FormatWith(newName, copyNumber);
						copyNumber++;
					}
					else
					{
						break;
					}
				}
				newName = testName;

				PrintItem newPrintItem = new PrintItem();
				newPrintItem.Name = newName;
				newPrintItem.FileLocation = newCopyFilename;
				newPrintItem.ReadOnly = printItemWrapper.PrintItem.ReadOnly;
				newPrintItem.Protected = printItemWrapper.PrintItem.Protected;
				UiThread.RunOnIdle(AddPartCopyToQueue, new PartToAddToQueue()
				{
					PrintItem = newPrintItem,
					InsertAfterIndex = thisIndexInQueue + 1
				});
			}
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		public override void OnDragDrop(FileDropEventArgs fileDropEventArgs)
		{
			int preAddCount = QueueData.Instance.Count;

			foreach (string droppedFileName in fileDropEventArgs.DroppedFiles)
			{
				string extension = Path.GetExtension(droppedFileName).ToUpper();
				if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
					|| extension == ".GCODE")
				{
					QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(droppedFileName), Path.GetFullPath(droppedFileName))));
				}
				else if (extension == ".ZIP")
				{
					ProjectFileHandler project = new ProjectFileHandler(null);
					List<PrintItem> partFiles = project.ImportFromProjectArchive(droppedFileName);
					if (partFiles != null)
					{
						foreach (PrintItem part in partFiles)
						{
							QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(part.Name, part.FileLocation)));
						}
					}
				}
			}

			if (QueueData.Instance.Count != preAddCount)
			{
				QueueData.Instance.SelectedIndex = QueueData.Instance.Count - 1;
			}

			base.OnDragDrop(fileDropEventArgs);
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
				}
			}
			base.OnDragOver(fileDropEventArgs);
		}

		private void AddHandlers()
		{
			queueDataView.SelectedItems.OnAdd += onLibraryItemsSelectChanged;
			queueDataView.SelectedItems.OnRemove += onLibraryItemsSelectChanged;
			QueueData.Instance.SelectedIndexChanged.RegisterEvent(PrintItemSelectionChanged, ref unregisterEvents);
		}

		void PrintItemSelectionChanged(object sender, EventArgs e)
		{
			if (!queueDataView.EditMode)
			{
				// Set the selection to the selected print item.
				QueueRowItem selectedItem = queueDataView.SelectedItem as QueueRowItem;
				if (selectedItem != null)
				{
					if (this.queueDataView.SelectedItems.Count > 0
						|| !this.queueDataView.SelectedItems.Contains(selectedItem))
					{
						this.queueDataView.ClearSelectedItems();
						this.queueDataView.SelectedItems.Add(selectedItem);
					}
				}
			}
		}

		private void AddItemsToQueue()
		{
			FileDialog.OpenFileDialog(
				new OpenFileDialogParams(ApplicationSettings.OpenPrintableFileParams)
				{
					MultiSelect = true,
					ActionButtonLabel = "Add to Queue",
					Title = "MatterControl: Select A File"
				},
				(openParams) =>
				{
					if (openParams.FileNames != null)
					{
						int preAddCount = QueueData.Instance.Count;

						foreach (string fileNameToLoad in openParams.FileNames)
						{
							if (Path.GetExtension(fileNameToLoad).ToUpper() == ".ZIP")
							{
								ProjectFileHandler project = new ProjectFileHandler(null);
								List<PrintItem> partFiles = project.ImportFromProjectArchive(fileNameToLoad);
								if (partFiles != null)
								{
									foreach (PrintItem part in partFiles)
									{
										QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(part.Name, part.FileLocation)));
									}
								}
							}
							else
							{
								QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(fileNameToLoad), Path.GetFullPath(fileNameToLoad))));
							}
						}

						if (QueueData.Instance.Count != preAddCount)
						{
							QueueData.Instance.SelectedIndex = QueueData.Instance.Count - 1;
						}
					}
				});
		}

		private void AddPartCopyToQueue(object state)
		{
			var partInfo = state as PartToAddToQueue;
			QueueData.Instance.AddItem(new PrintItemWrapper(partInfo.PrintItem), partInfo.InsertAfterIndex, QueueData.ValidateSizeOn32BitSystems.Skip);
		}

		private void addToLibraryButton_Click(object sender, EventArgs mouseEvent)
		{
			foreach (QueueRowItem queueItem in queueDataView.SelectedItems)
			{
				// TODO: put up a library chooser and let the user put it where they want
				LibraryProviderSQLite.Instance.AddFilesToLibrary(new string[] {queueItem.PrintItemWrapper.FileLocation });
			}
		}

		private bool addToLibraryMenu_Selected()
		{
			addToLibraryButton_Click(null, null);
			return true;
		}

		private void addToQueueButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(AddItemsToQueue);
		}

		private bool clearAllMenu_Select()
		{
			clearAllButton_Click(null, null);
			return true;
		}

		private void clearAllButton_Click(object sender, EventArgs mouseEvent)
		{
			QueueData.Instance.RemoveAll();
			leaveEditMode();
		}

		private void copyButton_Click(object sender, EventArgs mouseEvent)
		{
			CreateCopyInQueue();
		}

		private bool copyMenu_Selected()
		{
			copyButton_Click(null, null);
			return true;
		}

		private void deleteAllFromQueueButton_Click(object sender, EventArgs mouseEvent)
		{
			QueueData.Instance.RemoveAll();
		}

		private void enterEditModeButtonClick(object sender, EventArgs mouseEvent)
		{
			enterEditModeButton.Visible = false;
			leaveEditModeButton.Visible = true;
			queueDataView.EditMode = true;

			SetEditButtonsStates();
		}

		private bool exportButton_Click()
		{
			exportButton_Click(null, null);
			return true;
		}

		private void exportButton_Click(object sender, EventArgs mouseEvent)
		{
			//Open export options
			if (queueDataView.SelectedItems.Count == 1)
			{
				QueueRowItem libraryItem = queueDataView.SelectedItems[0];
				OpenExportWindow(libraryItem.PrintItemWrapper);
			}
		}

		private void exportQueueButton_Click(object sender, EventArgs mouseEvent)
		{
			List<PrintItem> partList = QueueData.Instance.CreateReadOnlyPartList();
			ProjectFileHandler project = new ProjectFileHandler(partList);
			project.SaveAs();
		}

		private void exportToSDProcess_UpdateRemainingItems(object sender, EventArgs e)
		{
			ExportToFolderProcess exportToSDProcess = (ExportToFolderProcess)sender;
		}

		private void importQueueButton_Click(object sender, EventArgs mouseEvent)
		{
			ProjectFileHandler project = new ProjectFileHandler(null);
			throw new NotImplementedException();
		}

		private void ItemMenu_SelectionChanged(object sender, EventArgs e)
		{
			string menuSelection = ((DropDownMenu)sender).SelectedValue;
			foreach (Tuple<string, Func<bool>> item in menuItems)
			{
				if (item.Item1 == menuSelection)
				{
					if (item.Item2 != null)
					{
						item.Item2();
					}
				}
			}
		}

		private void leaveEditMode()
		{
			enterEditModeButton.Visible = true;
			leaveEditModeButton.Visible = false;
			queueDataView.EditMode = false;
			
			PrintItemSelectionChanged(null, null);
		}

		private void leaveEditModeButtonClick(object sender, EventArgs mouseEvent)
		{
			leaveEditMode();
		}

		private void onLibraryItemsSelectChanged(object sender, EventArgs e)
		{
			SetEditButtonsStates();
		}

		private void OpenExportWindow(PrintItemWrapper printItem)
		{
			if (exportingWindowIsOpen == false)
			{
				exportingWindow = new ExportPrintItemWindow(printItem);
				this.exportingWindowIsOpen = true;
				exportingWindow.Closed += (source, e) => this.exportingWindowIsOpen = false;
				exportingWindow.ShowAsSystemWindow();
			}
			else
			{
				if (exportingWindow != null)
				{
					exportingWindow.BringToFront();
				}
			}
		}

		private void OpenPluginChooserWindow()
		{
			if (pluginChooserWindow == null)
			{
				pluginChooserWindow = new PluginChooserWindow();
				pluginChooserWindow.Closed += (sender, e) =>
				{
					pluginChooserWindow = null;
				};
			}
			else
			{
				pluginChooserWindow.BringToFront();
			}
		}

		private void removeButton_Click(object sender, EventArgs mouseEvent)
		{
			// Sort by index in the QueueData list to prevent positions shifting due to removes
			var sortedByIndexPos = this.queueDataView.SelectedItems.OrderByDescending(rowItem => QueueData.Instance.GetIndex(rowItem.PrintItemWrapper));

			// Once sorted, remove each selected item
			foreach (var item in sortedByIndexPos)
			{
				item.DeletePartFromQueue();
			}

			this.queueDataView.ClearSelectedItems();
		}

		private bool removeMenu_Selected()
		{
			removeButton_Click(null, null);
			return true;
		}

		private void sendButton_Click(object sender, EventArgs mouseEvent)
		{
			//Open export options
			List<PrintItemWrapper> itemList = this.queueDataView.SelectedItems.Select(item => item.PrintItemWrapper).ToList();
			if (sendButtonFunction != null)
			{
				UiThread.RunOnIdle(() => sendButtonFunction(null, itemList));
			}
			else
			{
				UiThread.RunOnIdle(() => StyledMessageBox.ShowMessageBox(null, "Oops! Send is currently disabled.", "Send Print"));
			}
		}

		private bool sendMenu_Selected()
		{
			sendButton_Click(null, null);
			return true;
		}

		private void SetDisplayAttributes()
		{
			this.Padding = new BorderDouble(3);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.AnchorAll();
		}

		private void SetMenuItems(DropDownMenu dropDownMenu)
		{
			menuItems = new TupleList<string, Func<bool>>();

			if (ActiveTheme.Instance.IsTouchScreen)
			{
				menuItems.Add(new Tuple<string, Func<bool>>("Remove All".Localize(), clearAllMenu_Select));
			}

			menuItems.Add(new Tuple<string, Func<bool>>("Send".Localize(), sendMenu_Selected));
			menuItems.Add(new Tuple<string, Func<bool>>("Add To Library".Localize(), addToLibraryMenu_Selected));

			BorderDouble padding = dropDownMenu.MenuItemsPadding;
			//Add the menu items to the menu itself
			foreach (Tuple<string, Func<bool>> item in menuItems)
			{
				if (item.Item2 == null)
				{
					dropDownMenu.MenuItemsPadding = new BorderDouble(5, 0, padding.Right, 3);
				}
				else
				{
					dropDownMenu.MenuItemsPadding = new BorderDouble(10, 5, padding.Right, 5);
				}

				dropDownMenu.AddItem(item.Item1);
			}

			dropDownMenu.Padding = padding;
		}

		private void SetEditButtonsStates()
		{
			int selectedCount = queueDataView.SelectedItems.Count;

			for(int buttonIndex=0; buttonIndex<itemOperationButtons.Children.Count; buttonIndex++)
			{
				bool enabled = selectedCount > 0;
				var child = itemOperationButtons.Children[buttonIndex];
				var button = child as Button;
				if (button != null)
				{
					if ((selectedCount > 1 && !editButtonsEnableData[buttonIndex].multipleItems))
					{
						enabled = false;
					}
					else
					{
						bool enabledState = enabled;

						if (!editButtonsEnableData[buttonIndex].protectedItems)
						{
							// so we can show for multi items lets check for protected items
							for (int itemIndex = 0; itemIndex < queueDataView.SelectedItems.Count; itemIndex++)
							{
								if (queueDataView.SelectedItems[itemIndex].PrintItemWrapper.PrintItem.Protected)
								{
									enabled = false;
								}
							}
						}
					}
					button.Enabled = enabled;
				}
			}
		}

		private class PartToAddToQueue
		{
			internal PrintItem PrintItem { get; set; }

			internal int InsertAfterIndex { get; set; }
		}
	}
}