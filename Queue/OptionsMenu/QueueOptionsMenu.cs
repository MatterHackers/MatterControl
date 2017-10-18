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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SetupWizard;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class QueueOptionsMenu : GuiWidget
	{
		public DropDownMenu MenuDropList;
		private TupleList<string, Func<bool>> menuItems;

		private ExportToFolderFeedbackWindow exportingWindow = null;

		public QueueOptionsMenu()
		{
			MenuDropList = new DropDownMenu("Queue".Localize() + "... ", Direction.Up);
			MenuDropList.Name = "Queue... Menu";
			MenuDropList.VAnchor = VAnchor.ParentBottomTop;
			MenuDropList.BorderWidth = 1;
			MenuDropList.MenuAsWideAsItems = false;
			MenuDropList.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
			MenuDropList.Margin = new BorderDouble(4, 0, 1, 0);
			MenuDropList.AlignToRightEdge = true;

			SetMenuItems();
			this.MenuDropList.SelectionChanged += new EventHandler(MenuDropList_SelectionChanged);
		}

		private void MenuDropList_SelectionChanged(object sender, EventArgs e)
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

		private void SetMenuItems()
		{
			menuItems = new TupleList<string, Func<bool>>();

#if !__ANDROID__
			menuItems.Add(new Tuple<string, Func<bool>>("Design".Localize(), null));
			menuItems.Add(new Tuple<string, Func<bool>>(" Export to Zip".Localize(), exportQueueToZipMenu_Click));
			menuItems.Add(new Tuple<string, Func<bool>>("G-Code".Localize(), null));
			menuItems.Add(new Tuple<string, Func<bool>>(" Export to Folder or SD Card".Localize(), exportGCodeToFolderButton_Click));
			//menuItems.Add(new Tuple<string, Func<bool>>("X3G", null));
			//menuItems.Add(new Tuple<string, Func<bool>>("Export to Folder".Localize(), exportX3GButton_Click));
#endif

			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_sd_card_reader))
			{
				menuItems.Add(new Tuple<string, Func<bool>>("SD Card".Localize(), null));
				menuItems.Add(new Tuple<string, Func<bool>>(" Load Files".Localize(), loadFilesFromSDButton_Click));
				menuItems.Add(new Tuple<string, Func<bool>>(" Eject SD Card".Localize(), ejectSDCardButton_Click));
			}

			// The pdf export library is not working on the mac at the moment so we don't include the
			// part sheet export option on mac.
			if (OsInformation.OperatingSystem == OSType.Windows)
			{
				menuItems.Add(new Tuple<string, Func<bool>>("Other".Localize(), null));
				menuItems.Add(new Tuple<string, Func<bool>>(" Create Part Sheet".Localize(), createPartsSheetsButton_Click));
				menuItems.Add(new Tuple<string, Func<bool>>(" Remove All".Localize(), removeAllFromQueueButton_Click));
			}
			else
			{
				// mac cannot export to pdf
				menuItems.Add(new Tuple<string, Func<bool>>("Other".Localize(), null));
				menuItems.Add(new Tuple<string, Func<bool>>(" Remove All".Localize(), removeAllFromQueueButton_Click));
			}

			BorderDouble padding = MenuDropList.MenuItemsPadding;
			//Add the menu items to the menu itself
			foreach (Tuple<string, Func<bool>> item in menuItems)
			{
				if (item.Item2 == null)
				{
					MenuDropList.MenuItemsPadding = new BorderDouble(5, 0, padding.Right, 3);
				}
				else
				{
					MenuDropList.MenuItemsPadding = new BorderDouble(10, 5, padding.Right, 5);
				}

				MenuItem menuItem = MenuDropList.AddItem(item.Item1);
				if(item.Item2 == null)
				{
					menuItem.Enabled = false;
				}
			}

			MenuDropList.Padding = padding;
		}

		private bool createPartsSheetsButton_Click()
		{
			UiThread.RunOnIdle(PartSheetClickOnIdle);
			return true;
		}

		private void PartSheetClickOnIdle()
		{
#if !__ANDROID__
			List<PrintItem> parts = QueueData.Instance.CreateReadOnlyPartList(true);
			if (parts.Count > 0)
			{
				FileDialog.SaveFileDialog(
					new SaveFileDialogParams("Save Parts Sheet|*.pdf")
					{
						ActionButtonLabel = "Save Parts Sheet".Localize(),
						Title = string.Format("{0}: {1}", "MatterControl".Localize(), "Save".Localize())
					},
					(saveParams) =>
					{
						if (!string.IsNullOrEmpty(saveParams.FileName))
						{
							PartsSheet currentPartsInQueue = new PartsSheet(parts, saveParams.FileName);

							currentPartsInQueue.SaveSheets();

							SavePartsSheetFeedbackWindow feedbackWindow = new SavePartsSheetFeedbackWindow(parts.Count, parts[0].Name, ActiveTheme.Instance.PrimaryBackgroundColor);
							currentPartsInQueue.UpdateRemainingItems += feedbackWindow.StartingNextPart;
							currentPartsInQueue.DoneSaving += feedbackWindow.DoneSaving;

							feedbackWindow.ShowAsSystemWindow();
						}
					});
			}
#endif
		}

		private string pleaseSelectPrinterMessage = "Before you can export printable files, you must select a printer.";
		private string pleaseSelectPrinterTitle = "Please select a printer";

		private void MustSelectPrinterMessage()
		{
			StyledMessageBox.ShowMessageBox(null, pleaseSelectPrinterMessage, pleaseSelectPrinterTitle);
		}

		private bool exportGCodeToFolderButton_Click()
		{
			if (!ActiveSliceSettings.Instance.PrinterSelected)
			{
				UiThread.RunOnIdle(MustSelectPrinterMessage);
			}
			else if (!ActiveSliceSettings.Instance.IsValid())
			{
				return false;
			}
			else
			{
				UiThread.RunOnIdle(SelectLocationToExportGCode);
			}

			return true;
		}

		private bool exportX3GButton_Click()
		{
			if (!ActiveSliceSettings.Instance.PrinterSelected)
			{
				UiThread.RunOnIdle(MustSelectPrinterMessage);
			}
			else
			{
				UiThread.RunOnIdle(SelectLocationToExportGCode);
			}
			return true;
		}

		private void ExportToFolderFeedbackWindow_Closed(object sender, ClosedEventArgs e)
		{
			this.exportingWindow = null;
		}

		private void SelectLocationToExportGCode()
		{
			SelectFolderDialogParams selectParams = new SelectFolderDialogParams("Select Location To Save Files");
			selectParams.ActionButtonLabel = "Export".Localize();
			selectParams.Title = "MatterControl: Select A Folder";

			FileDialog.SelectFolderDialog(selectParams, onSelectFolderDialog);
		}

		private void onSelectFolderDialog(SelectFolderDialogParams openParams)
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
						exportingWindow.Closed += ExportToFolderFeedbackWindow_Closed;
						exportingWindow.ShowAsSystemWindow();
					}
					else
					{
						exportingWindow.BringToFront();
					}

					ExportToFolderProcess exportToFolderProcess = new ExportToFolderProcess(parts, path);
					exportToFolderProcess.StartingNextPart += exportingWindow.StartingNextPart;
					exportToFolderProcess.UpdatePartStatus += exportingWindow.UpdatePartStatus;
					exportToFolderProcess.DoneSaving += exportingWindow.DoneSaving;
					exportToFolderProcess.Start();
				}
			}
		}

		private bool exportQueueToZipMenu_Click()
		{
			if (!ActiveSliceSettings.Instance.PrinterSelected)
			{
				UiThread.RunOnIdle(MustSelectPrinterMessage);
			}
			else if (!ActiveSliceSettings.Instance.IsValid())
			{
				return false;
			}
			else
			{
				UiThread.RunOnIdle(ExportQueueToZipOnIdle);
			}

			return true;
		}

		private void ExportQueueToZipOnIdle()
		{
			List<PrintItem> partList = QueueData.Instance.CreateReadOnlyPartList(false);
			ProjectFileHandler project = new ProjectFileHandler(partList);
			project.SaveAs();
		}

		private bool loadFilesFromSDButton_Click()
		{
			QueueData.Instance.LoadFilesFromSD();
			return true;
		}

		private bool ejectSDCardButton_Click()
		{
			// Remove all the QueueData.SdCardFileName parts from the queue
			QueueData.Instance.RemoveAllSdCardFiles();
			PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("M22"); // (Release SD card)
			return true;
		}

		private bool removeAllFromQueueButton_Click()
		{
			UiThread.RunOnIdle(removeAllPrintsFromQueue);
			return true;
		}

		private void removeAllPrintsFromQueue()
		{
			QueueData.Instance.RemoveAll();
		}
	}
}