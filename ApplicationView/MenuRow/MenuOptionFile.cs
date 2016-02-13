using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintQueue;
using System.Collections.Generic;
using MatterHackers.VectorMath;
using System;
using System.IO;

namespace MatterHackers.MatterControl
{
	public class MenuOptionFile : MenuBase
	{
		private static CreateFolderWindow createFolderWindow = null;

		public static MenuOptionFile CurrentMenuOptionFile = null;

		public event EventHandler<StringEventArgs> AddLocalFolderToLibrary;
		public EventHandler RedeemDesignCode;
        public EventHandler EnterShareCode;

		public MenuOptionFile()
			: base("File".Localize())
		{
			Name = "File Menu";
			CurrentMenuOptionFile = this;
		}

		override protected IEnumerable<MenuItemAction> GetMenuItems()
		{
			return new List<MenuItemAction>
			{
				new MenuItemAction("Add Printer".Localize(), addPrinter_Click),
                new MenuItemAction("Add File To Queue".Localize(), importFile_Click),
				//new MenuItemAction("Add Folder To Library".Localize(), addFolderToLibrar_Click),
				new MenuItemAction("Redeem Design Code".Localize(), redeemDesignCode_Click),
				new MenuItemAction("Enter Share Code".Localize(), enterShareCode_Click),
				new MenuItemAction("------------------------", null),
				new MenuItemAction("Exit".Localize(), exit_Click),
            };
		}

		private void redeemDesignCode_Click()
		{
			if (RedeemDesignCode != null)
			{
				RedeemDesignCode(this, null);
			}
		}

        private void enterShareCode_Click()
        {
            if (EnterShareCode != null)
            {
                EnterShareCode(this, null);
            }
        }

		private void addFolderToLibrar_Click()
		{
			if (AddLocalFolderToLibrary != null)
			{
				if (createFolderWindow == null)
				{
					UiThread.RunOnIdle(() =>
					{
						createFolderWindow = new CreateFolderWindow((returnInfo) =>
						{
							AddLocalFolderToLibrary(this, new StringEventArgs(returnInfo.newName));
						});
						createFolderWindow.Closed += (sender2, e2) => { createFolderWindow = null; };
					});
				}
				else
				{
					createFolderWindow.BringToFront();
				}
			}
		}

		private void addPrinter_Click()
		{
			UiThread.RunOnIdle(ConnectionWindow.Show);
		}

		private void importFile_Click()
		{
			UiThread.RunOnIdle(() =>
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
							foreach (string loadedFileName in openParams.FileNames)
							{
                                if (Path.GetExtension(loadedFileName).ToUpper() == ".ZIP")
                                {
                                    ProjectFileHandler project = new ProjectFileHandler(null);
                                    List<PrintItem> partFiles = project.ImportFromProjectArchive(loadedFileName);
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
                                    QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(loadedFileName), Path.GetFullPath(loadedFileName))));
                                }
							}
						}
					});
			});
		}

		private void exit_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				GuiWidget parent = this;
				while (parent as MatterControlApplication == null)
				{
					parent = parent.Parent;
				}

				MatterControlApplication app = parent as MatterControlApplication;
				app.RestartOnClose = false;
				app.Close();
			});
		}
	}
}