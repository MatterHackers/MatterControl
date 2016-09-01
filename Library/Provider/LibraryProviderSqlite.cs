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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderSQLite : LibraryProvider
	{
		public static bool PreloadingCalibrationFiles = false;
		protected PrintItemCollection baseLibraryCollection;
		protected List<PrintItemCollection> childCollections = new List<PrintItemCollection>();

		private bool ignoreNextKeywordFilter = false;
		private string keywordFilter = string.Empty;
		private List<PrintItem> printItems = new List<PrintItem>();

		public static RootedObjectEventHandler ItemAdded = new RootedObjectEventHandler();

		private Object initializingLock = new Object ();

		public LibraryProviderSQLite(PrintItemCollection callerSuppliedCollection, Action<LibraryProvider> setCurrentLibraryProvider, LibraryProvider parentLibraryProvider, string visibleName)
			: base(parentLibraryProvider, setCurrentLibraryProvider)
		{
			this.Name = visibleName;

			// Lock ensures that SQLite providers initialized near the same time from different threads (which has been observed during debug)
			// will run in a serial fashion and only one instance will construct and assign to .baseLibraryCollection
			lock (initializingLock) 
			{
				// Use null coalescing operator to assign either the caller supplied collection or if null, the root library collection
				this.baseLibraryCollection = callerSuppliedCollection ?? GetRootLibraryCollection();
			}

			LoadLibraryItems();

			ItemAdded.RegisterEvent(DatabaseFileChange, ref unregisterEvents);
		}

		private event EventHandler unregisterEvents;

		public override void Dispose()
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			ItemAdded.UnregisterEvent(DatabaseFileChange, ref unregisterEvents);
		}

		Stopwatch timeSinceLastChange = new Stopwatch();
		private async void DatabaseFileChange(object sender, EventArgs e)
		{
			if (timeSinceLastChange.IsRunning)
			{
				// rest the time so we will wait a bit longer
				timeSinceLastChange.Restart();
				// we already have a pending update so we'll just wait for that one to complete
			}
			else
			{
				// start the time before we do the refresh
				timeSinceLastChange.Restart();

				// run a thread to wait for the time to elapse
				await Task.Run(() =>
				{
					while (timeSinceLastChange.Elapsed.TotalSeconds < .5)
					{
						Thread.Sleep(10);
					}
				});

				UiThread.RunOnIdle(() =>
				{
					if (!Datastore.Instance.WasExited())
					{
						LoadLibraryItems();
					}
				});

				timeSinceLastChange.Stop();
			}
		}

		public static string StaticProviderKey
		{
			get
			{
				return "LibraryProviderSqliteKey";
			}
		}

        public override bool CanShare { get { return false; } }

		public override int CollectionCount
		{
			get
			{
				return childCollections.Count;
			}
		}

		public override int ItemCount
		{
			get
			{
				return printItems.Count;
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
				if (ignoreNextKeywordFilter)
				{
					ignoreNextKeywordFilter = false;
					return;
				}

				PrintItemCollection rootLibraryCollection = GetRootLibraryCollection();
				if (value != ""
					&& this.baseLibraryCollection.Id != rootLibraryCollection.Id)
				{
					LibraryProviderSQLite currentProvider = this.ParentLibraryProvider as LibraryProviderSQLite;
					while (currentProvider.ParentLibraryProvider != null
						&& currentProvider.baseLibraryCollection.Id != rootLibraryCollection.Id)
					{
						currentProvider = currentProvider.ParentLibraryProvider as LibraryProviderSQLite;
					}

					if (currentProvider != null)
					{
						currentProvider.KeywordFilter = value;
						currentProvider.ignoreNextKeywordFilter = true;
						UiThread.RunOnIdle(() => SetCurrentLibraryProvider(currentProvider));
					}
				}
				else // the search only shows for the cloud library root
				{
					if (keywordFilter != value)
					{
						keywordFilter = value;

						LoadLibraryItems(); 
					}
				}
			}
		}

		public override string ProviderKey
		{
			get
			{
				return StaticProviderKey;
			}
		}

		/// <summary>
		/// Exposes all PrintItems for use in file purge code in AboutWidget
		/// </summary>
		/// <returns>A list of all print items</returns>
		public static IEnumerable<PrintItem> GetAllPrintItemsRecursive()
		{
			// NOTE: We are making the assumption that everything is reference if it does not have a 0 in PrintItemCollectionID.
			return Datastore.Instance.dbSQLite.Query<PrintItem>("SELECT * FROM PrintItem WHERE PrintItemCollectionID != 0;");
		}

		public override void AddCollectionToLibrary(string collectionName)
		{
			PrintItemCollection newCollection = new PrintItemCollection(collectionName, "");
			newCollection.ParentCollectionID = baseLibraryCollection.Id;
			newCollection.Commit();
			LoadLibraryItems();
		}

		public override void AddItem(PrintItemWrapper itemToAdd)
		{
			AddItem(itemToAdd.Name, itemToAdd.FileLocation);
		}

		public async void AddItem(string fileName, string fileLocation)
		{
			await Task.Run(() =>
			{
				if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(fileLocation))
				{
					AddStlOrGcode(fileLocation, fileName);
				}

				UiThread.RunOnIdle(() =>
				{
					LoadLibraryItems();

					ItemAdded.CallEvents(this, null);
				});
			});
		}

		public void EnsureSamplePartsExist(IEnumerable<string> filenamesToValidate)
		{
			PreloadingCalibrationFiles = true;

			// Ensure the CalibrationParts directory exists to store/import the files from disk
			string tempPath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "calibration-parts");
			Directory.CreateDirectory(tempPath);

			var existingLibaryItems = this.GetLibraryItems().Select(i => i.Name);

			// Drop extensions and build a list of files that need to be imported into the library
			var missingFiles = filenamesToValidate.Where(fileName => !existingLibaryItems.Contains(Path.GetFileNameWithoutExtension(fileName), StringComparer.OrdinalIgnoreCase));

			// Create temp files on disk that can be imported into the library
			var tempFilesToImport = missingFiles.Select(fileName =>
			{
				// Copy calibration prints from StaticData to the filesystem before importing into the library
				string tempFilePath = Path.Combine(tempPath, Path.GetFileName(fileName));
				using (FileStream outstream = File.OpenWrite(tempFilePath))
				using (Stream instream = StaticData.Instance.OpenSteam(Path.Combine("OEMSettings", "SampleParts", fileName)))
				{
					instream.CopyTo(outstream);
				}

				// Project the new filename to the output
				return tempFilePath;
			}).ToArray();

			// Import any missing files into the library
			foreach (string file in tempFilesToImport)
			{
				// Ensure these operations run in serial rather than in parallel where they stomp on each other when writing to default.mcp
				this.AddItem(Path.GetFileNameWithoutExtension(file), file);
			}

			// Finally, make sure that we always add at least one item to the queue.
			QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(tempFilesToImport[0]), tempFilesToImport[0])));

			PreloadingCalibrationFiles = false;
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			return childCollections[collectionIndex];
		}

		public IEnumerable<PrintItem> GetLibraryItems(string keyphrase = null)
		{
			string query;
			if (string.IsNullOrEmpty(keyphrase))
			{
				query = string.Format("SELECT * FROM PrintItem WHERE PrintItemCollectionID = {0} ORDER BY Name ASC;", baseLibraryCollection.Id);
			}
			else
			{
				query = string.Format("SELECT * FROM PrintItem WHERE PrintItemCollectionID = {0} AND Name LIKE '%{1}%' ORDER BY Name ASC;", baseLibraryCollection.Id, keyphrase);
			}

			return Datastore.Instance.dbSQLite.Query<PrintItem>(query);
		}

		public override string GetPrintItemName(int itemIndex)
		{
			return printItems[itemIndex].Name;
		}

		public override Task<PrintItemWrapper> GetPrintItemWrapperAsync(int index)
		{
			if (index >= 0 && index < printItems.Count)
			{
				return Task.FromResult(new PrintItemWrapper(printItems[index], this.GetProviderLocator()));
			}

			return null;
		}

		public override LibraryProvider GetProviderForCollection(PrintItemCollection collection)
		{
			return new LibraryProviderSQLite(collection, SetCurrentLibraryProvider, this, collection.Name);
		}

		public override void RemoveCollection(int collectionIndexToRemove)
		{
			childCollections[collectionIndexToRemove].Delete();
			LoadLibraryItems();
		}

		public override void RemoveItem(int itemToRemoveIndex)
		{
			if (itemToRemoveIndex < 0)
			{
				// It may be possible to have the same item in the remove list twice.
				// so if it is not in the PrintItems then ignore it.
				return;
			}

			// and remove it from the data base
			printItems[itemToRemoveIndex].Delete();

			printItems.RemoveAt(itemToRemoveIndex);

			OnDataReloaded(null);
		}

		public override void RenameCollection(int collectionIndexToRename, string newName)
		{
			childCollections[collectionIndexToRename].Name = newName;
			childCollections[collectionIndexToRename].Commit();
			LoadLibraryItems();
		}

		public override void RenameItem(int itemIndexToRename, string newName)
		{
			printItems[itemIndexToRename].Name = newName;
			printItems[itemIndexToRename].Commit();
			LoadLibraryItems();
		}

        public override void ShareItem(int itemIndexToShare)
        {

        }

		protected static void SaveToLibraryFolder(PrintItemWrapper printItemWrapper, List<MeshGroup> meshGroups, bool AbsolutePositioned)
		{
			string[] metaData = { "Created By", "MatterControl" };
			if (AbsolutePositioned)
			{
				metaData = new string[] { "Created By", "MatterControl", "BedPosition", "Absolute" };
			}

			// if it is not already in the right location
			if (!printItemWrapper.FileLocation.Contains(ApplicationDataStorage.Instance.ApplicationLibraryDataPath))
			{
				// save a copy to the library and update this to point at it
				string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".amf");
				printItemWrapper.FileLocation = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

				MeshOutputSettings outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, metaData);
				MeshFileIo.Save(meshGroups, printItemWrapper.FileLocation, outputInfo);
			}
		}

		protected virtual void AddStlOrGcode(string loadedFileName, string displayName)
		{
			string extension = Path.GetExtension(loadedFileName).ToUpper();

			PrintItem printItem = new PrintItem();
			printItem.Name = displayName;
			printItem.FileLocation = Path.GetFullPath(loadedFileName);
			printItem.PrintItemCollectionID = this.baseLibraryCollection.Id;
			printItem.Commit();

			if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension)))
			{
				List<MeshGroup> meshToConvertAndSave = MeshFileIo.Load(loadedFileName);

				try
				{
					PrintItemWrapper printItemWrapper = new PrintItemWrapper(printItem, this.GetProviderLocator());
					SaveToLibraryFolder(printItemWrapper, meshToConvertAndSave, false);
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
				PrintItemWrapper printItemWrapper = new PrintItemWrapper(printItem, this.GetProviderLocator());
				string sourceFileName = printItem.FileLocation;
				string newFileName = Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(printItem.FileLocation));
				string destFileName = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, newFileName);

				File.Copy(sourceFileName, destFileName, true);

				printItemWrapper.FileLocation = destFileName;
				printItemWrapper.PrintItem.Commit();
			}
		}

		protected IEnumerable<PrintItemCollection> GetChildCollections()
		{
			string query = string.Format("SELECT * FROM PrintItemCollection WHERE ParentCollectionID = {0} ORDER BY Name ASC;", baseLibraryCollection.Id);
			return Datastore.Instance.dbSQLite.Query<PrintItemCollection>(query);
		}

		private PrintItemCollection GetRootLibraryCollection()
		{
			// Attempt to initialize the library from the Datastore if null
			PrintItemCollection rootLibraryCollection = Datastore.Instance.dbSQLite.Table<PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault();

			// If the _library collection is still missing, create and populate it with default content
			if (rootLibraryCollection == null)
			{
				rootLibraryCollection = new PrintItemCollection();
				rootLibraryCollection.Name = "_library";
				rootLibraryCollection.Commit();

				// In this case we now need to update the baseLibraryCollection instance member as code that executes
				// down this path will attempt to use the property and will exception if its not set
				this.baseLibraryCollection = rootLibraryCollection;

				// Preload library with Oem supplied list of default parts
				EnsureSamplePartsExist(OemSettings.Instance.PreloadedLibraryFiles);
			}

			return rootLibraryCollection;
		}

		private void LoadLibraryItems()
		{
			IEnumerable<PrintItem> partFiles = null;
			IEnumerable<PrintItemCollection> collections = null;

			partFiles = GetLibraryItems(KeywordFilter);
			collections = GetChildCollections();

			printItems.Clear();
			if (partFiles != null)
			{
				printItems.AddRange(partFiles);
			}
			childCollections.Clear();
			if (collections != null)
			{
				childCollections.AddRange(collections);
			}
			OnDataReloaded(null);
		}
	}

	public class LibraryProviderSQLiteCreator : ILibraryCreator
	{
		public string ProviderKey
		{
			get
			{
				return LibraryProviderSQLite.StaticProviderKey;
			}
		}

		public virtual LibraryProvider CreateLibraryProvider(LibraryProvider parentLibraryProvider, Action<LibraryProvider> setCurrentLibraryProvider)
		{
			return new LibraryProviderSQLite(null, setCurrentLibraryProvider, parentLibraryProvider, "Local Library".Localize());
		}
	}
}