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
	/*
	public class LibraryProviderHistoryCreator : ILibraryCreator
	{
		public virtual LibraryProvider CreateLibraryProvider(LibraryProvider parentLibraryProvider, Action<LibraryProvider> setCurrentLibraryProvider)
		{
			return new LibraryProviderHistory(null, parentLibraryProvider, setCurrentLibraryProvider);
		}

		public string ProviderKey
		{
			get
			{
				return LibraryProviderHistory.StaticProviderKey;
			}
		}
	}*/

	public class LibraryProviderHistory : LibraryProvider
	{
		private static LibraryProviderHistory instance = null;

		public LibraryProviderHistory(PrintItemCollection baseLibraryCollection, LibraryProvider parentLibraryProvider, Action<LibraryProvider> setCurrentLibraryProvider)
			: base(parentLibraryProvider, setCurrentLibraryProvider)
		{
			//PrintHistoryData.Instance.ItemAdded.RegisterEvent((sender, e) => OnDataReloaded(null), ref unregisterEvent);
			this.Name = "Print History";
		}

		public static LibraryProvider Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new LibraryProviderHistory(null, null, null);
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

        public override void ShareItem(int itemIndexToShare)
        {

        }

		public static string StaticProviderKey
		{
			get
			{
				return "LibraryProviderHistoryKey";
			}
		}

		public override int CollectionCount
		{
			get
			{
				return 0;
			}
		}

        public override bool CanShare { get { return false; } }

		public override int ItemCount
		{
			get
			{
				return 10;
				//return PrintHistoryData.Instance.Count;
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
			throw new NotImplementedException();
		}

		public override Task<PrintItemWrapper> GetPrintItemWrapperAsync(int index)
		{
			throw new NotImplementedException();
			//return PrintHistoryData.Instance.GetPrintItemWrapper(index);
		}

		public override LibraryProvider GetProviderForCollection(PrintItemCollection collection)
		{
			return new LibraryProviderHistory(collection, this, SetCurrentLibraryProvider);
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
	}
}