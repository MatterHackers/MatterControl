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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderSelector : LibraryProvider
	{
		private List<LibraryProvider> libraryProviders = new List<LibraryProvider>();
		int selectedLibraryProvider = -1;

		public LibraryProviderSelector()
		{
			// put in the sqlite provider
			LibraryProviderSQLite localStore = new LibraryProviderSQLite(Key);
			libraryProviders.Add(localStore);

			// and any directory providers (sd card provider, etc...)
			PrintItemCollection collectionBase = new PrintItemCollection("Downloads", Path.Combine("C:\\", "Users", "LarsBrubaker", "Downloads"));
			libraryProviders.Add(new LibraryProviderFileSystem(collectionBase, "Downloads", Key));

			// Check for LibraryProvider factories and put them in the list too.
		}

		#region Overriden Abstract Methods

		public override int CollectionCount
		{
			get
			{
				if (selectedLibraryProvider == -1)
				{
					return libraryProviders.Count;
				}
				else
				{
					return libraryProviders[selectedLibraryProvider].CollectionCount;
				}
			}
		}

		public override int ItemCount
		{
			get
			{
				if (selectedLibraryProvider == -1)
				{
					return 0;
				}
				else
				{
					return libraryProviders[selectedLibraryProvider].ItemCount;
				}
			}
		}

		public override string KeywordFilter
		{
			get
			{
				if (selectedLibraryProvider == -1)
				{
					return "";
				}
				else
				{
					return libraryProviders[selectedLibraryProvider].KeywordFilter;
				}
			}

			set
			{
				if (selectedLibraryProvider == -1)
				{
				}
				else
				{
					libraryProviders[selectedLibraryProvider].KeywordFilter = value;
				}
			}
		}

		public override void AddCollectionToLibrary(string collectionName)
		{
			if (selectedLibraryProvider == -1)
			{
				throw new NotImplementedException();
			}
			else
			{
				libraryProviders[selectedLibraryProvider].AddCollectionToLibrary(collectionName);
			}
		}

		public override void AddFilesToLibrary(IList<string> files, ReportProgressRatio reportProgress = null, RunWorkerCompletedEventHandler callback = null)
		{
			if (selectedLibraryProvider == -1)
			{
				throw new NotImplementedException();
			}
			else
			{
				libraryProviders[selectedLibraryProvider].AddFilesToLibrary(files, reportProgress, callback);
			}
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			if (selectedLibraryProvider == -1)
			{
				LibraryProvider provider = libraryProviders[collectionIndex];
				return new PrintItemCollection(provider.Name, provider.Key);
			}
			else
			{
				return libraryProviders[selectedLibraryProvider].GetCollectionItem(collectionIndex);
			}
		}

		public override string Key
		{
			get 
			{
				return "LibraryProviderSelector";
			}
		}

		public override string Name 
		{ 
			get 
			{
				return "Never visible"; 
			} 
		}

		public override PrintItemCollection GetParentCollectionItem()
		{
			if (selectedLibraryProvider == -1)
			{
				return null;
			}
			else
			{
				return libraryProviders[selectedLibraryProvider].GetParentCollectionItem();
			}
		}

		public override PrintItemWrapper GetPrintItemWrapper(int itemIndex)
		{
			if (selectedLibraryProvider == -1)
			{
				throw new NotImplementedException();
			}
			else
			{
				return libraryProviders[selectedLibraryProvider].GetPrintItemWrapper(itemIndex);
			}
		}

		public override bool HasParent 
		{
			get 
			{
				if (selectedLibraryProvider == -1)
				{
					return false;
				}
				else
				{
					return libraryProviders[selectedLibraryProvider].HasParent;
				}
			} 
		}

		public override void RemoveCollection(string collectionName)
		{
			if (selectedLibraryProvider == -1)
			{
				throw new NotImplementedException();
			}
			else
			{
				libraryProviders[selectedLibraryProvider].RemoveCollection(collectionName);
			}
		}

		public override void RemoveItem(PrintItemWrapper printItemWrapper)
		{
			if (selectedLibraryProvider == -1)
			{
				throw new NotImplementedException();
			}
			else
			{
				libraryProviders[selectedLibraryProvider].RemoveItem(printItemWrapper);
			}
		}

		public override void SetCollectionBase(PrintItemCollection collectionBase)
		{
			if (collectionBase.Key == Key)
			{
				selectedLibraryProvider = -1;
				return;
			}

			bool wasSet = false;
			for (int i = 0; i < libraryProviders.Count; i++)
			{
				if (libraryProviders[i].Key == collectionBase.Key)
				{
					selectedLibraryProvider = i;
					wasSet = true;
					break;
				}
			}

			if (!wasSet)
			{
				libraryProviders[selectedLibraryProvider].SetCollectionBase(collectionBase);
			}
		}

		#endregion Overriden Abstract Methods
	}
}