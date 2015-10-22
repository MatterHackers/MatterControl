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
using MatterHackers.PolygonMesh.Processors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class IndexArgs : EventArgs
	{
		internal int index;

		public int Index { get { return index; } }

		internal IndexArgs(int index)
		{
			this.index = index;
		}
	}

	public class SwapIndexArgs : EventArgs
	{
		internal int indexA;
		internal int indexB;

		internal SwapIndexArgs(int indexA, int indexB)
		{
			this.indexA = indexA;
			this.indexB = indexB;
		}
	}

	public class QueueData
	{
		public static readonly string SdCardFileName = "SD_CARD";

		private List<PrintItemWrapper> printItems = new List<PrintItemWrapper>();

		public List<PrintItemWrapper> PrintItems
		{
			get { return printItems; }
		}

		private int selectedIndex = -1;

		public int SelectedIndex
		{
			get { return selectedIndex; }
			set
			{
				selectedIndex = value;
				OnSelectedIndexChanged(new IndexArgs(value));
			}
		}

		public RootedObjectEventHandler ItemAdded = new RootedObjectEventHandler();
		public RootedObjectEventHandler ItemRemoved = new RootedObjectEventHandler();
		public RootedObjectEventHandler OrderChanged = new RootedObjectEventHandler();
		public RootedObjectEventHandler SelectedIndexChanged = new RootedObjectEventHandler();

		private static QueueData instance;

		public static QueueData Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new QueueData();
					instance.LoadDefaultQueue();
				}
				return instance;
			}
		}

		public void SwapItemsOnIdle(int indexA, int indexB)
		{
			UiThread.RunOnIdle(SwapItems, new SwapIndexArgs(indexA, indexB));
		}

		private void SwapItems(object state)
		{
			int indexA = ((SwapIndexArgs)state).indexA;
			int indexB = ((SwapIndexArgs)state).indexB;

			if (indexA >= 0 && indexA < Count
				&& indexB >= 0 && indexB < Count
				&& indexA != indexB)
			{
				PrintItemWrapper hold = PrintItems[indexA];
				PrintItems[indexA] = PrintItems[indexB];
				PrintItems[indexB] = hold;

				OnOrderChanged(null);

				SaveDefaultQueue();
			}
		}

		public void OnOrderChanged(EventArgs e)
		{
			OrderChanged.CallEvents(this, e);
		}

		public void RemoveIndexOnIdle(int index)
		{
			UiThread.RunOnIdle(RemoveIndex, new IndexArgs(index));
		}

		private void RemoveIndex(object state)
		{
			IndexArgs removeArgs = state as IndexArgs;
			if (removeArgs != null)
			{
				RemoveAt(removeArgs.index);
			}
		}

		public void RemoveAt(int index)
		{
			if (index >= 0 && index < Count)
			{
				bool ActiveItemMustStayInQueue = PrinterConnectionAndCommunication.Instance.PrinterIsPrinting || PrinterConnectionAndCommunication.Instance.PrinterIsPaused;
				bool PartMustStayInQueue = ActiveItemMustStayInQueue && PrintItems[index] == PrinterConnectionAndCommunication.Instance.ActivePrintItem;
				if (!PartMustStayInQueue)
				{
					PrintItems.RemoveAt(index);

					OnItemRemoved(new IndexArgs(index));

					SaveDefaultQueue();
				}
			}
		}

		public void OnItemRemoved(EventArgs e)
		{
			ItemRemoved.CallEvents(this, e);
		}

		public void OnSelectedIndexChanged(EventArgs e)
		{
			SelectedIndexChanged.CallEvents(this, e);
		}

		public PrintItemWrapper GetPrintItemWrapper(int index)
		{
			if (index >= 0 && index < PrintItems.Count)
			{
				return PrintItems[index];
			}

			return null;
		}

		public int GetIndex(PrintItemWrapper printItem)
		{
			return PrintItems.IndexOf(printItem);
		}

		public string[] GetItemNames()
		{
			List<string> itemNames = new List<string>(); ;
			for (int i = 0; i < PrintItems.Count; i++)
			{
				itemNames.Add(PrintItems[i].Name);
			}

			return itemNames.ToArray();
		}

		public string GetItemName(int itemIndex)
		{
			return PrintItems[itemIndex].Name;
		}

		private bool gotBeginFileList = false;

		private event EventHandler unregisterEvents;

		public void LoadFilesFromSD()
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected
				&& !(PrinterConnectionAndCommunication.Instance.PrinterIsPrinting
				|| PrinterConnectionAndCommunication.Instance.PrinterIsPaused))
			{
				gotBeginFileList = false;
				PrinterConnectionAndCommunication.Instance.ReadLine.RegisterEvent(GetSdCardList, ref unregisterEvents);
				StringBuilder commands = new StringBuilder();
				commands.AppendLine("M21"); // Init SD card
				commands.AppendLine("M20"); // List SD card
				PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow(commands.ToString());
			}
		}

		private void GetSdCardList(object sender, EventArgs e)
		{
			StringEventArgs currentEvent = e as StringEventArgs;
			if (currentEvent != null)
			{
				if (!currentEvent.Data.StartsWith("echo:"))
				{
					switch (currentEvent.Data)
					{
						case "Begin file list":
							gotBeginFileList = true;
							break;

						default:
							if (gotBeginFileList)
							{
								bool sdCardItemInQueue = false;
								bool validSdCardItem = false;

								foreach (PrintItem item in CreateReadOnlyPartList())
								{
									if (item.FileLocation == QueueData.SdCardFileName
										&& item.Name == currentEvent.Data)
									{
										sdCardItemInQueue = true;
										break;
									}
								}

								string sdCardFileExtension = currentEvent.Data.ToUpper();

								if (sdCardFileExtension.Contains(".GCO")
									|| sdCardFileExtension.Contains(".GCODE"))
								{
									validSdCardItem = true;
								}

								if (!sdCardItemInQueue && validSdCardItem)
								{
									// If there is not alread an sd card item in the queue with this name then add it.
									AddItem(new PrintItemWrapper(new PrintItem(currentEvent.Data, QueueData.SdCardFileName)));
								}
							}
							break;

						case "End file list":
							PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(GetSdCardList, ref unregisterEvents);
							break;
					}
				}
			}
		}

		public List<PrintItem> CreateReadOnlyPartList()
		{
			List<PrintItem> listToReturn = new List<PrintItem>();
			for (int i = 0; i < Count; i++)
			{
				listToReturn.Add(GetPrintItemWrapper(i).PrintItem);
			}
			return listToReturn;
		}

		private static bool Is32Bit()
		{
			if (IntPtr.Size == 4)
			{
				return true;
			}

			return false;
		}

		private PrintItemWrapper partUnderConsideration = null;

		public enum ValidateSizeOn32BitSystems { Required, Skip }

		public void AddItem(PrintItemWrapper item, int indexToInsert = -1, ValidateSizeOn32BitSystems checkSize = ValidateSizeOn32BitSystems.Required)
		{
			if (Is32Bit())
			{
				// Check if the part we are adding is BIG. If it is warn the user and
				// possibly don't add it
				bool warnAboutFileSize = false;
				long estimatedMemoryUse = 0;
				if (File.Exists(item.FileLocation)
					&& checkSize == ValidateSizeOn32BitSystems.Required)
				{
					estimatedMemoryUse = MeshFileIo.GetEstimatedMemoryUse(item.FileLocation);

					if (OsInformation.OperatingSystem == OSType.Android)
					{
						if (estimatedMemoryUse > 100000000)
						{
							warnAboutFileSize = true;
						}
					}
					else
					{
						if (estimatedMemoryUse > 500000000)
						{
							warnAboutFileSize = true;
						}
					}
				}

				if (warnAboutFileSize)
				{
					partUnderConsideration = item;
					// Show a dialog and only load the part to the queue if the user clicks yes.
					UiThread.RunOnIdle(() =>
					{
						string memoryWarningMessage = "Are you sure you want to add this part ({0}) to the Queue?\nThe 3D part you are trying to load may be too complicated and cause performance or stability problems.\n\nConsider reducing the geometry before proceeding.".Localize().FormatWith(item.Name);
						StyledMessageBox.ShowMessageBox(UserSaidToAllowAddToQueue, memoryWarningMessage, "File May Cause Problems".Localize(), StyledMessageBox.MessageType.YES_NO, "Add To Queue", "Do Not Add");
						// show a dialog to tell the user there is an update
					});
					return;
				}
				else
				{
					DoAddItem(item, indexToInsert);
				}
			}
			else
			{
				DoAddItem(item, indexToInsert);
			}
		}

		private void UserSaidToAllowAddToQueue(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				DoAddItem(partUnderConsideration, -1);
			}
		}


		private void DoAddItem(PrintItemWrapper item, int indexToInsert)
		{
			if (indexToInsert == -1)
			{
				indexToInsert = PrintItems.Count;
			}
			PrintItems.Insert(indexToInsert, item);
			OnItemAdded(new IndexArgs(indexToInsert));
			SaveDefaultQueue();
		}

		public void LoadDefaultQueue()
		{
			RemoveAll();
			ManifestFileHandler manifest = new ManifestFileHandler(null);
			List<PrintItem> partFiles = manifest.ImportFromJson();
			if (partFiles != null)
			{
				foreach (PrintItem item in partFiles)
				{
					AddItem(new PrintItemWrapper(item), -1, QueueData.ValidateSizeOn32BitSystems.Skip);
				}
			}
			RemoveAllSdCardFiles();
		}

		public void RemoveAllSdCardFiles()
		{
			for (int i = Count - 1; i >= 0; i--)
			{
				PrintItem printItem = PrintItems[i].PrintItem;
				if (printItem.FileLocation == QueueData.SdCardFileName)
				{
					RemoveAt(i);
				}
			}
		}

		public void OnItemAdded(EventArgs e)
		{
			ItemAdded.CallEvents(this, e);
		}

		public void SaveDefaultQueue()
		{
			List<PrintItem> partList = CreateReadOnlyPartList();
			ManifestFileHandler manifest = new ManifestFileHandler(partList);
			manifest.ExportToJson();
		}

		public int Count
		{
			get
			{
				return PrintItems.Count;
			}
		}

		public void RemoveAll()
		{
			for (int i = PrintItems.Count - 1; i >= 0; i--)
			{
				RemoveAt(i);
			}
		}
	}
}