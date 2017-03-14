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
using MatterHackers.DataConverters3D;
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

		private EventHandler unregisterEvents;

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

		private async void AddItem(string fileName, string fileLocation)
		{
			await Task.Run(() =>
			{
				if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(fileLocation))
				{
					using (var stream = File.OpenRead(fileLocation))
					{
						AddItem(stream, Path.GetExtension(fileLocation).ToUpper(), fileName);
					}
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

			var existingLibaryItems = this.GetLibraryItems().Select(i => i.Name);

			// Build a list of filenames that need to be imported into the library
			var missingFiles = filenamesToValidate.Where(fileName => !existingLibaryItems.Contains(Path.GetFileNameWithoutExtension(fileName), StringComparer.OrdinalIgnoreCase));

			// Import missing content into the library
			foreach (string fileName in missingFiles)
			{
				using (Stream instream = StaticData.Instance.OpenSteam(Path.Combine("OEMSettings", "SampleParts", fileName)))
				{
					// Ideally all StaticData content should be AMF but allow STL if that's what we have
					this.AddItem(instream, Path.GetExtension(fileName), Path.GetFileNameWithoutExtension(fileName), forceAMF: false);
				}
			}

			// Finally, make sure that we always add at least one item to the queue - ensures that even without printer selection we have some content
			var firstItem = this.GetLibraryItems().FirstOrDefault();
			if (firstItem != null)
			{
				PreLoadItemToQueue(firstItem);
			}

			PreloadingCalibrationFiles = false;
		}

		private void PreLoadItemToQueue(PrintItem printItem)
		{
			string fileDest = printItem.FileLocation;
			if (!string.IsNullOrEmpty(fileDest)
				&& File.Exists(fileDest))
			{
				var printItemToAdd = new PrintItemWrapper(printItem);

				// check if there is a thumbnail image for this file and load it into the user cache if so
				string justThumbFile = printItem.Name + ".png";
				string applicationUserDataPath = StaticData.Instance.MapPath(Path.Combine("OEMSettings", "SampleParts"));
				string thumbnailSourceFile = Path.Combine(applicationUserDataPath, justThumbFile);
				if (File.Exists(thumbnailSourceFile))
				{
					string thumbnailDestFile = PartThumbnailWidget.GetImageFileName(printItemToAdd);

					try
					{
						Directory.CreateDirectory(Path.GetDirectoryName(thumbnailDestFile));

						// copy it to the right place
						File.Copy(thumbnailSourceFile, thumbnailDestFile, true);
					}
					catch
					{
					}
				}

				QueueData.Instance.AddItem(printItemToAdd);
			}
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

		/// <summary>
		/// Creates a database PrintItem entity, if forceAMF is set, converts to AMF otherwise just copies 
		/// the source file to a new library path and updates the PrintItem to point at the new target
		/// </summary>
		private void AddItem(Stream stream, string extension, string displayName, bool forceAMF = true)
		{
			// Create a new entity in the database
			PrintItem printItem = new PrintItem();
			printItem.Name = displayName;
			printItem.PrintItemCollectionID = this.baseLibraryCollection.Id;
			printItem.Commit();

			// Special load processing for mesh data, simple copy below for non-mesh
			if (forceAMF 
				&& (extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension.ToUpper())))
			{
				try
				{
					// Load mesh
					IObject3D loadedItem = MeshFileIo.Load(stream, extension);
					var meshToConvertAndSave = new List<MeshGroup> { loadedItem.Flatten() };

					// Create a new PrintItemWrapper

					if (!printItem.FileLocation.Contains(ApplicationDataStorage.Instance.ApplicationLibraryDataPath))
					{
						string[] metaData = { "Created By", "MatterControl" };
						if (false) //AbsolutePositioned
						{
							metaData = new string[] { "Created By", "MatterControl", "BedPosition", "Absolute" };
						}

						// save a copy to the library and update this to point at it
						printItem.FileLocation = CreateLibraryPath(".amf");
						var outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, metaData);
						MeshFileIo.Save(meshToConvertAndSave, printItem.FileLocation, outputInfo);
						printItem.Commit();
					}
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
				// Non-mesh content - copy stream to new Library path
				printItem.FileLocation = CreateLibraryPath(extension);
				using (var outStream = File.Create(printItem.FileLocation))
				{
					stream.CopyTo(outStream);
				}
				printItem.Commit();
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

		private static string CreateLibraryPath(string extension)
		{
			string fileName = Path.ChangeExtension(Path.GetRandomFileName(), string.IsNullOrEmpty(extension) ? ".amf" : extension);
			return Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);
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