/*
Copyright (c) 2017, John Lewin
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.Library
{
	public class FileSystemContainer : WritableContainer
	{
		private FileSystemWatcher directoryWatcher;

		private bool isActiveContainer;
		private bool isDirty;

		public FileSystemContainer(string fullPath)
		{
			this.FullPath = fullPath;
			this.Name = Path.GetFileName(fullPath);

			this.IsProtected = false;

			this.ChildContainers = new List<ILibraryContainerLink>();
			this.Items = new List<ILibraryItem>();
#if !__ANDROID__
			if (AggContext.OperatingSystem == OSType.Windows
				&& Directory.Exists(fullPath))
			{
				directoryWatcher = new FileSystemWatcher(fullPath);
				directoryWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
				directoryWatcher.Changed += DirectoryContentsChanged;
				directoryWatcher.Created += DirectoryContentsChanged;
				directoryWatcher.Deleted += DirectoryContentsChanged;
				directoryWatcher.Renamed += DirectoryContentsChanged;
				directoryWatcher.IncludeSubdirectories = false;

				// Begin watching.
				directoryWatcher.EnableRaisingEvents = true;
			}
#endif
		}

		public string FullPath { get; protected set; }

		// Indicates if the new AMF file should use the original file name incremented until no name collision occurs
		public bool UseIncrementedNameDuringTypeChange { get; internal set; }

		public override void Activate()
		{
			this.isActiveContainer = true;
			base.Activate();
		}

		public override void Deactivate()
		{
			this.isActiveContainer = false;
			base.Deactivate();
		}

		private string keywordFilter = "";
		public override string KeywordFilter
		{
			get => keywordFilter;
			set
			{
				if (keywordFilter != value)
				{
					keywordFilter = value;
					this.ReloadContent();
				}
			}
		}

		public override void Dispose()
		{
			if (directoryWatcher != null)
			{
				directoryWatcher.EnableRaisingEvents = false;

				directoryWatcher.Changed -= DirectoryContentsChanged;
				directoryWatcher.Created -= DirectoryContentsChanged;
				directoryWatcher.Deleted -= DirectoryContentsChanged;
				directoryWatcher.Renamed -= DirectoryContentsChanged;
			}
		}

		long lastTimeContentsChanged;

		private RunningInterval waitingForRefresh;

		private void DirectoryContentsChanged(object sender, EventArgs e)
		{
			// Flag for reload
			isDirty = true;

			lastTimeContentsChanged = UiThread.CurrentTimerMs;

			// Only refresh content if we're the active container
			if (isActiveContainer
				&& waitingForRefresh == null)
			{
				waitingForRefresh = UiThread.SetInterval(WaitToRefresh, .5);
			}
		}

		private void WaitToRefresh()
		{
			if (UiThread.CurrentTimerMs > lastTimeContentsChanged + 500
				&& waitingForRefresh != null)
			{
				UiThread.ClearInterval(waitingForRefresh);

				waitingForRefresh = null;
				this.ReloadContent();
			}
		}

		public override void Load()
		{
			this.Load(false);
		}

		public void Load(bool recursive)
		{
			SearchOption searchDepth = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

			try
			{
				string filter = this.KeywordFilter.Trim();

				var allFiles = Directory.GetFiles(FullPath, "*.*", searchDepth);

				var zipFiles = allFiles.Where(f => Path.GetExtension(f).IndexOf(".zip", StringComparison.OrdinalIgnoreCase) != -1);

				var nonZipFiles = allFiles.Except(zipFiles);

				List<ILibraryContainerLink> containers;
				if (filter == "")
				{
					var directories = Directory.GetDirectories(FullPath, "*.*", searchDepth).Select(p => new DirectoryContainerLink(p)).ToList<ILibraryContainerLink>();
					containers = directories.Concat(zipFiles.Select(f => new LocalZipContainerLink(f))).OrderBy(d => d.Name).ToList();
				}
				else
				{
					containers = new List<ILibraryContainerLink>();
				}

				var matchedFiles = nonZipFiles.Where(filePath =>
				{
					string fileName = Path.GetFileName(filePath);

					return (filter == "" || FileNameContainsFilter(filePath, filter))
						&& ApplicationController.Instance.Library.IsContentFileType(fileName);
				});

				// Matched containers
				this.ChildContainers = containers;

				// Matched files projected onto FileSystemFileItem
				this.Items = matchedFiles.OrderBy(f => f).Select(f => new FileSystemFileItem(f)).ToList<ILibraryItem>();

				this.isDirty = false;
			}
			catch (Exception ex)
			{
				this.ChildContainers = new List<ILibraryContainerLink>();
				this.Items = new List<ILibraryItem>()
				{
					new MessageItem("Error loading container - " + ex.Message)
				};
			}
		}

		private bool FileNameContainsFilter(string filename, string filter)
		{
			string nameWithSpaces = Path.GetFileNameWithoutExtension(filename.Replace('_', ' '));

			// Split the filter on word boundaries and determine if all terms in the given file name
			foreach (string word in filter.Split(' '))
			{
				if (nameWithSpaces.IndexOf(word, StringComparison.OrdinalIgnoreCase) == -1)
				{
					return false;
				}
			}

			return true;
		}

		private string GetNonCollidingName(string fileName)
		{
			// Switching from .stl, .obj or similar to AMF. Save the file and update the
			// the filename with an incremented (n) value to reflect the extension change in the UI
			var similarFileNames = Directory.GetFiles(this.FullPath, $"{Path.GetFileNameWithoutExtension(fileName)}.*");

			// ;
			var validName = agg_basics.GetNonCollidingName(fileName, (testName) => !File.Exists(testName));

			return validName;
		}

		public async override void Add(IEnumerable<ILibraryItem> items)
		{
			if (!items.Any())
			{
				return;
			}

			directoryWatcher.EnableRaisingEvents = false;

			Directory.CreateDirectory(this.FullPath);

			await Task.Run(async () =>
			{
				foreach (var item in items)
				{
					switch (item)
					{
						case CreateFolderItem newFolder:
							string targetFolderPath = Path.Combine(this.FullPath, newFolder.Name);

							// TODO: write adaption of GetNonCollidingName for directories
							Directory.CreateDirectory(targetFolderPath);
							this.isDirty = true;

							break;

						case ILibraryAssetStream streamItem:
							string targetPath = Path.Combine(this.FullPath, streamItem.FileName);

							try
							{
								if (File.Exists(targetPath))
								{
									targetPath = GetNonCollidingName(Path.GetFileName(targetPath));
								}

								using (var outputStream = File.OpenWrite(targetPath))
								using (var contentStream = await streamItem.GetStream(null))
								{
									contentStream.Stream.CopyTo(outputStream);
								}

								this.Items.Add(new FileSystemFileItem(targetPath));
								this.isDirty = true;
							}
							catch (Exception ex)
							{
								UiThread.RunOnIdle(() =>
								{
									ApplicationController.Instance.LogError($"Error adding file: {targetPath}\r\n{ex.Message}");
								});
							}
							break;
					}
				}
			});

			directoryWatcher.EnableRaisingEvents = false;

			if (this.isDirty)
			{
				this.ReloadContent();
			}
		}

		public override void Remove(IEnumerable<ILibraryItem> items)
		{
			// Removing content from the filesystem can have devastating effects - open a shell window allowing the customer make changes as they seem fit
			if (AggContext.OperatingSystem == OSType.Windows)
			{
				if (items.Count() == 1
					&& items.FirstOrDefault() is FileSystemFileItem fileItem)
				{
					Process.Start("explorer.exe", $"/select, \"{fileItem.Path}\"");
				}
				else
				{
					Process.Start(this.FullPath);
				}
			}
		}

		public override void Rename(ILibraryItem item, string revisedName)
		{
			if (item is DirectoryContainerLink directoryLink)
			{
				if (Directory.Exists(directoryLink.Path))
				{
					Process.Start(this.FullPath);
				}
			}
			else if (item is FileSystemFileItem fileItem)
			{
				string sourceFile = fileItem.Path;
				if (File.Exists(sourceFile))
				{
					string extension = Path.GetExtension(sourceFile);
					string destFile = Path.Combine(Path.GetDirectoryName(sourceFile), revisedName);
					destFile = Path.ChangeExtension(destFile, extension);

					File.Move(sourceFile, destFile);

					fileItem.Path = destFile;

					this.ReloadContent();
				}
			}
		}

		public class DirectoryContainerLink : FileSystemItem, ILibraryContainerLink
		{
			public DirectoryContainerLink(string path)
				: base(path)
			{
			}

			public bool IsReadOnly { get; set; } = false;

			public bool UseIncrementedNameDuringTypeChange { get; set; }

			public Task<ILibraryContainer> GetContainer(Action<double, string> reportProgress)
			{
				return Task.FromResult<ILibraryContainer>(
					new FileSystemContainer(this.Path)
					{
						UseIncrementedNameDuringTypeChange = this.UseIncrementedNameDuringTypeChange
					});
			}
		}
	}
}
