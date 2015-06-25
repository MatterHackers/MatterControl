using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintQueue;
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
                {LocalizedString.Get("Add Printer"), addPrinter_Click},
                {LocalizedString.Get("Add File"), importFile_Click},
				{LocalizedString.Get("Exit"), exit_Click},
            };
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
								QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(loadedFileName), Path.GetFullPath(loadedFileName))));
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