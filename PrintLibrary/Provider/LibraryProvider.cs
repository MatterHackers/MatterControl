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

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class ProviderLocatorNode
	{
		public string Key;
		public string Name;

		public ProviderLocatorNode(string key, string name)
		{
			this.Key = key;
			this.Name = name;
		}
	}

	public abstract class LibraryProvider
	{
		public static RootedObjectEventHandler CollectionChanged = new RootedObjectEventHandler();
		public static RootedObjectEventHandler DataReloaded = new RootedObjectEventHandler();
		public static RootedObjectEventHandler ItemAdded = new RootedObjectEventHandler();
		public static RootedObjectEventHandler ItemRemoved = new RootedObjectEventHandler();

		private static LibraryProvider instance;

		public static LibraryProvider Instance
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

		#region Abstract Methods

		public abstract int CollectionCount { get; }

		public abstract bool HasParent { get; }

		public abstract int ItemCount { get; }

		public abstract string KeywordFilter { get; set; }

		public abstract string Name { get; }

		public abstract string ProviderKey { get; }

		public abstract void AddCollectionToLibrary(string collectionName);

		public abstract void AddFilesToLibrary(IList<string> files, List<ProviderLocatorNode> providerSavePath, ReportProgressRatio reportProgress = null, RunWorkerCompletedEventHandler callback = null);

		// A key,value list that threads into the current collection looks like "key0,displayName0|key1,displayName1|key2,displayName2|...|keyN,displayNameN".
		public abstract List<ProviderLocatorNode> GetProviderLocator();

		public abstract PrintItemCollection GetCollectionItem(int collectionIndex);

		public abstract PrintItemCollection GetParentCollectionItem();

		public abstract PrintItemWrapper GetPrintItemWrapper(int itemIndex);

		public abstract void RemoveCollection(string collectionName);

		public abstract void RemoveItem(PrintItemWrapper printItemWrapper);

		public abstract void SetCollectionBase(PrintItemCollection collectionBase);

		#endregion Abstract Methods

		#region Static Methods

		public static void OnDataReloaded(EventArgs eventArgs)
		{
			DataReloaded.CallEvents(Instance, eventArgs);
		}

		public static void OnItemAdded(EventArgs eventArgs)
		{
			ItemAdded.CallEvents(Instance, eventArgs);
		}

		public static void OnItemRemoved(EventArgs eventArgs)
		{
			ItemRemoved.CallEvents(Instance, eventArgs);
		}

		#endregion Static Methods
	}
}