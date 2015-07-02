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

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderSelector : LibraryProvider
	{
		private static LibraryProviderSelector instance = null;
		private List<LibraryProvider> libraryProviders = new List<LibraryProvider>();

		private LibraryProviderSelector()
			: base(null)
		{
			// put in the sqlite provider
			libraryProviders.Add(new LibraryProviderSQLite(null, this));

			// and any directory providers (sd card provider, etc...)
			//
			// Add "Downloads" file system example
			string downloadsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			if (Directory.Exists(downloadsDirectory))
			{
				libraryProviders.Add(new LibraryProviderFileSystem(downloadsDirectory, "Downloads", this));
			}

			//#if __ANDROID__
			//libraryProviders.Add(new LibraryProviderFileSystem(ApplicationDataStorage.Instance.PublicDataStoragePath, "Downloads", this.ProviderKey));

			// Check for LibraryProvider factories and put them in the list too.
			PluginFinder<LibraryProviderPlugin> libraryProviderPlugins = new PluginFinder<LibraryProviderPlugin>();
			foreach (LibraryProviderPlugin libraryProviderPlugin in libraryProviderPlugins.Plugins)
			{
				libraryProviders.Add(libraryProviderPlugin.CreateLibraryProvider(this));
			}

			providerLocationStack.Add(new PrintItemCollection("..", ProviderKey));
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

		private List<PrintItemCollection> providerLocationStack = new List<PrintItemCollection>();

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
				return libraryProviders.Count;
			}
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

		public override void AddFilesToLibrary(IList<string> files, ReportProgressRatio reportProgress = null, RunWorkerCompletedEventHandler callback = null)
		{
			throw new NotImplementedException();
		}

		public override void AddItem(PrintItemWrapper itemToAdd)
		{
			throw new NotImplementedException();
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			LibraryProvider provider = libraryProviders[collectionIndex];
			return new PrintItemCollection(provider.Name, provider.ProviderKey);
		}

		public override PrintItemWrapper GetPrintItemWrapper(int itemIndex)
		{
			if (libraryProviders[0].ProviderKey != LibraryProviderSQLite.StaticProviderKey)
			{
				throw new Exception("It is expected these are the same.");
			}
			return libraryProviders[0].GetPrintItemWrapper(itemIndex);
		}

		public override LibraryProvider GetProviderForItem(PrintItemCollection collection)
		{
			foreach (LibraryProvider libraryProvider in libraryProviders)
			{
				if (collection.Key == libraryProvider.ProviderKey)
				{
					return libraryProvider;
				}
			}

			throw new NotImplementedException();
		}

		public override void RemoveCollection(PrintItemCollection collectionToRemove)
		{
			throw new NotImplementedException();
		}

		public override void RemoveItem(PrintItemWrapper printItemWrapper)
		{
			List<ProviderLocatorNode> subProviderSavePath;
			int libraryProviderToUseIndex = GetProviderIndex(printItemWrapper, out subProviderSavePath);

			libraryProviders[libraryProviderToUseIndex].RemoveItem(printItemWrapper);
		}

		public override void SaveToLibrary(PrintItemWrapper printItemWrapper, List<MeshGroup> meshGroupsToSave, List<ProviderLocatorNode> providerSavePath = null)
		{
			throw new NotImplementedException();
		}

		private int GetProviderIndex(PrintItemWrapper printItemWrapper, out List<ProviderLocatorNode> subProviderSavePath)
		{
			List<ProviderLocatorNode> providerPath = printItemWrapper.PrintItem.GetLibraryProviderLocator();

			return GetProviderIndex(providerPath, out subProviderSavePath);
		}

		private int GetProviderIndex(List<ProviderLocatorNode> providerSavePath, out List<ProviderLocatorNode> subProviderSavePath)
		{
			subProviderSavePath = null;

			if (providerSavePath != null
				&& providerSavePath.Count > 1) // key 0 is this provider so we want to look at the next provider
			{
				for (int i = 0; i < libraryProviders.Count; i++)
				{
					if (libraryProviders[i].ProviderKey == providerSavePath[1].Key)
					{
						subProviderSavePath = new List<ProviderLocatorNode>(providerSavePath);
						subProviderSavePath.RemoveAt(0);
						return i;
					}
				}
			}

			return 0;
		}

		#endregion Overriden Abstract Methods

		public static LibraryProvider GetProviderForItem(PrintItemWrapper printItemWrapper)
		{
			throw new NotImplementedException();
		}
	}
}