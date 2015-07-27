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
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public abstract class LibraryProvider : IDisposable
	{
		public event EventHandler DataReloaded;
		private LibraryProvider parentLibraryProvider = null;

		public LibraryProvider(LibraryProvider parentLibraryProvider)
		{
			this.parentLibraryProvider = parentLibraryProvider;
		}

		public LibraryProvider ParentLibraryProvider { get { return parentLibraryProvider; } }

		#region Member Methods

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

		public abstract bool Visible { get; }

		static ImageBuffer normalFolderImage = null;
		public static ImageBuffer NormalFolderImage
		{
			get
			{
				if (normalFolderImage == null)
				{
					string path = Path.Combine("Icons", "FileDialog", "folder.png");

					normalFolderImage = new ImageBuffer();
					StaticData.Instance.LoadImage(path, normalFolderImage);
				}

				return normalFolderImage;
			}
		}

		static ImageBuffer upFolderImage = null;
		public static ImageBuffer UpFolderImage
		{
			get
			{
				if (upFolderImage == null)
				{
					string path = Path.Combine("Icons", "FileDialog", "up_folder.png");

					upFolderImage = new ImageBuffer();
					StaticData.Instance.LoadImage(path, upFolderImage);
				}

				return upFolderImage;
			}
		}

		public void AddFilesToLibrary(IList<string> files, ReportProgressRatio reportProgress = null)
		{
			foreach (string file in files)
			{
				AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(file), file), this));
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

		public abstract int CollectionCount { get; }

		public abstract int ItemCount { get; }

		public abstract string KeywordFilter { get; set; }

		public abstract string Name { get; }

		public abstract string ProviderData { get; }

		public abstract string ProviderKey { get; }

		public abstract void AddCollectionToLibrary(string collectionName);

		public abstract void AddItem(PrintItemWrapper itemToAdd);

		public abstract void Dispose();

		public abstract PrintItemCollection GetCollectionItem(int collectionIndex);

		public abstract Task<PrintItemWrapper> GetPrintItemWrapperAsync(int itemIndex, ReportProgressRatio reportProgress = null);

		// TODO: make this asnyc
		//public abstract Task<LibraryProvider> GetProviderForCollectionAsync(PrintItemCollection collection);
		public abstract LibraryProvider GetProviderForCollection(PrintItemCollection collection);

		public abstract void RemoveCollection(int collectionIndexToRemove);

		public abstract void RemoveItem(int itemIndexToRemove);

		#endregion Abstract Methods

		#region Static Methods

		public void OnDataReloaded(EventArgs eventArgs)
		{
			if (DataReloaded != null)
			{
				DataReloaded(null, eventArgs);
			}
		}

		#endregion Static Methods

		public virtual int GetCollectionChildCollectionCount(int collectionIndex)
		{
			return GetProviderForCollection(GetCollectionItem(collectionIndex)).CollectionCount;
		}

		public virtual int GetCollectionItemCount(int collectionIndex)
		{
			return GetProviderForCollection(GetCollectionItem(collectionIndex)).ItemCount;
		}

		public virtual ImageBuffer GetCollectionFolderImage(int collectionIndex)
		{
			return NormalFolderImage;
		}

		public virtual GuiWidget GetItemThumbnail(int printItemIndex)
		{
			var printItemWrapper = GetPrintItemWrapperAsync(printItemIndex).Result;
			return new PartThumbnailWidget(printItemWrapper, "part_icon_transparent_40x40.png", "building_thumbnail_40x40.png", PartThumbnailWidget.ImageSizes.Size50x50);
		}

		public virtual string GetPrintItemName(int itemIndex)
		{
			return "";
		}

		public virtual bool IsItemProtected(int itemIndex)
		{
			return false;
		}

		public virtual bool IsItemReadOnly(int itemIndex)
		{
			return false;
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