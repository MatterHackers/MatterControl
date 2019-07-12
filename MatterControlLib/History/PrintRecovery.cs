/*
Copyright (c) 2019, Kevin Pope, Lars Brubaker, John Lewin
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

using System.IO;
using System.Linq;
using MatterControl.Printing;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintHistory
{
	public static class PrintRecovery
	{
		public static bool RecoveryAvailable(PrinterConfig printer)
		{
			PrintTask lastPrint = PrintHistoryData.Instance.GetHistoryForPrinter(printer.Settings.ID.GetHashCode()).FirstOrDefault();
			return RecoveryAvailable(printer, lastPrint);
		}

		public static bool RecoveryAvailable(PrinterConfig printer, PrintTask lastPrint)
		{
			return lastPrint != null
				&& !lastPrint.PrintComplete // Top Print History Item is not complete
				&& !string.IsNullOrEmpty(lastPrint.PrintingGCodeFileName) // PrintingGCodeFileName is set
				&& File.Exists(lastPrint.PrintingGCodeFileName) // PrintingGCodeFileName is still on disk
				&& lastPrint.PercentDone > 0 // we are actually part way into the print
				&& printer.Settings.GetValue<bool>(SettingsKey.recover_is_enabled)
				&& !printer.Settings.GetValue<bool>(SettingsKey.has_hardware_leveling);
		}

		public static void CheckIfNeedToRecoverPrint(PrinterConfig printer)
		{
			string printRecoveryWarningMessage = "WARNING: In order to perform print recovery, your printer must move down to reach its home position.\nIf your print is too large, part of your printer may collide with it when moving down.\nMake sure it is safe to perform this operation before proceeding.".Localize();

			PrintTask lastPrint = PrintHistoryData.Instance.GetHistoryForPrinter(printer.Settings.ID.GetHashCode()).FirstOrDefault();
			if (lastPrint != null)
			{
				if (RecoveryAvailable(printer))
				{
					bool safeHomingDirection = printer.Settings.GetValue<bool>(SettingsKey.z_homes_to_max);

					StyledMessageBox.ShowMessageBox(
						(messageBoxResponse) =>
						{
							if (messageBoxResponse)
							{
								UiThread.RunOnIdle(async () =>
								{
									if (printer.Connection.CommunicationState == CommunicationStates.Connected)
									{
										printer.Connection.CommunicationState = CommunicationStates.PreparingToPrint;

										// TODO: Reimplement
										//await printer.Connection.StartPrint(lastPrint.PrintingGCodeFileName, lastPrint);

										// This needs to be reworked to support the PrintServer owning the PrintTask/Job 

										System.Diagnostics.Debugger.Break();
										//printer.Connection.StartPrint(lastPrint.PrintingGCodeFileName);

										ApplicationController.Instance.MonitorPrintTask(printer);
									}
								});
							}
							else // the recovery has been canceled
							{
								lastPrint.PrintingGCodeFileName = null;
								lastPrint.Commit();
							}
						},
						"It appears your last print failed to complete.\n\nWould your like to attempt to recover from the last know position?".Localize() 
						+ (safeHomingDirection ? "" : "\n\n" + printRecoveryWarningMessage),
						"Recover Last Print".Localize(),
						StyledMessageBox.MessageType.YES_NO,
						"Recover Print".Localize(),
						"Cancel".Localize());
				}
			}
		}
	}
}