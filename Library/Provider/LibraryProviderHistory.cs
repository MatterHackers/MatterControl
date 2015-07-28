/*
Copyright (c) 2015, Lars Brubaker
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintHistory;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderHistory : LibraryProvider
	{
		private static LibraryProviderHistory instance = null;
		private PrintItemCollection baseLibraryCollection;

		private List<PrintItemCollection> childCollections = new List<PrintItemCollection>();
		private string keywordFilter = string.Empty;

		EventHandler unregisterEvent;

		public LibraryProviderHistory(PrintItemCollection baseLibraryCollection, LibraryProvider parentLibraryProvider)
			: base(parentLibraryProvider)
		{
			this.baseLibraryCollection = baseLibraryCollection;

			//PrintHistoryData.Instance.ItemAdded.RegisterEvent((sender, e) => OnDataReloaded(null), ref unregisterEvent);
		}

		public static LibraryProvider Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new LibraryProviderHistory(null, null);
				}

				return instance;
			}
		}

		public override string GetPrintItemName(int itemIndex)
		{
			return "item";
			//return PrintHistoryData.Instance.GetHistoryItem(itemIndex);
		}

		public override void RenameCollection(int collectionIndexToRename, string newName)
		{
			throw new NotImplementedException();
		}

		public override void RenameItem(int itemIndexToRename, string newName)
		{
			throw new NotImplementedException();
		}

		public static string StaticProviderKey
		{
			get
			{
				return "LibraryProviderHistoryKey";
			}
		}

		public override bool Visible
		{
			get { return true; }
		}

		public override void Dispose()
		{
		}

		public override int CollectionCount
		{
			get
			{
				return 0;
			}
		}

		public override int ItemCount
		{
			get
			{
				return 10;
				//return PrintHistoryData.Instance.Count;
			}
		}

		public override string KeywordFilter
		{
			get
			{
				return keywordFilter;
			}

			set
			{
				keywordFilter = value;
			}
		}

		public override string Name
		{
			get
			{
				return "Print History";
			}
		}

		public override string ProviderData
		{
			get 
			{
				return baseLibraryCollection.Id.ToString();
			}
		}

		public override string ProviderKey
		{
			get
			{
				return StaticProviderKey;
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

		public override void AddCollectionToLibrary(string collectionName)
		{
		}

		public override void AddItem(PrintItemWrapper itemToAdd)
		{
			throw new NotImplementedException();
			//PrintHistoryData.Instance.AddItem(itemToAdd);
		}

		public void AddItem(PrintItemWrapper item, int indexToInsert = -1)
		{
			throw new NotImplementedException();
			//PrintHistoryData.Instance.AddItem(item, indexToInsert);
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			return childCollections[collectionIndex];
		}

		public async override Task<PrintItemWrapper> GetPrintItemWrapperAsync(int index, ReportProgressRatio reportProgress = null)
		{
			throw new NotImplementedException();
			//return PrintHistoryData.Instance.GetPrintItemWrapper(index);
		}

		public override LibraryProvider GetProviderForCollection(PrintItemCollection collection)
		{
			return new LibraryProviderHistory(collection, this);
		}

		public override void RemoveCollection(int collectionIndexToRemove)
		{
		}

		public override void RemoveItem(int itemToRemoveIndex)
		{
			throw new NotImplementedException();
			//PrintHistoryData.Instance.RemoveAt(itemToRemoveIndex);
			OnDataReloaded(null);
		}

		private static void AddStlOrGcode(LibraryProviderHistory libraryToAddTo, string loadedFileName, string extension)
		{
			PrintItem printItem = new PrintItem();
			printItem.Name = Path.GetFileNameWithoutExtension(loadedFileName);
			printItem.FileLocation = Path.GetFullPath(loadedFileName);
			printItem.PrintItemCollectionID = libraryToAddTo.baseLibraryCollection.Id;
			printItem.Commit();

			if (MeshFileIo.ValidFileExtensions().Contains(extension))
			{
				List<MeshGroup> meshToConvertAndSave = MeshFileIo.Load(loadedFileName);

				try
				{
					PrintItemWrapper printItemWrapper = new PrintItemWrapper(printItem, libraryToAddTo);
					SaveToLibraryFolder(printItemWrapper, meshToConvertAndSave, false);
					libraryToAddTo.AddItem(printItemWrapper);
				}
				catch (System.UnauthorizedAccessException)
				{
					UiThread.RunOnIdle(() =>
					{
						//Do something special when unauthorized?
						StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes, unauthorized access", "Unable to save");
					});
				}
				catch
				{
					UiThread.RunOnIdle(() =>
					{
						StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
					});
				}
			}
			else // it is not a mesh so just add it
			{
				PrintItemWrapper printItemWrapper = new PrintItemWrapper(printItem, libraryToAddTo);
				if (false)
				{
					libraryToAddTo.AddItem(printItemWrapper);
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
					libraryToAddTo.AddItem(printItemWrapper);
				}
			}
		}

		private IEnumerable<PrintItemCollection> GetChildCollections()
		{
			string query = string.Format("SELECT * FROM PrintItemCollection WHERE ParentCollectionID = {0} ORDER BY Name ASC;", baseLibraryCollection.Id);
			IEnumerable<PrintItemCollection> result = (IEnumerable<PrintItemCollection>)Datastore.Instance.dbSQLite.Query<PrintItemCollection>(query);
			return result;
		}

		public IEnumerable<PrintItem> GetLibraryItems(string keyphrase = null)
		{
			string query;
			if (keyphrase == null)
			{
				query = string.Format("SELECT * FROM PrintItem WHERE PrintItemCollectionID = {0} ORDER BY Name ASC;", baseLibraryCollection.Id);
			}
			else
			{
				query = string.Format("SELECT * FROM PrintItem WHERE PrintItemCollectionID = {0} AND Name LIKE '%{1}%' ORDER BY Name ASC;", baseLibraryCollection.Id, keyphrase);
			}
			IEnumerable<PrintItem> result = (IEnumerable<PrintItem>)Datastore.Instance.dbSQLite.Query<PrintItem>(query);
			return result;
		}

		private void loadFilesIntoLibraryBackgoundWorker_DoWork(IList<string> fileList)
		{
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
								AddStlOrGcode(this, part.FileLocation, Path.GetExtension(part.FileLocation).ToUpper());
							}
						}
					}
					else
					{
						AddStlOrGcode(this, loadedFileName, extension);
					}
				}
			}
		}
	}
}