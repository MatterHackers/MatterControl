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

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderSelector : LibraryProvider
	{
		private static LibraryProviderSelector instance = null;
		private List<LibraryProvider> libraryProviders = new List<LibraryProvider>();

		private List<LibraryProvider> visibleProviders;

		internal LibraryProvider PurchasedLibrary { get; private set; }

		private event EventHandler unregisterEvents;

		List<ImageBuffer> folderImagesForChildren = new List<ImageBuffer>();

		private LibraryProviderSelector()
			: base(null)
		{

			ApplicationController.Instance.CloudSyncStatusChanged.RegisterEvent(CloudSyncStatusChanged, ref unregisterEvents);

			// put in the sqlite provider
			libraryProviders.Add(new LibraryProviderSQLite(null, this));

			AddFolderImage("library_folder.png");

			// Check for LibraryProvider factories and put them in the list too.
			PluginFinder<LibraryProviderPlugin> libraryProviderPlugins = new PluginFinder<LibraryProviderPlugin>();
			foreach (LibraryProviderPlugin libraryProviderPlugin in libraryProviderPlugins.Plugins)
			{
				// This coupling is required to navigate to the Purchased folder after redemption or purchase updates
				var pluginProvider = libraryProviderPlugin.CreateLibraryProvider(this);
				if (pluginProvider.ProviderKey == "LibraryProviderPurchasedKey")
				{
					this.PurchasedLibrary = pluginProvider;
				}

				libraryProviders.Add(pluginProvider);
				folderImagesForChildren.Add(libraryProviderPlugin.GetFolderImage());
			}

			// and any directory providers (sd card provider, etc...)
			// Add "Downloads" file system example
			string downloadsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			if (Directory.Exists(downloadsDirectory))
			{
				libraryProviders.Add(new LibraryProviderFileSystem(downloadsDirectory, "Downloads", this));
				AddFolderImage("download_folder.png");
			}

			this.FilterProviders();
		}

		private void AddFolderImage(string iconFileName)
		{
			string libraryIconPath = Path.Combine("Icons", "FileDialog", iconFileName);
			ImageBuffer libraryFolderImage = new ImageBuffer();
			StaticData.Instance.LoadImage(libraryIconPath, libraryFolderImage);
			folderImagesForChildren.Add(libraryFolderImage);
		}

		public override ImageBuffer GetCollectionFolderImage(int collectionIndex)
		{
			return folderImagesForChildren[collectionIndex];
		}

		private void FilterProviders()
		{
			this.visibleProviders = libraryProviders.Where(p => p.Visible).ToList();
			OnDataReloaded(null);
		}

		public void CloudSyncStatusChanged(object sender, EventArgs eventArgs)
		{
			var e = eventArgs as ApplicationController.CloudSyncEventArgs;

			// If signing out, we need to force selection to this provider
			if(e != null && !e.IsAuthenticated)
			{
				// Switch to the purchased library
				LibraryDataView.CurrentLibraryProvider = this;
			}

			// Refresh state
			UiThread.RunOnIdle(FilterProviders, 1);
		}

		public static LibraryProviderSelector Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new LibraryProviderSelector();
				}

				return instance;
			}
		}

		#region Overriden Abstract Methods

		public static string LibraryProviderSelectorKey
		{
			get
			{
				return "ProviderSelectorKey";
			}
		}

		public override int CollectionCount
		{
			get
			{
				return this.visibleProviders.Count;
			}
		}

		public override bool Visible
		{
			get { return true; }
		}

		public override void Dispose()
		{
		}

		public override int ItemCount
		{
			get
			{
				return 0;
			}
		}

		public override string KeywordFilter
		{
			get
			{
				return "";
			}

			set
			{
			}
		}

		public override string Name
		{
			get
			{
				return "Home".Localize();
			}
		}

		public override string ProviderData
		{
			get { return ""; }
		}

		public override string ProviderKey
		{
			get
			{
				return LibraryProviderSelectorKey;
			}
		}

		public override void AddCollectionToLibrary(string collectionName)
		{
			throw new NotImplementedException();
		}

		public override void AddItem(PrintItemWrapper itemToAdd)
		{
			throw new NotImplementedException();
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			LibraryProvider provider = visibleProviders[collectionIndex];
			return new PrintItemCollection(provider.Name, provider.ProviderKey);
		}

		public async override Task<PrintItemWrapper> GetPrintItemWrapperAsync(int itemIndex, ReportProgressRatio reportProgress = null)
		{
			throw new NotImplementedException("Print items are not allowed at the root level");
		}

		public override LibraryProvider GetProviderForCollection(PrintItemCollection collection)
		{
			foreach (LibraryProvider libraryProvider in visibleProviders)
			{
				if (collection.Key == libraryProvider.ProviderKey)
				{
					return libraryProvider;
				}
			}

			throw new NotImplementedException();
		}

		public override void RemoveCollection(int collectionIndexToRemove)
		{
			throw new NotImplementedException();
		}

		public override void RemoveItem(int itemToRemoveIndex)
		{
			throw new NotImplementedException();
		}

		#endregion Overriden Abstract Methods
	}
}