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
using System.Linq;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class MenuOptionFile : MenuBase
	{
		public static MenuOptionFile CurrentMenuOptionFile = null;

		public MenuOptionFile()
			: base("File".Localize())
		{
			Name = "File Menu";
			CurrentMenuOptionFile = this;
		}

		protected override IEnumerable<MenuItemAction> GetMenuActions()
		{
			return new List<MenuItemAction>
			{
#if DEBUG
				new MenuItemAction("Printing Window...".Localize(), () => PrintingWindow.Show()),
				new MenuItemAction("------------------------", null),
#endif
				new MenuItemAction("Add Printer".Localize(), AddPrinter_Click),
				new MenuItemAction("Import Printer".Localize(), ImportPrinter),
				new MenuItemAction("Add File To Queue".Localize(), importFile_Click),
				new MenuItemAction("Redeem Design Code".Localize(), () => ApplicationController.Instance.RedeemDesignCode?.Invoke()),
				new MenuItemAction("Enter Share Code".Localize(), () => ApplicationController.Instance.EnterShareCode?.Invoke()),
				new MenuItemAction("------------------------", null),
				new MenuItemAction("Exit".Localize(), () =>
				{
					MatterControlApplication app = this.Parents<MatterControlApplication>().FirstOrDefault();
					app.RestartOnClose = false;
					app.Close();
				})
			};
		}

		private void ImportPrinter()
		{
			FileDialog.OpenFileDialog(
					new OpenFileDialogParams("settings files|*.ini;*.printer;*.slice"),
					(dialogParams) =>
					{
						if (!string.IsNullOrEmpty(dialogParams.FileName))
						{
							ImportSettingsFile(dialogParams.FileName);
						}
					});
		}

		private void ImportSettingsFile(string settingsFilePath)
		{
			if(!ProfileManager.ImportFromExisting(settingsFilePath))
			{
				StyledMessageBox.ShowMessageBox(null, "Oops! Settings file '{0}' did not contain any settings we could import.".Localize().FormatWith(Path.GetFileName(settingsFilePath)), "Unable to Import".Localize());
			}
		}

		private void AddPrinter_Click()
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting
				|| PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
			{
				UiThread.RunOnIdle(() =>
				StyledMessageBox.ShowMessageBox(null, "Please wait until the print has finished and try again.".Localize(), "Can't add printers while printing".Localize())
				);
			}
			else
			{
				WizardWindow.ShowPrinterSetup(true);
			}
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
                                    List<PrintItem> partFiles = ProjectFileHandler.ImportFromProjectArchive(loadedFileName);
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

	}
}
