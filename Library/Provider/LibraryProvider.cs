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
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public abstract class LibraryProvider : IDisposable
	{
		public static RootedObjectEventHandler DataReloaded = new RootedObjectEventHandler();
		private LibraryProvider parentLibraryProvider = null;

		public LibraryProvider(LibraryProvider parentLibraryProvider)
		{
			this.parentLibraryProvider = parentLibraryProvider;
		}

		public LibraryProvider ParentLibraryProvider { get { return parentLibraryProvider; } }

		#region Member Methods

		public abstract bool Visible { get; }

		public bool HasParent
		{
			get
			{
				if (this.ParentLibraryProvider != null)
				{
					return true;
				}

				return false;
			}
		}

		// A key,value list that threads into the current collection looks like "key0,displayName0|key1,displayName1|key2,displayName2|...|keyN,displayNameN".
		public List<ProviderLocatorNode> GetProviderLocator()
		{
			List<ProviderLocatorNode> providerLocator = new List<ProviderLocatorNode>();
			if (ParentLibraryProvider != null)
			{
				providerLocator.AddRange(ParentLibraryProvider.GetProviderLocator());
			}

			providerLocator.Add(new ProviderLocatorNode(ProviderKey, Name, ProviderData));

			return providerLocator;
		}

		#endregion Member Methods

		#region Abstract Methods

		public abstract void Dispose();
		public abstract int CollectionCount { get; }

		public abstract int ItemCount { get; }

		public abstract string KeywordFilter { get; set; }

		public abstract string Name { get; }

		public abstract string ProviderData { get; }

		public abstract string ProviderKey { get; }

		public abstract void AddCollectionToLibrary(string collectionName);

		public abstract void AddFilesToLibrary(IList<string> files, ReportProgressRatio reportProgress = null);

		public abstract void AddItem(PrintItemWrapper itemToAdd);

		public abstract PrintItemCollection GetCollectionItem(int collectionIndex);

		public abstract PrintItemWrapper GetPrintItemWrapper(int itemIndex);

		public abstract LibraryProvider GetProviderForItem(PrintItemCollection collection);

		public abstract void RemoveCollection(PrintItemCollection collectionToRemove);

		public abstract void RemoveItem(PrintItemWrapper printItemWrapper);

		public abstract void SaveToLibrary(PrintItemWrapper printItemWrapper, List<MeshGroup> meshGroupsToSave, List<ProviderLocatorNode> providerSavePath = null);

		#endregion Abstract Methods

		#region Static Methods

		public static void OnDataReloaded(EventArgs eventArgs)
		{
			DataReloaded.CallEvents(null, eventArgs);
		}

		#endregion Static Methods

		public virtual GuiWidget GetItemThumbnail(int printItemIndex)
		{
			PartThumbnailWidget thumbnailWidget = new PartThumbnailWidget(GetPrintItemWrapper(printItemIndex), "part_icon_transparent_40x40.png", "building_thumbnail_40x40.png", PartThumbnailWidget.ImageSizes.Size50x50);
			return thumbnailWidget;
		}
	}

	public class ProviderLocatorNode
	{
		public string Key;
		public string Name;
		public string ProviderData;

		public ProviderLocatorNode(string key, string name, string providerData)
		{
			this.Key = key;
			this.Name = name;
			this.ProviderData = providerData;
		}
	}
}