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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl.Library
{
	public class PrintQueueContainer : WritableContainer
	{
		public PrintQueueContainer()
		{
			this.ChildContainers = new List<ILibraryContainerLink>();
			this.Items = new List<ILibraryItem>();
			this.Name = "Print Queue".Localize();

			Task.Run(() =>
			{
				this.ReloadContainer();
			});
		}

		private void ReloadContainer()
		{
			this.Items = QueueData.Instance.PrintItems.Select(p => new FileSystemFileItem(p.FileLocation)
			{
				Name = p.Name
			}).ToList<ILibraryItem>();

			UiThread.RunOnIdle(this.OnReloaded);
		}

		public override async void Add(IEnumerable<ILibraryItem> items)
		{
			await AddAllItems(items);

			this.ReloadContainer();
		}

		public static async Task AddAllItems(IEnumerable<ILibraryItem> items)
		{
			await Task.Run(async () =>
			{
				foreach (var item in items)
				{
					switch (item)
					{
						case ILibraryContentStream streamItem:
							string itemPath;

							if (streamItem is FileSystemFileItem)
							{
								// Get existing file path
								var fileItem = streamItem as FileSystemFileItem;
								itemPath = fileItem.Path;
							}
							else
							{
								// Copy stream to library path
								itemPath = ApplicationDataStorage.Instance.GetNewLibraryFilePath("." + streamItem.ContentType);

								using (var outputStream = File.OpenWrite(itemPath))
								using (var streamInteface = await streamItem.GetContentStream(null))
								{
									streamInteface.Stream.CopyTo(outputStream);
								}
							}

							// Add to Queue
							if (File.Exists(itemPath))
							{
								QueueData.Instance.AddItem(
									new PrintItemWrapper(
										new PrintItem(streamItem.Name, itemPath)),
									0);
							}
							break;
					}
				}
			});
		}

		public override void Remove(IEnumerable<ILibraryItem> items)
		{
			foreach (var fileSystemItem in items.OfType<FileSystemFileItem>())
			{
				if (fileSystemItem != null)
				{
					var matches = QueueData.Instance.PrintItems.Where(p => p.FileLocation == fileSystemItem.Path).ToList();

					foreach(var printItem in matches)
					{
						int index = QueueData.Instance.GetIndex(printItem);
						if (index != -1)
						{
							QueueData.Instance.RemoveAt(index);
						}
					}
				}
			}

			this.ReloadContainer();
		}

		public override bool AllowAction(ContainerActions containerActions)
		{
			return containerActions != ContainerActions.AddContainers;
		}

		public override void Dispose()
		{
		}
	}
}
