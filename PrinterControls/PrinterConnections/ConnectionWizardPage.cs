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
		public ConnectionWizardPage()
		{
			cancelButton.Click += (s, e) => PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
		}
	}
}