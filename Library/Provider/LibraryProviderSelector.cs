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
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using MatterHackers.Localizations;
using System.IO;
using System.Linq;
using MatterHackers.Agg.UI;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using System.Threading;
using MatterHackers.Agg.ImageProcessing;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderSelector : LibraryProvider
	{
		private List<ILibraryCreator> libraryCreators = new List<ILibraryCreator>();

		private Dictionary<int, LibraryProvider> libraryProviders = new Dictionary<int, LibraryProvider>();

		public ILibraryCreator PurchasedLibraryCreator { get; private set; }

		public ILibraryCreator SharedLibraryCreator { get; private set; }

		private EventHandler unregisterEvents;

		private List<ImageBuffer> folderImagesForChildren = new List<ImageBuffer>();

		private int firstAddedDirectoryIndex;

		private PluginFinder<LibraryProviderPlugin> libraryProviderPlugins;

		private bool includeQueueLibraryProvider;

		public static readonly string ProviderKeyName = "ProviderSelectorKey";

		public static RootedObjectEventHandler LibraryRootNotice = new RootedObjectEventHandler();

		static LibraryProviderSelector currentInstance;

		public LibraryProviderSelector(Action<LibraryProvider> setCurrentLibraryProvider, bool includeQueueLibraryProvider)
			: base(null, setCurrentLibraryProvider)
		{
			currentInstance = this;

			this.includeQueueLibraryProvider = includeQueueLibraryProvider;
			this.Name = "Home".Localize();

			LibraryRootNotice.RegisterEvent((sender, args) =>
			{
				this.ReloadData();
			}, ref unregisterEvents);

			ApplicationController.Instance.CloudSyncStatusChanged.RegisterEvent(CloudSyncStatusChanged, ref unregisterEvents);

			libraryProviderPlugins = new PluginFinder<LibraryProviderPlugin>();

			ReloadData();
		}


		public static void Reload()
		{
			currentInstance.ReloadData();
		}

		private void ReloadData()
		{
			libraryCreators.Clear();
			folderImagesForChildren.Clear();

			if (includeQueueLibraryProvider)
			{
				// put in the queue provider
				libraryCreators.Add(new LibraryProviderQueueCreator());
				AddFolderImage("queue_folder.png");
			}

			/*
			if (false)
			{
				// put in the history provider
				libraryCreators.Add(new LibraryProviderHistoryCreator());
				AddFolderImage("queue_folder.png");
			} */

			// put in the sqlite provider
			libraryCreators.Add(new LibraryProviderSQLiteCreator());
			AddFolderImage("library_folder.png");

			// Check for LibraryProvider factories and put them in the list too.
			foreach (LibraryProviderPlugin libraryProviderPlugin in libraryProviderPlugins.Plugins)
			{
				if (libraryProviderPlugin.ProviderKey == "LibraryProviderPurchasedKey")
				{
					this.PurchasedLibraryCreator = libraryProviderPlugin;
				}

				if (libraryProviderPlugin.ProviderKey == "LibraryProviderSharedKey")
				{
					this.SharedLibraryCreator = libraryProviderPlugin;
				}

				if (libraryProviderPlugin.ShouldBeShown())
				{
					// This coupling is required to navigate to the Purchased folder after redemption or purchase updates
					libraryCreators.Add(libraryProviderPlugin);
					folderImagesForChildren.Add(libraryProviderPlugin.GetFolderImage());
				}
			}

			// and any directory providers (sd card provider, etc...)
			// Add "Downloads" file system example
			string downloadsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			if (Directory.Exists(downloadsDirectory))
			{
				libraryCreators.Add(
					new LibraryProviderFileSystemCreator(
						downloadsDirectory,
						"Downloads".Localize(),
						useIncrementedNameDuringTypeChange: true));

				AddFolderImage("download_folder.png");
			}

			string userLibraryFoldersPath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "LibraryFolders.conf");
			if (File.Exists(userLibraryFoldersPath))
			{
				foreach (string directory in File.ReadLines(userLibraryFoldersPath))
				{
					if (Directory.Exists(directory))
					{
						libraryCreators.Add(
							new LibraryProviderFileSystemCreator(
								directory,
								(new DirectoryInfo(directory).Name),
								useIncrementedNameDuringTypeChange: true));

						AddFolderImage("download_folder.png");
					}
				}
			}

			firstAddedDirectoryIndex = libraryCreators.Count;
			OnDataReloaded(null);
		}

		public override bool IsProtected()
		{
			return true;
		}

		private void AddFolderImage(string iconFileName)
		{
			string libraryIconPath = Path.Combine("FileDialog", iconFileName);
			ImageBuffer libraryFolderImage = StaticData.Instance.LoadIcon(libraryIconPath).InvertLightness();
			folderImagesForChildren.Add(libraryFolderImage);
		}

		public override ImageBuffer GetCollectionFolderImage(int collectionIndex)
		{
			return folderImagesForChildren[collectionIndex];
		}

		public override void RenameCollection(int collectionIndexToRename, string newName)
		{
			if (collectionIndexToRename >= firstAddedDirectoryIndex)
			{
				LibraryProviderFileSystemCreator fileSystemLibraryCreator = libraryCreators[collectionIndexToRename] as LibraryProviderFileSystemCreator;
				if (fileSystemLibraryCreator != null
					&& fileSystemLibraryCreator.Description != newName)
				{
					fileSystemLibraryCreator.Description = newName;
					UiThread.RunOnIdle(() => OnDataReloaded(null));
				}
			}
		}

		public override void RenameItem(int itemIndexToRename, string newName)
		{
			throw new NotImplementedException();
		}

		public override bool CanShare { get { return false; } }

		public override void ShareItem(int itemIndexToShare)
		{
		}

		public void CloudSyncStatusChanged(object sender, EventArgs eventArgs)
		{
			var e = eventArgs as ApplicationController.CloudSyncEventArgs;

			// If signing out, we need to force selection to this provider
			if (e?.IsAuthenticated == false
				&& SetCurrentLibraryProvider != null)
			{
				// Switch to the selector
				SetCurrentLibraryProvider(this);
			}
		}

		#region Overriden Abstract Methods

		public override int CollectionCount
		{
			get
			{
				if (string.IsNullOrEmpty(KeywordFilter))
				{
					return this.libraryCreators.Count;
				}
				else
				{
					return 0;
				}
			}
		}

		public override int ItemCount
		{
			get
			{
				if (string.IsNullOrEmpty(KeywordFilter))
				{
					return 0;
				}
				else
				{
					return 1;
				}
			}
		}

		string keywordFilter = "";
		public override string KeywordFilter
		{
			get
			{
				return keywordFilter;
			}

			set
			{
				// if the filter is changing
				if (keywordFilter != value)
				{
					keywordFilter = value;

					OnDataReloaded(null);
				}
			}
		}

		public override string ProviderKey
		{
			get
			{
				return LibraryProviderSelector.ProviderKeyName;
			}
		}

		public override void AddCollectionToLibrary(string collectionName)
		{
			UiThread.RunOnIdle(() =>
			FileDialog.SelectFolderDialog(new SelectFolderDialogParams("Select Folder"), (SelectFolderDialogParams folderParams) =>
			{
				libraryCreators.Add(new LibraryProviderFileSystemCreator(folderParams.FolderPath, collectionName));
				AddFolderImage("folder.png");
				UiThread.RunOnIdle(() => OnDataReloaded(null));
			}));
		}

		public override void AddItem(PrintItemWrapper itemToAdd)
		{
			if (Directory.Exists(itemToAdd.FileLocation))
			{
				libraryCreators.Add(new LibraryProviderFileSystemCreator(itemToAdd.FileLocation, Path.GetFileName(itemToAdd.FileLocation)));
				AddFolderImage("folder.png");
				UiThread.RunOnIdle(() => OnDataReloaded(null));
			}
		}

		public override GuiWidget GetItemThumbnail(int printItemIndex)
		{
			ImageBuffer warningImage = StaticData.Instance.LoadIcon(Path.Combine("FileDialog", "file.png"), 48, 48).InvertLightness();

			return new ImageWidget(warningImage)
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor,
			};
		}

		public override void Dispose()
		{
			foreach (KeyValuePair<int, LibraryProvider> keyValue in libraryProviders)
			{
				keyValue.Value.Dispose();
			}

			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			if (libraryProviders.ContainsKey(collectionIndex))
			{
				libraryProviders[collectionIndex].Dispose();
				libraryProviders.Remove(collectionIndex);
			}
			LibraryProvider provider = libraryCreators[collectionIndex].CreateLibraryProvider(this, SetCurrentLibraryProvider);
			libraryProviders.Add(collectionIndex, provider);
			return new PrintItemCollection(provider.Name, provider.ProviderKey);
		}

		public override string GetPrintItemName(int itemIndex)
		{
			return LibraryRowItem.SearchResultsNotAvailableToken;
		}

		public override Task<PrintItemWrapper> GetPrintItemWrapperAsync(int itemIndex)
		{
			var printItemWrapper = new PrintItemWrapper(new PrintItem(LibraryRowItem.SearchResultsNotAvailableToken, LibraryRowItem.SearchResultsNotAvailableToken), this.GetProviderLocator())
			{
				UseIncrementedNameDuringTypeChange = true
			};

			return Task.FromResult(printItemWrapper);
		}

		public override LibraryProvider GetProviderForCollection(PrintItemCollection collection)
		{
			LibraryProvider provider = libraryProviders.Values.Where(p => p.ProviderKey == collection.Key).FirstOrDefault();
			if (provider != null)
			{
				return provider;
			}

			foreach (ILibraryCreator libraryCreator in libraryCreators)
			{
				if (collection.Key == libraryCreator.ProviderKey)
				{
					return libraryCreator.CreateLibraryProvider(this, SetCurrentLibraryProvider);
				}
			}

			throw new NotImplementedException();
		}

		public override void RemoveCollection(int collectionIndexToRemove)
		{
			libraryCreators.RemoveAt(collectionIndexToRemove);

			UiThread.RunOnIdle(() => OnDataReloaded(null));
		}

		public override void RemoveItem(int itemToRemoveIndex)
		{
			throw new NotImplementedException();
		}

		#endregion Overriden Abstract Methods

		public LibraryProvider GetPurchasedLibrary()
		{
			((LibraryProviderPlugin)PurchasedLibraryCreator).ForceVisible();
			LibraryProvider purchasedProvider = PurchasedLibraryCreator.CreateLibraryProvider(this, SetCurrentLibraryProvider);
			return purchasedProvider;
		}

        public LibraryProvider GetSharedLibrary()
        {
            ((LibraryProviderPlugin)SharedLibraryCreator).ForceVisible();
            LibraryProvider sharedProvider = SharedLibraryCreator.CreateLibraryProvider(this, SetCurrentLibraryProvider);
            return sharedProvider;
        }
	}
}