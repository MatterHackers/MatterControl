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
		public MenuOptionFile()
			: base("File".Localize())
		{
		}

		override protected TupleList<string, Func<bool>> GetMenuItems()
		{
			return new TupleList<string, Func<bool>>
            {
                {"Add Printer".Localize(), addPrinter_Click},
                {"Add File".Localize(), importFile_Click},
				{"------------------------", nothing_Click},
				{"Exit".Localize(), exit_Click},
            };
		}

		private bool nothing_Click()
		{
			return true;
		}

		private bool addPrinter_Click()
		{
			UiThread.RunOnIdle(ConnectionWindow.Show);
			return true;
		}

		private bool importFile_Click()
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
			return true;
		}

		private string cannotExitWhileActiveMessage = "Oops! You cannot exit while a print is active.".Localize();
		private string cannotExitWhileActiveTitle = "Unable to Exit";

		private bool exit_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				GuiWidget parent = this;
				while (parent as MatterControlApplication == null)
				{
					parent = parent.Parent;
				}

				if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
				{
					StyledMessageBox.ShowMessageBox(null, cannotExitWhileActiveMessage, cannotExitWhileActiveTitle);
				}
				else
				{
					MatterControlApplication app = parent as MatterControlApplication;
					app.RestartOnClose = false;
					app.Close();
				}
			});
			return true;
		}
	}
}