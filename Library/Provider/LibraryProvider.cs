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
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public abstract class LibraryProvider : IDisposable
	{
		protected Action<LibraryProvider> SetCurrentLibraryProvider { get; private set; }

		protected Dictionary<int, ProgressPlug> itemReportProgressHandlers = new Dictionary<int, ProgressPlug>();

		private LibraryProvider parentLibraryProvider = null;

		public LibraryProvider(LibraryProvider parentLibraryProvider, Action<LibraryProvider> setCurrentLibraryProvider)
		{
			this.SetCurrentLibraryProvider = setCurrentLibraryProvider;
			this.parentLibraryProvider = parentLibraryProvider;
		}

		public event EventHandler DataReloaded;

		public LibraryProvider ParentLibraryProvider { get { return parentLibraryProvider; } }

		#region Member Methods

		private static ImageBuffer normalFolderImage = null;

		private static ImageBuffer upFolderImage = null;

		public static ImageBuffer NormalFolderImage
		{
			get
			{
				if (normalFolderImage == null)
				{
					string path = Path.Combine("FileDialog", "folder.png");

					normalFolderImage = StaticData.Instance.LoadIcon(path).InvertLightness();
				}

				return normalFolderImage;
			}
		}

		public static ImageBuffer UpFolderImage
		{
			get
			{
				if (upFolderImage == null)
				{
					string path = Path.Combine("FileDialog", "up_folder.png");

					upFolderImage = StaticData.Instance.LoadIcon(path).InvertLightness();
				}

				return upFolderImage;
			}
		}

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

		public void AddFilesToLibrary(IList<string> files, ReportProgressRatio reportProgress = null)
		{
			foreach (string loadedFileName in files)
			{
				string extension = Path.GetExtension(loadedFileName).ToUpper();
				if ((extension != "" && MeshFileIo.ValidFileExtensions().Contains(extension))
					|| extension == ".GCODE"
					|| extension == ".ZIP")
				{
					if (extension == ".ZIP")
					{
						List<PrintItem> partFiles = ProjectFileHandler.ImportFromProjectArchive(loadedFileName);
						if (partFiles != null)
						{
							foreach (PrintItem part in partFiles)
							{
								AddItem(new PrintItemWrapper(part, this.GetProviderLocator()));
							}
						}
					}
					else
					{
						AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(loadedFileName), loadedFileName), this.GetProviderLocator()));
					}
				}
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

			providerLocator.Add(new ProviderLocatorNode(ProviderKey, Name));

			return providerLocator;
		}

		#endregion Member Methods

		#region Abstract Methods

		public abstract int CollectionCount { get; }

		public abstract int ItemCount { get; }

        public abstract bool CanShare { get; }

		public abstract string ProviderKey { get; }

		public abstract void AddCollectionToLibrary(string collectionName);

		public abstract void AddItem(PrintItemWrapper itemToAdd);

		public abstract PrintItemCollection GetCollectionItem(int collectionIndex);

		public abstract Task<PrintItemWrapper> GetPrintItemWrapperAsync(int itemIndex);

		// TODO: make this asnyc
		//public abstract Task<LibraryProvider> GetProviderForCollectionAsync(PrintItemCollection collection);
		public abstract LibraryProvider GetProviderForCollection(PrintItemCollection collection);

		public abstract void RemoveCollection(int collectionIndexToRemove);

		public abstract void RemoveItem(int itemIndexToRemove);

		// Base implementation simply calls RemoveItem
		public virtual void RemoveItems(int[] indexes)
		{
			// Remove items in reverse order
			foreach (var i in indexes.OrderByDescending(i => i))
			{
				RemoveItem(i);
			}
		}

		// Base implementation does not do moving.
		public virtual void MoveItems(int[] indexes)
		{
		}

		public abstract void RenameCollection(int collectionIndexToRename, string newName);

		public abstract void RenameItem(int itemIndexToRename, string newName);
        public abstract void ShareItem(int itemIndexToShare);

		#endregion Abstract Methods

		#region Static Methods

		public void OnDataReloaded(EventArgs eventArgs)
		{
			DataReloaded?.Invoke(this, eventArgs);
		}

		#endregion Static Methods

		public virtual string KeywordFilter { get; set; }

		public virtual string StatusMessage
		{
			get { return ""; }
		}

		public virtual void Dispose()
		{
		}

		public virtual int GetCollectionChildCollectionCount(int collectionIndex)
		{
			return GetProviderForCollection(GetCollectionItem(collectionIndex)).CollectionCount;
		}

		public virtual ImageBuffer GetCollectionFolderImage(int collectionIndex)
		{
			return NormalFolderImage;
		}

		public virtual int GetCollectionItemCount(int collectionIndex)
		{
			return GetProviderForCollection(GetCollectionItem(collectionIndex)).ItemCount;
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

		public LibraryProvider GetRootProvider()
		{
			LibraryProvider parent = this;
			while (parent != null
				&& parent.ParentLibraryProvider != null)
			{
				parent = parent.ParentLibraryProvider;
			}

			return parent;
		}

		public string Name { get; protected set; }

		public virtual bool IsItemProtected(int itemIndex)
		{
			return false;
		}

		public virtual bool IsItemReadOnly(int itemIndex)
		{
			return false;
		}

		public void RegisterForProgress(int itemIndex, ReportProgressRatio reportProgress)
		{
			if (!itemReportProgressHandlers.ContainsKey(itemIndex))
			{
				itemReportProgressHandlers.Add(itemIndex, new ProgressPlug()
				{
					ProgressOutput = reportProgress,
				});
			}
			else
			{
				itemReportProgressHandlers[itemIndex].ProgressOutput = reportProgress;
			}
		}

		protected ProgressPlug GetItemProgressPlug(int itemIndex)
		{
			if (!itemReportProgressHandlers.ContainsKey(itemIndex))
			{
				itemReportProgressHandlers.Add(itemIndex, new ProgressPlug());
			}

			return itemReportProgressHandlers[itemIndex];
		}

		public class ProgressPlug
		{
			public ReportProgressRatio ProgressOutput;

			public void ProgressInput(double progress0To1, string processingState, out bool continueProcessing)
			{
				continueProcessing = true;
				if (ProgressOutput != null)
				{
					ProgressOutput(progress0To1, processingState, out continueProcessing);
				}
			}
		}

		public virtual bool IsProtected()
		{
			return false;
		}
	}

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
}