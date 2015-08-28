﻿/*
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderFileSystemCreator : ILibraryCreator
	{
		string rootPath;
		public string Description { get; set; }

		public LibraryProviderFileSystemCreator(string rootPath, string description)
		{
			this.rootPath = rootPath;
			this.Description = description;
		}

		public string ProviderKey
		{
			get
			{
				return "FileSystem_" + rootPath + "_Key";
			}
		}

		public virtual LibraryProvider CreateLibraryProvider(LibraryProvider parentLibraryProvider, Action<LibraryProvider> setCurrentLibraryProvider)
		{
			return new LibraryProviderFileSystem(rootPath, Description, parentLibraryProvider);
		}
	}

	public class LibraryProviderFileSystem : LibraryProvider
	{
		private string currentDirectory = ".";
		private List<string> currentDirectoryDirectories = new List<string>();
		private List<string> currentDirectoryFiles = new List<string>();
		private FileSystemWatcher directoryWatcher = new FileSystemWatcher();
		private string keywordFilter = string.Empty;
		private string rootPath;

		public LibraryProviderFileSystem(string rootPath, string name, LibraryProvider parentLibraryProvider)
			: base(parentLibraryProvider)
		{
			this.Name = name;
			this.rootPath = rootPath;

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

		public override int CollectionCount
		{
			get
			{
				return currentDirectoryDirectories.Count;
			}
		}

		public override void RenameItem(int itemIndexToRename, string newName)
		{
			string sourceFile = Path.Combine(rootPath, currentDirectoryFiles[itemIndexToRename]);
			if (File.Exists(sourceFile))
			{
				string destFile = Path.Combine(Path.GetDirectoryName(sourceFile), newName);
				File.Move(sourceFile, destFile);
				Stopwatch time = Stopwatch.StartNew();
				// Wait for up to some amount of time for the directory to be gone.
				while (File.Exists(destFile)
					&& time.ElapsedMilliseconds < 100)
				{
					Thread.Sleep(1); // make sure we are not eating all the cpu time.
				}
				GetFilesAndCollectionsInCurrentDirectory();
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
					GetFilesAndCollectionsInCurrentDirectory(keywordFilter.Trim() != "");
				}
			}
		}

		public void ChangeName(string newName)
		{
			this.Name = newName;
		}

		public override string ProviderKey
		{
			get
			{
				return "FileSystem_" + rootPath + "_Key";
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

		public override void AddItem(PrintItemWrapper itemToAdd)
		{
			string destPath = rootPath;

			itemToAdd.FileLocation = CopyFile(itemToAdd.FileLocation, itemToAdd.Name, destPath);

			GetFilesAndCollectionsInCurrentDirectory();
		}

		public override void Dispose()
		{
			directoryWatcher.EnableRaisingEvents = false;

			directoryWatcher.Changed -= DiretoryContentsChanged;
			directoryWatcher.Created -= DiretoryContentsChanged;
			directoryWatcher.Deleted -= DiretoryContentsChanged;
			directoryWatcher.Renamed -= DiretoryContentsChanged;
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

			return new PrintItemWrapper(new DataStorage.PrintItem(Path.GetFileNameWithoutExtension(fileName), fileName), this);
		}

		public override LibraryProvider GetProviderForCollection(PrintItemCollection collection)
		{
			return new LibraryProviderFileSystem(Path.Combine(rootPath, collection.Key), collection.Name, this);
		}

		public override void RenameCollection(int collectionIndexToRename, string newName)
		{
			string sourceDir = Path.Combine(rootPath, currentDirectoryDirectories[collectionIndexToRename]);
			if (Directory.Exists(sourceDir))
			{
				string destDir = Path.Combine(Path.GetDirectoryName(sourceDir), newName);
				Directory.Move(sourceDir, destDir);
				Stopwatch time = Stopwatch.StartNew();
				// Wait for up to some amount of time for the directory to be gone.
				while (Directory.Exists(destDir)
					&& time.ElapsedMilliseconds < 100)
				{
					Thread.Sleep(1); // make sure we are not eating all the cpu time.
				}
				GetFilesAndCollectionsInCurrentDirectory();
			}
		}

		public override void RemoveCollection(int collectionIndexToRemove)
		{
			string directoryPath = Path.Combine(rootPath, currentDirectoryDirectories[collectionIndexToRemove]);
			if (Directory.Exists(directoryPath))
			{
				Directory.Delete(directoryPath, true);
				Stopwatch time = Stopwatch.StartNew();
				// Wait for up to some amount of time for the directory to be gone.
				while (Directory.Exists(directoryPath)
					&& time.ElapsedMilliseconds < 100)
				{
					Thread.Sleep(1); // make sure we are not eating all the cpu time.
				}
				GetFilesAndCollectionsInCurrentDirectory();
			}
		}

		public override void RemoveItem(int itemToRemoveIndex)
		{
			string filePath = currentDirectoryFiles[itemToRemoveIndex];
			File.Delete(filePath);
			GetFilesAndCollectionsInCurrentDirectory();
		}

		private static string CopyFile(string sourceFile, string destFileName, string destPath)
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
			string outputFileName = Path.Combine(destPath, destFileName);
			outputFileName = Path.ChangeExtension(outputFileName, Path.GetExtension(sourceFile));
			// and copy the file
			try
			{
				if (!File.Exists(outputFileName))
				{
					File.Copy(sourceFile, outputFileName);
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
					File.Copy(sourceFile, fileNameToUse);
				}
			}
			catch (Exception e)
			{
			}

			return outputFileName;
		}

		private void DiretoryContentsChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				GetFilesAndCollectionsInCurrentDirectory();
			});
		}

		private async void GetFilesAndCollectionsInCurrentDirectory(bool recursive = false)
		{
			List<string> newReadDirectoryDirectories = new List<string>();
			List<string> newReadDirectoryFiles = new List<string>();

			await Task.Run(() =>
			{
				try
				{
					string[] directories = null;
					if (recursive)
					{
						directories = Directory.GetDirectories(Path.Combine(rootPath, currentDirectory), "*.*", SearchOption.AllDirectories);
					}
					else
					{
						directories = Directory.GetDirectories(Path.Combine(rootPath, currentDirectory));
					}
					foreach (string directoryName in directories)
					{
						string subPath = directoryName.Substring(rootPath.Length + 1);
						newReadDirectoryDirectories.Add(subPath);
					}
				}
				catch (Exception)
				{
				}

				try
				{
					string upperFilter = keywordFilter.ToUpper();
					string[] files = Directory.GetFiles(Path.Combine(rootPath, currentDirectory));
					foreach (string filename in files)
					{
						if (ApplicationSettings.LibraryFilterFileExtensions.Contains(Path.GetExtension(filename).ToLower()))
						{
							if (upperFilter.Trim() == string.Empty
								|| Path.GetFileNameWithoutExtension(filename.ToUpper()).Contains(upperFilter))
							{
								newReadDirectoryFiles.Add(filename);
							}
						}
					}
					if (recursive)
					{
						foreach (string directory in newReadDirectoryDirectories)
						{
							string subDirectory = Path.Combine(rootPath, directory);
							string[] subDirectoryFiles = Directory.GetFiles(Path.Combine(rootPath, currentDirectory));
							foreach (string filename in subDirectoryFiles)
							{
								if (ApplicationSettings.LibraryFilterFileExtensions.Contains(Path.GetExtension(filename).ToLower()))
								{
									if (keywordFilter.Trim() == string.Empty
										|| Path.GetFileNameWithoutExtension(filename.ToUpper()).Contains(upperFilter))
									{
										newReadDirectoryFiles.Add(filename);
									}
								}
							}
						}
					}
				}
				catch (Exception)
				{
				}
			});

			if (recursive)
			{
				currentDirectoryDirectories.Clear();
			}
			else
			{
				currentDirectoryDirectories = newReadDirectoryDirectories;
			}
			currentDirectoryFiles = newReadDirectoryFiles;

			OnDataReloaded(null);
		}

		private string GetPathFromLocator(List<ProviderLocatorNode> providerLocator)
		{
			string pathWithDot = Path.Combine(rootPath, providerLocator[providerLocator.Count - 1].Key);
			string pathWithoutDot = pathWithDot.Replace("." + Path.DirectorySeparatorChar, "");
			return pathWithoutDot;
		}
	}
}