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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
		private FileSystemWatcher directoryWatcher = new FileSystemWatcher();
		private string keywordFilter = string.Empty;
		private string rootPath;

		public LibraryProviderFileSystem(string rootPath, string description, LibraryProvider parentLibraryProvider)
			: base(parentLibraryProvider)
		{
			this.description = description;
			this.rootPath = rootPath;

			key = keyCount.ToString();
			keyCount++;

			directoryWatcher.Path = rootPath;

			directoryWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
				   | NotifyFilters.FileName | NotifyFilters.DirectoryName;
			directoryWatcher.Changed += DiretoryContentsChanged;
			directoryWatcher.Created += DiretoryContentsChanged;
			directoryWatcher.Deleted += DiretoryContentsChanged;
			directoryWatcher.Renamed += DiretoryContentsChanged;

			// Begin watching.
			directoryWatcher.EnableRaisingEvents = true;

			GetFilesAndCollectionsInCurrentDirectory();
		}

		public override bool Visible
		{
			get { return true; }
		}

		public override void Dispose()
		{
			directoryWatcher.EnableRaisingEvents = false;

			directoryWatcher.Changed -= DiretoryContentsChanged;
			directoryWatcher.Created -= DiretoryContentsChanged;
			directoryWatcher.Deleted -= DiretoryContentsChanged;
			directoryWatcher.Renamed -= DiretoryContentsChanged;
		}

		public override int CollectionCount
		{
			get
			{
				return currentDirectoryDirectories.Count;
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
					GetFilesAndCollectionsInCurrentDirectory();
				}
			}
		}

		public override string Name { get { return description; } }

		public override string ProviderData
		{
			get { return rootPath; }
		}

		public override string ProviderKey
		{
			get
			{
				return "FileSystem_" + key.ToString() + "_Key";
			}
		}

		public override void AddCollectionToLibrary(string collectionName)
		{
			string directoryPath = Path.Combine(rootPath, currentDirectory, collectionName);
			if (!Directory.Exists(directoryPath))
			{
				Directory.CreateDirectory(directoryPath);
				GetFilesAndCollectionsInCurrentDirectory();
			}
		}

		public override void AddFilesToLibrary(IList<string> files, ReportProgressRatio reportProgress = null)
		{
			string destPath = rootPath;

			CopyAllFiles(files, destPath);

			GetFilesAndCollectionsInCurrentDirectory();
		}

		public override void AddItem(PrintItemWrapper itemToAdd)
		{
			throw new NotImplementedException();
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			string directoryName = currentDirectoryDirectories[collectionIndex];
			return new PrintItemCollection(Path.GetFileNameWithoutExtension(directoryName), Path.Combine(rootPath, directoryName));
		}

		public override string GetPrintItemName(int itemIndex)
		{
			return Path.GetFileName(currentDirectoryFiles[itemIndex]);
		}

		public async override Task<PrintItemWrapper> GetPrintItemWrapperAsync(int itemIndex)
		{
			string fileName = currentDirectoryFiles[itemIndex];
			
			List<ProviderLocatorNode> providerLocator = GetProviderLocator();
			string providerLocatorJson = JsonConvert.SerializeObject(providerLocator);
			
			return new PrintItemWrapper(new DataStorage.PrintItem(Path.GetFileNameWithoutExtension(fileName), fileName, providerLocatorJson));
		}

		public override LibraryProvider GetProviderForItem(PrintItemCollection collection)
		{
			return new LibraryProviderFileSystem(Path.Combine(rootPath, collection.Key), collection.Name, this);
		}

		public override void RemoveCollection(PrintItemCollection collectionToRemove)
		{
			string directoryPath = collectionToRemove.Key;
			if (Directory.Exists(directoryPath))
			{
				Stopwatch time = Stopwatch.StartNew();
				Directory.Delete(directoryPath, true);
				// Wait for up to some amount of time for the directory to be gone.
				while (Directory.Exists(directoryPath) 
					&& time.ElapsedMilliseconds < 100)
				{
					Thread.Sleep(1); // make sure we are not eating all the cpu time.
				}
				GetFilesAndCollectionsInCurrentDirectory();
			}
		}

		public override void RemoveItem(PrintItemWrapper printItemWrapper)
		{
			File.Delete(printItemWrapper.PrintItem.FileLocation);
			GetFilesAndCollectionsInCurrentDirectory();
		}

		public override void SaveToLibrary(PrintItemWrapper printItemWrapper, List<MeshGroup> meshGroupsToSave, List<ProviderLocatorNode> providerSavePath)
		{
			throw new NotImplementedException();
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
					if (!File.Exists(outputFileName))
					{
						File.Copy(file, outputFileName);
					}
					else // make a new file and append a number so that we are not destructive
					{
						string directory = Path.GetDirectoryName(outputFileName);
						string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputFileName);
						string extension = Path.GetExtension(outputFileName);
						// get the filename without a number on the end
						int lastSpaceIndex = fileNameWithoutExtension.LastIndexOf(' ');
						if (lastSpaceIndex != -1)
						{
							int endingNumber;
							// check if the last set of characters is a number
							if (int.TryParse(fileNameWithoutExtension.Substring(lastSpaceIndex), out endingNumber))
							{
								fileNameWithoutExtension = fileNameWithoutExtension.Substring(0, lastSpaceIndex);
							}
						}
						int numberToAppend = 2;
						string fileNameToUse = Path.Combine(directory, fileNameWithoutExtension + " " + numberToAppend.ToString() + extension);
						while (File.Exists(fileNameToUse))
						{
							numberToAppend++;
							fileNameToUse = Path.Combine(directory, fileNameWithoutExtension + " " + numberToAppend.ToString() + extension);
						}
						File.Copy(file, fileNameToUse);
					}
				}
				catch (Exception e)
				{
				}
			}
		}

		private void DiretoryContentsChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				GetFilesAndCollectionsInCurrentDirectory();
			});
		}

		private void GetFilesAndCollectionsInCurrentDirectory()
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

			LibraryProvider.OnDataReloaded(null);
		}

		private string GetPathFromLocator(List<ProviderLocatorNode> providerLocator)
		{
			string pathWithDot = Path.Combine(rootPath, providerLocator[providerLocator.Count - 1].Key);
			string pathWithoutDot = pathWithDot.Replace("." + Path.DirectorySeparatorChar, "");
			return pathWithoutDot;
		}
	}
}