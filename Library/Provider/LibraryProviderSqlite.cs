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
	public class LibraryProviderSQLiteCreator : ILibraryCreator
	{
		public virtual LibraryProvider CreateLibraryProvider(LibraryProvider parentLibraryProvider)
		{
			return new LibraryProviderSQLite(null, parentLibraryProvider, "Local Library");
		}

		public string ProviderKey
		{
			get
			{
				return LibraryProviderSQLite.StaticProviderKey;
			}
		}
	}

	public class LibraryProviderSQLite : ClassicSqliteStorageProvider
	{
		public bool PreloadingCalibrationFiles = false;

		private static LibraryProviderSQLite instance = null;

		private List<PrintItemWrapper> printItems = new List<PrintItemWrapper>();

		private string visibleName;

		public LibraryProviderSQLite(PrintItemCollection baseLibraryCollection, LibraryProvider parentLibraryProvider, string visibleName)
			: base(parentLibraryProvider)
		{
			this.visibleName = visibleName;

			if (baseLibraryCollection == null)
			{
				baseLibraryCollection = GetRootLibraryCollection2().Result;
			}

			this.baseLibraryCollection = baseLibraryCollection;
			LoadLibraryItems();
		}

		public static LibraryProviderSQLite Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new LibraryProviderSQLite(null, null, "Local Library");
				}

				return instance;
			}
		}

		public override string GetPrintItemName(int itemIndex)
		{
			return printItems[itemIndex].Name;
		}

		public override void RenameCollection(int collectionIndexToRename, string newName)
		{
			childCollections[collectionIndexToRename].Name = newName;
			childCollections[collectionIndexToRename].Commit();
			LoadLibraryItems();
		}

		public override void RenameItem(int itemIndexToRename, string newName)
		{
			printItems[itemIndexToRename].PrintItem.Name = newName;
			printItems[itemIndexToRename].PrintItem.Commit();
			LoadLibraryItems();
		}

		public static string StaticProviderKey
		{
			get
			{
				return "LibraryProviderSqliteKey";
			}
		}

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

		public override string Name
		{
			get
			{
				return visibleName;
			}
		}

		public override string ProviderKey
		{
			get
			{
				return StaticProviderKey;
			}
		}

		public override void AddCollectionToLibrary(string collectionName)
		{
			PrintItemCollection newCollection = new PrintItemCollection(collectionName, "");
			newCollection.ParentCollectionID = baseLibraryCollection.Id;
			newCollection.Commit();
			LoadLibraryItems();
		}

		public async override void AddItem(PrintItemWrapper itemToAdd)
		{
			await AddItemAsync(itemToAdd.Name, itemToAdd.FileLocation, fireDataReloaded: true);

			LoadLibraryItems();
			OnDataReloaded(null);
		}

		public async Task AddItemAsync(string fileName, string fileLocation, bool fireDataReloaded)
		{
			if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(fileLocation))
			{
				await Task.Run(() => AddStlOrGcode(fileLocation, fileName));
			}
		}

		public async override Task<PrintItemWrapper> GetPrintItemWrapperAsync(int index)
		{
			if (index >= 0 && index < printItems.Count)
			{
				return printItems[index];
			}

			return null;
		}

		public override LibraryProvider GetProviderForCollection(PrintItemCollection collection)
		{
			return new LibraryProviderSQLite(collection, this, collection.Name);
		}

		public override void RemoveCollection(int collectionIndexToRemove)
		{
			childCollections[collectionIndexToRemove].Delete();
			LoadLibraryItems();
			OnDataReloaded(null);
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

		public async Task EnsureSamplePartsExist(IEnumerable<string> filenamesToValidate)
		{
			PreloadingCalibrationFiles = true;

			// Ensure the CalibrationParts directory exists to store/import the files from disk
			string tempPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationUserDataPath, "data", "temp", "calibration-parts");
			Directory.CreateDirectory(tempPath);

			var existingLibaryItems = this.GetLibraryItems().Select(i => i.Name);

			// Build a list of files that need to be imported into the library
			var missingFiles = filenamesToValidate.Where(fileName => !existingLibaryItems.Contains(fileName, StringComparer.OrdinalIgnoreCase));

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
				await this.AddItemAsync(Path.GetFileNameWithoutExtension(file), file, false);
			}

			PreloadingCalibrationFiles = false;
		}

		/// <summary>
		/// Exposes all PrintItems for use in file purge code in AboutWidget
		/// </summary>
		/// <returns>A list of all print items</returns>
		public static IEnumerable<PrintItem> GetAllPrintItemsRecursive()
		{
			// NOTE: We are making the assumption that everything is reference if it does not have a 0 in eth PrintItemCollectionID.
			string query = "SELECT * FROM PrintItem WHERE PrintItemCollectionID != 0;";
			IEnumerable<PrintItem> result = (IEnumerable<PrintItem>)Datastore.Instance.dbSQLite.Query<PrintItem>(query);
			return result;
		}

		private async Task<PrintItemCollection> GetRootLibraryCollection2()
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
				await this.EnsureSamplePartsExist(OemSettings.Instance.PreloadedLibraryFiles);
			}

			return rootLibraryCollection;
		}

		private void LoadLibraryItems()
		{
			printItems.Clear();
			IEnumerable<PrintItem> partFiles = GetLibraryItems(KeywordFilter);
			if (partFiles != null)
			{
				foreach (PrintItem part in partFiles)
				{
					PrintItemWrapper item = new PrintItemWrapper(part, this);
					printItems.Add(item);
				}
			}

			childCollections.Clear();
			GetChildCollections();
			IEnumerable<PrintItemCollection> collections = GetChildCollections();
			if (collections != null)
			{
				childCollections.AddRange(collections);
			}

			OnDataReloaded(null);
		}
	}
}