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
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl.PrintHistory
{
	public static class PrintRecovery
	{
		static PrintTask lastPrintTask;

		public static void CheckIfNeedToRecoverPrint()
		{
			string recoverPrint = "Recover Print".Localize();
			string cancelRecovery = "Cancel".Localize();
			string printRecoveryWarningMessage = "WARNING: In order to perform print recovery, your printer must move down to reach its home position.\nIf your print is too large, part of your printer may collide with it when moving down.\nMake sure it is safe to perform this operation before proceeding.".Localize();
			string printRecoveryMessage = "It appears your last print failed to complete.\n\nWould your like to attempt to recover from the last know position?".Localize();
			string recoverPrintTitle = "Recover Last Print".Localize();

			foreach (PrintTask lastPrint in PrintHistoryData.Instance.GetHistoryItems(1))
			{
				if (!lastPrint.PrintComplete // Top Print History Item is not complete
					&& !string.IsNullOrEmpty(lastPrint.PrintingGCodeFileName) // PrintingGCodeFileName is set
					&& File.Exists(lastPrint.PrintingGCodeFileName) // PrintingGCodeFileName is still on disk
					&& lastPrint.PercentDone > 0 // we are actually part way into the print
					&& ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.recover_is_enabled)
					&& !ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_hardware_leveling))
                {
					lastPrintTask = lastPrint;
					if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.z_homes_to_max))
					{
						StyledMessageBox.ShowMessageBox(RecoverPrintProcessDialogResponse, printRecoveryMessage, recoverPrintTitle, StyledMessageBox.MessageType.YES_NO, recoverPrint, cancelRecovery);
					}
					else // make sure we include a waring
					{
						StyledMessageBox.ShowMessageBox(RecoverPrintProcessDialogResponse, printRecoveryMessage + "\n\n" + printRecoveryWarningMessage, recoverPrintTitle, StyledMessageBox.MessageType.YES_NO, recoverPrint, cancelRecovery);
					}
				}
			}
		}

		private static void RecoverPrintProcessDialogResponse(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				UiThread.RunOnIdle(() =>
				{
					if (PrinterConnection.Instance.CommunicationState == CommunicationStates.Connected)
					{
						PrinterConnection.Instance.CommunicationState = CommunicationStates.PreparingToPrint;
						PrinterConnection.Instance.StartPrint(lastPrintTask.PrintingGCodeFileName, lastPrintTask);
					}
				});
			}
			else // the recovery has been canceled
			{
				lastPrintTask.PrintingGCodeFileName = null;
				lastPrintTask.Commit();
			}
		}

	}

	public class PrintHistoryData
	{
		public static readonly int RecordLimit = 20;
		public RootedObjectEventHandler HistoryCleared = new RootedObjectEventHandler();
		public bool ShowTimestamp;
		private static PrintHistoryData instance;

		private static event EventHandler unregisterEvents;

		public static PrintHistoryData Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new PrintHistoryData();
					PrinterConnection.Instance.ConnectionSucceeded.RegisterEvent((s, e) =>
					{
						UiThread.RunOnIdle(PrintRecovery.CheckIfNeedToRecoverPrint);
					}, ref unregisterEvents);
				}
				return instance;
			}
		}

		public IEnumerable<DataStorage.PrintTask> GetHistoryItems(int recordCount)
		{
			string query;
			if (UserSettings.Instance.get("PrintHistoryFilterShowCompleted") == "true")
			{
				query = string.Format("SELECT * FROM PrintTask WHERE PrintComplete = 1 ORDER BY PrintStart DESC LIMIT {0};", recordCount);
			}
			else
			{
				query = string.Format("SELECT * FROM PrintTask ORDER BY PrintStart DESC LIMIT {0};", recordCount);
			}

			return Datastore.Instance.dbSQLite.Query<PrintTask>(query);
		}

		internal void ClearHistory()
		{
			Datastore.Instance.dbSQLite.ExecuteScalar<PrintTask>("DELETE FROM PrintTask;");
			HistoryCleared.CallEvents(this, null);
		}
	}
}