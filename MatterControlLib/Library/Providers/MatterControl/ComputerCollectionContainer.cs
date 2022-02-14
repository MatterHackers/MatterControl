/*
Copyright (c) 2018, John Lewin
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

using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.Library
{
    public class ComputerCollectionContainer : LibraryContainer
    {
		public ComputerCollectionContainer()
		{
			this.ChildContainers = new SafeList<ILibraryContainerLink>();
			this.Items = new SafeList<ILibraryItem>();
			this.Name = "Computer".Localize();

			if (Directory.Exists(ApplicationDataStorage.Instance.DownloadsDirectory))
			{
				this.ChildContainers.Add(
					new DynamicContainerLink(
						"Downloads".Localize(),
						StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
						StaticData.Instance.LoadIcon(Path.Combine("Library", "download_icon.png")),
						() => new FileSystemContainer(ApplicationDataStorage.Instance.DownloadsDirectory)
						{
							UseIncrementedNameDuringTypeChange = true,
							DefaultSort = new LibrarySortBehavior()
							{
								SortKey = SortKey.ModifiedDate,
							}
						}));
			}

			if (Directory.Exists(ApplicationDataStorage.Instance.DesktopDirectory))
			{
				this.ChildContainers.Add(
					new DynamicContainerLink(
						"Desktop".Localize(),
						StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
						StaticData.Instance.LoadIcon(Path.Combine("Library", "desktop.png")),
						() => new FileSystemContainer(ApplicationDataStorage.Instance.DesktopDirectory)
						{
							UseIncrementedNameDuringTypeChange = true,
							DefaultSort = new LibrarySortBehavior()
							{
								SortKey = SortKey.ModifiedDate,
							}
						}));
			}

			if (Directory.Exists(ApplicationDataStorage.Instance.MyDocumentsDirectory))
			{
				this.ChildContainers.Add(
					new DynamicContainerLink(
						"MyDocuments".Localize(),
						StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
						StaticData.Instance.LoadIcon(Path.Combine("Library", "mydocuments.png")),
						() => new FileSystemContainer(ApplicationDataStorage.Instance.MyDocumentsDirectory)
						{
							UseIncrementedNameDuringTypeChange = true,
							DefaultSort = new LibrarySortBehavior()
							{
								SortKey = SortKey.ModifiedDate,
							}
						}));
			}

			if (ProfileManager.Instance != null)
			{
				var userDirectory = ProfileManager.Instance.UserProfilesDirectory;
				var libraryFiles = Directory.GetFiles(userDirectory, "*.library");
				foreach (var libraryFile in libraryFiles)
				{
					this.ChildContainers.Add(LibraryJsonFile.ContainerFromLocalFile(libraryFile));
				}
			}
		}
		
		public override void Load()
        {
        }
    }
}
