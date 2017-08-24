using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class ConnectionWizardPage : WizardPage
	{
		// TODO: It would seem that only one of these pages that derives from ConnectionWizardPage needs to abort a connect attempt... *************************************
		public ConnectionWizardPage(string unlocalizedTextForCancelButton = "Cancel", string unlocalizedTextForTitle = "Setup Wizard")
			: base (unlocalizedTextForCancelButton: unlocalizedTextForCancelButton, unlocalizedTextForTitle: unlocalizedTextForTitle)
		{
			cancelButton.Click += (s, e) => PrinterConnection.Instance.HaltConnectionThread();
		}
	}
}