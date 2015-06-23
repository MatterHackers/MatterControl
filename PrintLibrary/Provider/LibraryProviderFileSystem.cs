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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderFileSystem : LibraryProvider
	{
		public string key;
		private static int keyCount = 0;
		private string currentDirectory = ".";
		private List<string> currentDirectoryDirectories = new List<string>();
		private List<string> currentDirectoryFiles = new List<string>();
		private string description;
		private string keywordFilter = string.Empty;
		private string parentKey = null;
		private string rootPath;

		public LibraryProviderFileSystem(string rootPath, string description, string parentKeyKey)
		{
			this.parentKey = parentKeyKey;
			this.description = description;
			this.rootPath = rootPath;

			key = keyCount.ToString();
			keyCount++;
			GetFilesInCurrentDirectory();
		}

		public override int CollectionCount
		{
			get
			{
				return currentDirectoryDirectories.Count;
			}
		}

		public override bool HasParent
		{
			get
			{
				if (parentKey != null)
				{
					return true;
				}

				return false;
			}
		}

		public override int ItemCount
		{
			get
			{
				return currentDirectoryFiles.Count;
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
				if (keywordFilter != value)
				{
					keywordFilter = value;
					GetFilesInCurrentDirectory();
					LibraryProvider.OnDataReloaded(null);
				}
			}
		}

		public override string Name { get { return description; } }

		public override string ProviderKey
		{
			get
			{
				return "FileSystem_" + key.ToString() + "_Key";
			}
		}

		public override void AddCollectionToLibrary(string collectionName)
		{
			throw new NotImplementedException();
		}

		public override void AddFilesToLibrary(IList<string> files, List<ProviderLocatorNode> providerLocator, ReportProgressRatio reportProgress = null, RunWorkerCompletedEventHandler callback = null)
		{
			if (providerLocator == null || providerLocator.Count <= 1)
			{
				string destPath = rootPath;

				CopyAllFiles(files, destPath);
			}
			else // we have a path that we need to save to
			{
				string destPath = GetPathFromLocator(providerLocator);

				CopyAllFiles(files, destPath);
			}

			GetFilesInCurrentDirectory();
			LibraryProvider.OnDataReloaded(null);
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			string directoryName = currentDirectoryDirectories[collectionIndex];
			return new PrintItemCollection(Path.GetFileNameWithoutExtension(directoryName), directoryName);
		}

		public override PrintItemCollection GetParentCollectionItem()
		{
			if (currentDirectory == ".")
			{
				if (parentKey != null)
				{
					return new PrintItemCollection("..", parentKey);
				}
				else
				{
					return null;
				}
			}
			else
			{
				string parentDirectory = Path.GetDirectoryName(currentDirectory);
				return new PrintItemCollection("..", parentDirectory);
			}
		}

		public override PrintItemWrapper GetPrintItemWrapper(int itemIndex)
		{
			string fileName = currentDirectoryFiles[itemIndex];
			List<ProviderLocatorNode> providerLocator = LibraryProvider.Instance.GetProviderLocator();
			string providerLocatorJson = JsonConvert.SerializeObject(providerLocator);
			return new PrintItemWrapper(new DataStorage.PrintItem(Path.GetFileNameWithoutExtension(fileName), fileName, providerLocatorJson));
		}

		public override List<ProviderLocatorNode> GetProviderLocator()
		{
			throw new NotImplementedException();
		}

		public override void RemoveCollection(string collectionName)
		{
			throw new NotImplementedException();
		}

		public override void RemoveItem(PrintItemWrapper printItemWrapper)
		{
			File.Delete(printItemWrapper.PrintItem.FileLocation);
			GetFilesInCurrentDirectory();
			LibraryProvider.OnDataReloaded(null);
		}

		public override void SaveToLibrary(PrintItemWrapper printItemWrapper, List<MeshGroup> meshGroupsToSave, List<ProviderLocatorNode> providerSavePath)
		{
			throw new NotImplementedException();
		}

		public override void SetCollectionBase(PrintItemCollection collectionBase)
		{
			string collectionPath = collectionBase.Key;
			int startOfCurrentDir = collectionPath.IndexOf('.');
			if (startOfCurrentDir != -1)
			{
				this.currentDirectory = collectionPath.Substring(startOfCurrentDir);
			}

			GetFilesInCurrentDirectory();
		}

		private static void CopyAllFiles(IList<string> files, string destPath)
		{
			// make sure the directory exists
			try
			{
				Directory.CreateDirectory(destPath);
			}
			catch (Exception e)
			{
			}

			// save it to the root directory
			foreach (string file in files)
			{
				string outputFileName = Path.Combine(destPath, Path.GetFileName(file));
				// and copy the file
				try
				{
					File.Copy(file, outputFileName);
				}
				catch (Exception e)
				{
				}
			}
		}

		private void GetFilesInCurrentDirectory()
		{
			currentDirectoryDirectories.Clear();
			string[] directories = Directory.GetDirectories(Path.Combine(rootPath, currentDirectory));
			foreach (string directoryName in directories)
			{
				if (keywordFilter.Trim() == string.Empty
					|| Path.GetFileNameWithoutExtension(directoryName).Contains(keywordFilter))
				{
					string subPath = directoryName.Substring(rootPath.Length + 1);
					currentDirectoryDirectories.Add(subPath);
				}
			}

			currentDirectoryFiles.Clear();
			string[] files = Directory.GetFiles(Path.Combine(rootPath, currentDirectory));
			foreach (string filename in files)
			{
				if (ApplicationSettings.LibraryFilterFileExtensions.Contains(Path.GetExtension(filename).ToLower()))
				{
					if (keywordFilter.Trim() == string.Empty
						|| Path.GetFileNameWithoutExtension(filename).Contains(keywordFilter))
					{
						currentDirectoryFiles.Add(filename);
					}
				}
			}
		}

		private string GetPathFromLocator(List<ProviderLocatorNode> providerLocator)
		{
			string pathWithDot = Path.Combine(rootPath, providerLocator[providerLocator.Count - 1].Key);
			string pathWithoutDot = pathWithDot.Replace("." + Path.DirectorySeparatorChar, "");
			return pathWithoutDot;
		}
	}
}