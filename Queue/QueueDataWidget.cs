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
using MatterHackers.Agg.PlatformAbstract;
using Newtonsoft.Json;
using MatterHackers.PolygonMesh;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrintQueue
{
	internal class ButtonEnableData
	{
		internal bool multipleItems;
		internal bool collectionItems;

		internal ButtonEnableData(bool multipleItems, bool protectedItems, bool collectionItems)
		{
			this.multipleItems = multipleItems;
			this.collectionItems = collectionItems;
		}
	}

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
		private List<PrintItemAction> menuItems;
		private HashSet<string> singleSelectionMenuItems = new HashSet<string>();
		private HashSet<string> multiSelectionMenuItems = new HashSet<string>();
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

			FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				enterEditModeButton = editButtonFactory.Generate("Edit".Localize(), centerText: true);
				enterEditModeButton.ToolTipText = "Enter Multi Select mode".Localize();
				enterEditModeButton.Click += enterEditModeButtonClick;

				leaveEditModeButton = editButtonFactory.Generate("Done".Localize(), centerText: true);
				leaveEditModeButton.Name = "Queue Done Button";
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
					enterEditModeButton.Name = "Queue Edit Button";
					enterEditModeButton.Click += enterEditModeButtonClick;
				}

				multiSelectionMenuItems.Add("Merge".Localize() + "...");

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

				// put in the item edit menu
				{
					moreMenu = new DropDownMenu("More".Localize() + "... ");
					moreMenu.NormalColor = new RGBA_Bytes();
					moreMenu.BorderWidth = (int)(1 * GuiWidget.DeviceScale + .5);
					moreMenu.BorderColor = new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor,100);
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
					addToQueueButton = textImageButtonFactory.Generate("Add".Localize(), StaticData.Instance.LoadIcon("icon_plus.png", 32, 32));
					addToQueueButton.ToolTipText = "Add an .stl, .amf, .gcode or .zip file to the Queue".Localize();
					buttonPanel1.AddChild(addToQueueButton);
					addToQueueButton.Margin = new BorderDouble(0, 0, 3, 0);
					addToQueueButton.Click += addToQueueButton_Click;
					addToQueueButton.Name = "Queue Add Button";

					// put in the creator button
					{
						createButton = textImageButtonFactory.Generate("Create".Localize(), StaticData.Instance.LoadIcon("icon_creator.png", 32, 32));
						createButton.ToolTipText = "Choose a Create Tool to generate custom designs".Localize();
						createButton.Name = "Design Tool Button";
						buttonPanel1.AddChild(createButton);
						createButton.Margin = new BorderDouble(0, 0, 3, 0);
						createButton.Click += (sender, e) =>
						{
							// Clear the queue selection
							QueueData.Instance.SelectedIndex = -1;

							// Clear the scene and switch to editing view
							view3DWidget.ClearBedAndLoadPrintItemWrapper(null, true);
						};
					}

					bool touchScreenMode = UserSettings.Instance.IsTouchScreen;

					if (OemSettings.Instance.ShowShopButton)
					{
						shopButton = textImageButtonFactory.Generate("Buy Materials".Localize(), StaticData.Instance.LoadIcon("icon_shopping_cart_32x32.png", 32,32));
						shopButton.ToolTipText = "Shop online for printing materials".Localize();
						shopButton.Name = "Buy Materials Button";
						buttonPanel1.AddChild(shopButton);
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
						}


					buttonPanel1.AddChild(new HorizontalSpacer());

					queueMenuContainer = new FlowLayoutWidget();
					queueMenuContainer.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
					queueMenu = new QueueOptionsMenu();
					queueMenuContainer.AddChild(queueMenu.MenuDropList);
					buttonPanel1.AddChild(queueMenuContainer);
				}
				allControls.AddChild(buttonPanel1);
			}
			allControls.AnchorAll();

			this.AddChild(allControls);

			QueueData.Instance.SelectedIndexChanged.RegisterEvent((s,e) => SetEditButtonsStates(), ref unregisterEvents);

			SetEditButtonsStates(); 
		}

		private void CreateEditBarButtons()
		{
			itemOperationButtons = new FlowLayoutWidget();
			double oldWidth = editButtonFactory.FixedWidth;
			editButtonFactory.FixedWidth = 0;

			Button exportItemButton = editButtonFactory.Generate("Export".Localize());
			exportItemButton.Name = "Queue Export Button";
			exportItemButton.Margin = new BorderDouble(3, 0);
			exportItemButton.Click += exportButton_Click;
			editButtonsEnableData.Add(new ButtonEnableData(false, false, false));
			itemOperationButtons.AddChild(exportItemButton);

			Button copyItemButton = editButtonFactory.Generate("Copy".Localize());
			copyItemButton.Name = "Queue Copy Button";
			copyItemButton.Margin = new BorderDouble(3, 0);
			copyItemButton.Click += copyButton_Click;
			editButtonsEnableData.Add(new ButtonEnableData(false, true, false));
			itemOperationButtons.AddChild(copyItemButton);

			Button removeItemButton = editButtonFactory.Generate("Remove".Localize());
			removeItemButton.Name = "Queue Remove Button";
			removeItemButton.Margin = new BorderDouble(3, 0);
			removeItemButton.Click += removeButton_Click;
			editButtonsEnableData.Add(new ButtonEnableData(true, true, true));
			itemOperationButtons.AddChild(removeItemButton);

			editButtonFactory.FixedWidth = oldWidth;
		}

		public delegate void SendButtonAction(object state, List<PrintItemWrapper> sendItems);

		private EventHandler unregisterEvents;

		public void CreateCopyInQueue()
		{
			// Guard for single item selection
			if (QueueData.Instance.SelectedCount != 1) return;

			var queueRowItem = this.queueDataView.GetQueueRowItem(QueueData.Instance.SelectedIndex);
			if(queueRowItem == null)
			{
				return;
			}

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

		public override void OnClosed(ClosedEventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		public static void DoAddFiles(List<string> files)
        {
            int preAddCount = QueueData.Instance.ItemCount;

            foreach (string fileToAdd in files)
            {
                string extension = Path.GetExtension(fileToAdd).ToUpper();
                if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
                    || extension == ".GCODE")
                {
                    QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(fileToAdd), Path.GetFullPath(fileToAdd))));
                }
                else if (extension == ".ZIP")
                {
                    List<PrintItem> partFiles = ProjectFileHandler.ImportFromProjectArchive(fileToAdd);
                    if (partFiles != null)
                    {
                        foreach (PrintItem part in partFiles)
                        {
                            QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(part.Name, part.FileLocation)));
                        }
                    }
                }
            }

            if (QueueData.Instance.ItemCount != preAddCount)
            {
                QueueData.Instance.SelectedIndex = QueueData.Instance.ItemCount - 1;
            }
        }

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.DragFiles?.Count > 0)
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

			base.OnMouseEnterBounds(mouseEvent);
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
						int preAddCount = QueueData.Instance.ItemCount;

						foreach (string fileNameToLoad in openParams.FileNames)
						{
							string extension = Path.GetExtension(fileNameToLoad).ToUpper();
							if (extension == ".ZIP")
							{
								List<PrintItem> partFiles = ProjectFileHandler.ImportFromProjectArchive(fileNameToLoad);
								if (partFiles != null)
								{
									foreach (PrintItem part in partFiles)
									{
										QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(part.Name, part.FileLocation)));
									}
								}
							}
							else if (extension != "" && ApplicationSettings.OpenDesignFileParams.Contains(extension.ToLower()))
							{
								// Only add files if they have an extension and if it's in the OpenDesignFileParams list
								QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(fileNameToLoad), Path.GetFullPath(fileNameToLoad))));
							}
						}

						if (QueueData.Instance.ItemCount != preAddCount)
						{
							QueueData.Instance.SelectedIndex = QueueData.Instance.ItemCount - 1;
						}
					}
				});
		}

		private void AddPartCopyToQueue(object state)
		{
			var partInfo = state as PartToAddToQueue;
			QueueData.Instance.AddItem(new PrintItemWrapper(partInfo.PrintItem), partInfo.InsertAfterIndex, QueueData.ValidateSizeOn32BitSystems.Skip);
		}

		void DoAddToSpecificLibrary(SaveAsWindow.SaveAsReturnInfo returnInfo, Action action)
		{
			if (returnInfo != null)
			{
				LibraryProvider libraryToSaveTo = returnInfo.destinationLibraryProvider;
				if (libraryToSaveTo != null)
				{
					foreach (var queueItemIndex in QueueData.Instance.SelectedIndexes)
					{
						var queueItem = queueDataView.GetQueueRowItem(queueItemIndex);
						if (queueItem != null)
						{
						if (File.Exists(queueItem.PrintItemWrapper.FileLocation))
						{
							PrintItemWrapper printItemWrapper = new PrintItemWrapper(new PrintItem(queueItem.PrintItemWrapper.PrintItem.Name, queueItem.PrintItemWrapper.FileLocation), returnInfo.destinationLibraryProvider.GetProviderLocator());
							libraryToSaveTo.AddItem(printItemWrapper);
						}
					}
					}
					libraryToSaveTo.Dispose();
				}
			}
		}

		private View3DWidget view3DWidget;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			view3DWidget = MatterControlApplication.Instance.ActiveView3DWidget;
			if (view3DWidget == null)
			{
				base.OnMouseDown(mouseEvent);
				return;
			}

			var screenSpaceMousePosition = this.TransformToScreenSpace(mouseEvent.Position);
			var topToBottomItemListBounds = queueDataView.topToBottomItemList.TransformToScreenSpace(queueDataView.topToBottomItemList.LocalBounds);

			bool mouseInQueueItemList = topToBottomItemListBounds.Contains(screenSpaceMousePosition);

			// Clear or assign a drag source
			view3DWidget.DragDropSource = (!mouseInQueueItemList) ? null : new Object3D
			{
				ItemType = Object3DTypes.Model,
				Mesh = PlatonicSolids.CreateCube(10, 10, 10)
			};

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y)
				&& mouseEvent.DragFiles?.Count > 0)
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

			if (!this.HasBeenClosed &&
				view3DWidget?.DragDropSource != null &&
				queueDataView.DragSourceRowItem != null)
			{
				var screenSpaceMousePosition = this.TransformToScreenSpace(mouseEvent.Position);

				if(!File.Exists(queueDataView.DragSourceRowItem.PrintItemWrapper.FileLocation))
				{
					view3DWidget.DragDropSource = null;
					queueDataView.DragSourceRowItem = null;
					return;
				}

				if(view3DWidget.AltDragOver(screenSpaceMousePosition))
				{
					view3DWidget.DragDropSource.MeshPath = queueDataView.DragSourceRowItem.PrintItemWrapper.FileLocation;

					base.OnMouseMove(mouseEvent);

					view3DWidget.LoadDragSource();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.DragFiles?.Count > 0)
			{
				DoAddFiles(mouseEvent.DragFiles);
			}

			if (view3DWidget?.DragDropSource != null && view3DWidget.Scene.Children.Contains(view3DWidget.DragDropSource))
			{
				// Mouse and widget positions
				var screenSpaceMousePosition = this.TransformToScreenSpace(mouseEvent.Position);
				var meshViewerPosition = this.view3DWidget.meshViewerWidget.TransformToScreenSpace(view3DWidget.meshViewerWidget.LocalBounds);

				// If the mouse is not within the meshViewer, remove the inserted drag item
				if (!meshViewerPosition.Contains(screenSpaceMousePosition))
				{
					view3DWidget.Scene.ModifyChildren(children => children.Remove(view3DWidget.DragDropSource));
					view3DWidget.Scene.ClearSelection();
				}
				else
				{
					// Create and push the undo operation
					view3DWidget.AddUndoOperation(
						new InsertCommand(view3DWidget, view3DWidget.DragDropSource));
				}
			}

			if (view3DWidget != null)
			{
				view3DWidget.DragDropSource = null;
			}

			base.OnMouseUp(mouseEvent);
		}

		private void addToLibraryButton_Click(object sender, EventArgs mouseEvent)
		{
			SaveAsWindow saveAsWindow = new SaveAsWindow(DoAddToSpecificLibrary, null, false, false);
		}

		private void addToQueueButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(AddItemsToQueue);
		}

		private void clearAllButton_Click(object sender, EventArgs mouseEvent)
		{
			QueueData.Instance.RemoveAll();
			LeaveEditMode();
		}

		private void copyButton_Click(object sender, EventArgs mouseEvent)
		{
			CreateCopyInQueue();
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

		private void exportButton_Click(object sender, EventArgs mouseEvent)
		{
			//Open export options
			if (QueueData.Instance.SelectedCount == 1)
			{
				QueueRowItem libraryItem = queueDataView.GetQueueRowItem(QueueData.Instance.SelectedIndex);
				if (libraryItem != null)
				{
				OpenExportWindow(libraryItem.PrintItemWrapper);
			}
		}
		}

		private void exportQueueButton_Click(object sender, EventArgs mouseEvent)
		{
			List<PrintItem> partList = QueueData.Instance.CreateReadOnlyPartList(false);
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
			foreach (var menuItem in menuItems)
			{
				if (menuItem.Title == menuSelection)
				{
					menuItem.Action?.Invoke(queueDataView.GetSelectedItems(), this);
				}
			}
		}

		public void LeaveEditMode()
		{
			if (queueDataView.EditMode)
			{
				enterEditModeButton.Visible = true;
				leaveEditModeButton.Visible = false;
				queueDataView.EditMode = false;
			}
		}

		private void leaveEditModeButtonClick(object sender, EventArgs mouseEvent)
		{
			LeaveEditMode();
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
				pluginChooserWindow.Name = "Plugin Chooser Window";
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
			QueueData.Instance.RemoveSelected();
			}

		private void sendButton_Click(object sender, EventArgs mouseEvent)
		{
			if (sendButtonFunction != null)
			{
				List<PrintItemWrapper> itemList = this.queueDataView.GetSelectedItems().Select(item => item.PrintItemWrapper).ToList();
				UiThread.RunOnIdle(() => sendButtonFunction(null, itemList));
			}
			else
			{
				UiThread.RunOnIdle(() => StyledMessageBox.ShowMessageBox(null, "Oops! Send is currently disabled.", "Send Print"));
			}
		}

		private void SetDisplayAttributes()
		{
			this.Padding = new BorderDouble(3);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.AnchorAll();
		}

		private void SetMenuItems(DropDownMenu dropDownMenu)
		{
			menuItems = new List<PrintItemAction>();

			menuItems.Add(new PrintItemAction()
			{
				Title = "Send".Localize(),
				Action = (items, queueDataView) => sendButton_Click(null, null)
			});

			menuItems.Add(new PrintItemAction()
			{
				Title = "Add to Library".Localize(),
				Action = (items, queueDataView) => addToLibraryButton_Click(null, null)
			});

			// Extension point for plugins to hook into selected item actions
			var pluginFinder = new PluginFinder<PrintItemMenuExtension>();
			foreach (var menuExtensionPlugin in pluginFinder.Plugins)
			{
				foreach(var menuItem in menuExtensionPlugin.GetMenuItems())
				{
					menuItems.Add(menuItem);
				}
			}

			BorderDouble padding = dropDownMenu.MenuItemsPadding;

			//Add the menu items to the menu itself
			foreach (PrintItemAction item in menuItems)
			{
				if (item.Action == null)
				{
					dropDownMenu.MenuItemsPadding = new BorderDouble(5, 0, padding.Right, 3);
				}
				else
				{
					if(item.SingleItemOnly)
					{
						singleSelectionMenuItems.Add(item.Title);
					}
					dropDownMenu.MenuItemsPadding = new BorderDouble(10, 5, padding.Right, 5);
				}

				dropDownMenu.AddItem(item.Title);
			}

			dropDownMenu.Padding = padding;
		}

		private void SetEditButtonsStates()
		{
			int selectedCount = QueueData.Instance.SelectedCount;

			// Disable menu items which are singleSelection only
			foreach(MenuItem menuItem in moreMenu.MenuItems)
			{
				// TODO: Ideally this would set .Enabled but at the moment, disabled controls don't have enough 
				// functionality to convey the disabled aspect or suppress click events
				if (selectedCount == 1)
				{
					menuItem.Enabled = !multiSelectionMenuItems.Contains(menuItem.Text);
			}
				else
				{
					menuItem.Enabled = !singleSelectionMenuItems.Contains(menuItem.Text);
				}
			}

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