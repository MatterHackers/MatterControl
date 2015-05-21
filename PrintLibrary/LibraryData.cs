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
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class LibraryData
	{
		private List<PrintItemWrapper> printItems = new List<PrintItemWrapper>();

		public List<PrintItemWrapper> PrintItems
		{
			get { return printItems; }
		}

		public RootedObjectEventHandler DataReloaded = new RootedObjectEventHandler();
		public RootedObjectEventHandler ItemAdded = new RootedObjectEventHandler();
		public RootedObjectEventHandler ItemRemoved = new RootedObjectEventHandler();
		public RootedObjectEventHandler OrderChanged = new RootedObjectEventHandler();

		private DataStorage.PrintItemCollection libraryCollection;

		private static LibraryData instance;

		public static LibraryData Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new LibraryData();
					instance.LoadLibraryItems();
				}
				return instance;
			}
		}

		static public void SaveToLibraryFolder(PrintItemWrapper printItemWrapper, List<MeshGroup> meshGroups, bool AbsolutePositioned)
		{
			string[] metaData = { "Created By", "MatterControl" };
			if (AbsolutePositioned)
			{
				metaData = new string[] { "Created By", "MatterControl", "BedPosition", "Absolute" };
			}
			if (printItemWrapper.FileLocation.Contains(ApplicationDataStorage.Instance.ApplicationLibraryDataPath))
			{
				MeshOutputSettings outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, metaData);
				MeshFileIo.Save(meshGroups, printItemWrapper.FileLocation, outputInfo);
			}
			else // save a copy to the library and update this to point at it
			{
				string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".amf");
				printItemWrapper.FileLocation = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

				MeshOutputSettings outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, metaData);
				MeshFileIo.Save(meshGroups, printItemWrapper.FileLocation, outputInfo);

				printItemWrapper.PrintItem.Commit();

				// let the queue know that the item has changed so it load the correct part
				QueueData.Instance.SaveDefaultQueue();
			}

			printItemWrapper.OnFileHasChanged();
		}

		private string keywordFilter = "";

		public string KeywordFilter
		{
			get { return keywordFilter; }
			set
			{
				if (this.keywordFilter != value)
				{
					this.keywordFilter = value;
					LoadLibraryItems();
				}
			}
		}

		public void AddItem(PrintItemWrapper item, int indexToInsert = -1)
		{
			if (indexToInsert == -1)
			{
				indexToInsert = PrintItems.Count;
			}
			PrintItems.Insert(indexToInsert, item);
			OnItemAdded(new IndexArgs(indexToInsert));
            item.PrintItem.PrintItemCollectionID = LibraryData.Instance.LibraryCollection.Id;
            item.PrintItem.Commit();
           
		}

		public void RemoveItem(PrintItemWrapper printItemWrapper)
		{
			int index = PrintItems.IndexOf(printItemWrapper);
			if (index < 0)
			{
				// It may be possible to have the same item in the remove list twice.
				// so if it is not in the PrintItems then ignore it.
				return;
			}
			PrintItems.RemoveAt(index);

			// and remove it from the data base
			printItemWrapper.Delete();

			OnItemRemoved(new IndexArgs(index));
		}

		public PrintItemWrapper GetPrintItemWrapper(int index)
		{
			if (index >= 0 && index < Count)
			{
				return PrintItems[index];
			}

			return null;
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

		public DataStorage.PrintItemCollection LibraryCollection
		{
			get
			{
				// Attempt to initialize the library from the Datastore if null
				if (libraryCollection == null)
				{
					libraryCollection = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault();
				}

				// If the _library collection is still missing, create and populate it with default content
				if (libraryCollection == null)
				{
					libraryCollection = new PrintItemCollection();
					libraryCollection.Name = "_library";
					libraryCollection.Commit();

					// Preload library with Oem supplied list of default parts
					string[] itemsToAdd = LibraryData.SyncCalibrationFilesToDisk(OemSettings.Instance.PreloadedLibraryFiles);
					if (itemsToAdd.Length > 0)
					{
						// Import any files sync'd to disk into the library, then add them to the queue
						LibraryData.Instance.LoadFilesIntoLibrary(itemsToAdd);
					}
				}
				return libraryCollection;
			}
		}

		internal static string[] SyncCalibrationFilesToDisk(List<string> calibrationPrintFileNames)
		{
			// Ensure the CalibrationParts directory exists to store/import the files from disk
			string tempPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationUserDataPath, "data", "temp", "calibration-parts");
			Directory.CreateDirectory(tempPath);

			// Build a list of temporary files that should be imported into the library
			return calibrationPrintFileNames.Where(fileName =>
			{
				// Filter out items that already exist in the library
				return LibraryData.Instance.GetLibraryItems(Path.GetFileNameWithoutExtension(fileName)).Count() <= 0;
			}).Select(fileName =>
			{
				// Copy calibration prints from StaticData to the filesystem before importing into the library
				string tempFile = Path.Combine(tempPath, Path.GetFileName(fileName));
				using (FileStream outstream = File.OpenWrite(tempFile))
				using (Stream instream = StaticData.Instance.OpenSteam(Path.Combine("OEMSettings", "SampleParts", fileName)))
				{
					instream.CopyTo(outstream);
				}

				// Project the new filename to the output
				return tempFile;
			}).ToArray();
		}

		internal IEnumerable<DataStorage.PrintItem> GetLibraryItems(string keyphrase = null)
		{
			if (LibraryCollection == null)
			{
				return null;
			}
			else
			{
				string query;
				if (keyphrase == null)
				{
					query = string.Format("SELECT * FROM PrintItem WHERE PrintItemCollectionID = {0} ORDER BY Name ASC;", libraryCollection.Id);
				}
				else
				{
					query = string.Format("SELECT * FROM PrintItem WHERE PrintItemCollectionID = {0} AND Name LIKE '%{1}%' ORDER BY Name ASC;", libraryCollection.Id, keyphrase);
				}
				IEnumerable<DataStorage.PrintItem> result = (IEnumerable<DataStorage.PrintItem>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.PrintItem>(query);
				return result;
			}
		}

		public void LoadLibraryItems()
		{
			PrintItems.Clear();
			IEnumerable<DataStorage.PrintItem> partFiles = GetLibraryItems(keywordFilter);
			if (partFiles != null)
			{
				foreach (PrintItem part in partFiles)
				{
					PrintItems.Add(new PrintItemWrapper(part));
				}
			}
			OnDataReloaded(null);
		}

		public void OnDataReloaded(EventArgs e)
		{
			DataReloaded.CallEvents(this, e);
		}

		public void OnItemAdded(EventArgs e)
		{
			ItemAdded.CallEvents(this, e);
		}

		public void OnItemRemoved(EventArgs e)
		{
			ItemRemoved.CallEvents(this, e);
		}

		public void SaveLibraryItems()
		{
			//
		}

		public int Count
		{
			get
			{
				return PrintItems.Count;
			}
		}

		private ReportProgressRatio fileLoadReportProgress = null;

		public void LoadFilesIntoLibrary(IList<string> files, ReportProgressRatio reportProgress = null, RunWorkerCompletedEventHandler callback = null)
		{
			this.fileLoadReportProgress = reportProgress;
			if (files != null && files.Count > 0)
			{
				BackgroundWorker loadFilesIntoLibraryBackgroundWorker = new BackgroundWorker();
				loadFilesIntoLibraryBackgroundWorker.WorkerReportsProgress = true;

				loadFilesIntoLibraryBackgroundWorker.DoWork += new DoWorkEventHandler(loadFilesIntoLibraryBackgoundWorker_DoWork);
				loadFilesIntoLibraryBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loadFilesIntoLibraryBackgroundWorker_RunWorkerCompleted);

				if (callback != null)
				{
					loadFilesIntoLibraryBackgroundWorker.RunWorkerCompleted += callback;
				}

				loadFilesIntoLibraryBackgroundWorker.RunWorkerAsync(files);
			}
		}

		private void loadFilesIntoLibraryBackgoundWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			IList<string> fileList = e.Argument as IList<string>;
			foreach (string loadedFileName in fileList)
			{
				string extension = Path.GetExtension(loadedFileName).ToUpper();
				if (MeshFileIo.ValidFileExtensions().Contains(extension)
					|| extension == ".GCODE"
					|| extension == ".ZIP")
				{
					if (extension == ".ZIP")
					{
						ProjectFileHandler project = new ProjectFileHandler(null);
						List<PrintItem> partFiles = project.ImportFromProjectArchive(loadedFileName);
						if (partFiles != null)
						{
							foreach (PrintItem part in partFiles)
							{
								AddStlOrGcode(part.FileLocation, Path.GetExtension(part.FileLocation).ToUpper());
							}
						}
					}
					else
					{
						AddStlOrGcode(loadedFileName, extension);
					}
				}
			}
		}

		private static void AddStlOrGcode(string loadedFileName, string extension)
		{
			PrintItem printItem = new PrintItem();
			printItem.Name = Path.GetFileNameWithoutExtension(loadedFileName);
			printItem.FileLocation = Path.GetFullPath(loadedFileName);
			printItem.PrintItemCollectionID = LibraryData.Instance.LibraryCollection.Id;
			printItem.Commit();

			if (MeshFileIo.ValidFileExtensions().Contains(extension))
			{
				List<MeshGroup> meshToConvertAndSave = MeshFileIo.Load(loadedFileName);

				try
				{
					PrintItemWrapper printItemWrapper = new PrintItemWrapper(printItem);
					LibraryData.SaveToLibraryFolder(printItemWrapper, meshToConvertAndSave, false);
					LibraryData.Instance.AddItem(printItemWrapper);
				}
				catch (System.UnauthorizedAccessException)
				{
					UiThread.RunOnIdle((state) =>
					{
						//Do something special when unauthorized?
						StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes, unauthorized access", "Unable to save");
					});
				}
				catch
				{
					UiThread.RunOnIdle((state) =>
					{
						StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
					});
				}
			}
			else // it is not a mesh so just add it
			{
				PrintItemWrapper printItemWrapper = new PrintItemWrapper(printItem);
				if (false)
				{
					LibraryData.Instance.AddItem(printItemWrapper);
				}
				else // save a copy to the library and update this to point at it
				{
					string sourceFileName = printItem.FileLocation;
					string newFileName = Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(printItem.FileLocation));
					string destFileName = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, newFileName);

					File.Copy(sourceFileName, destFileName, true);

					printItemWrapper.FileLocation = destFileName;
					printItemWrapper.PrintItem.Commit();

					// let the queue know that the item has changed so it load the correct part
					LibraryData.Instance.AddItem(printItemWrapper);
				}
			}
		}

		private void loadFilesIntoLibraryBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
		}
	}
}