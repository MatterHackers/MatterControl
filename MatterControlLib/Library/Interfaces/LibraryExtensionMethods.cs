﻿/*
Copyright (c) 2022, John Lewin, Lars Brubaker
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
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Library
{
	public static class LibraryExtensionMethods
	{
		public static async Task<IObject3D> CreateContent(this ILibraryItem libraryItem, Action<double, string> progressReporter)
		{
			if (libraryItem is IRequireInitialize requiresInitialize)
			{
				await requiresInitialize.Initialize();
			}

			if (ApplicationController.Instance.Library.GetContentProvider(libraryItem) is ISceneContentProvider contentProvider)
			{
				return await contentProvider?.CreateItem(libraryItem, progressReporter);
			}

			return null;
		}

		public static bool IsContentFileType(this ILibraryItem item)
		{
			return item is ILibraryObject3D
				|| (item is ILibraryAssetStream contentStream
					&& ApplicationController.Instance.Library.IsContentFileType(contentStream.FileName));
		}

		public static bool IsMeshFileType(this ILibraryItem item)
		{
			return item is ILibraryAssetStream contentStream
					&& ApplicationController.Instance.Library.IsMeshFileType(contentStream.FileName);
		}

		public static IEnumerable<ILibraryContainer> Ancestors(this ILibraryContainer item)
		{
			var context = item.Parent;
			while (context != null)
			{
				yield return context;
				context = context.Parent;
			}
		}

		public static void SaveAs(this ISceneContext sceneContext, ILibraryContainer libraryContainer, string newName)
		{
			var oldContentStore = sceneContext.EditContext.ContentStore;
			var newContentStore = sceneContext.EditContext.ContentStore = libraryContainer as IContentStore;

			// Save to the destination provider
			if (newContentStore is FileSystemContainer fileSystemContainer)
			{
				var fileSystemItem = new FileSystemFileItem(Path.ChangeExtension(Path.Combine(fileSystemContainer.FullPath, newName), ".mcx"));
				fileSystemContainer.Save(fileSystemItem, sceneContext.Scene);

				// if it is not a printer switch to the new fileSystemItem
				if (sceneContext.Printer == null)
				{
					sceneContext.EditContext.SourceItem = fileSystemItem;
				}

				// make sure we don't ask to save again if no changes
				sceneContext.Scene.MarkSavePoint();
			}
			else if (newContentStore is ILibraryWritableContainer writableContainer)
			{
				// Wrap stream with ReadOnlyStream library item and add to container
				writableContainer.Add(new[]
				{
					new InMemoryLibraryItem(sceneContext.Scene)
					{
						Name = newName
					}
				});

				// make sure we don't ask to save again if no changes
				sceneContext.Scene.MarkSavePoint();
			}

			oldContentStore?.Dispose();
		}

		public static IEnumerable<ILibraryContainer> AncestorsAndSelf(this ILibraryContainer item)
		{
			var container = item;
			while (container != null)
			{
				yield return container;
				container = container.Parent;
			}
		}

		public static string GetPath(this ILibraryContainer currentContainer)
		{
			return string.Join("/", currentContainer.AncestorsAndSelf().Reverse().Select(c => c.Name));
		}

		private static string GetDBKey(ILibraryContainer activeContainer)
		{
			var idFromPath = activeContainer.GetPath();

			using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(idFromPath)))
			{
				var sha1 = HashGenerator.ComputeSHA1(stream);
				return $"library-{sha1}";
			}
		}

		public static LibraryViewState GetUserView(this ILibraryContainer currentContainer)
		{
			string dbKey = GetDBKey(currentContainer);

			try
			{
				string storedJson = UserSettings.Instance.get(dbKey);
				if (!string.IsNullOrWhiteSpace(storedJson))
				{
					return JsonConvert.DeserializeObject<LibraryViewState>(storedJson);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error loading view {currentContainer.Name}: {ex.Message}");
			}

			return null;
		}

		public static void PersistUserView(this ILibraryContainer currentContainer, LibraryViewState userView)
		{
			string dbKey = GetDBKey(currentContainer);

			// Store
			string json = JsonConvert.SerializeObject(userView);

			UserSettings.Instance.set(dbKey, json);
		}

		public static void Add(this Dictionary<string, IContentProvider> list, IEnumerable<string> extensions, IContentProvider provider)
		{
			foreach (var extension in extensions)
			{
				list.Add(extension, provider);
			}
		}

		public static Task<IObject3D> CreateContent(this ILibraryAssetStream item, Action<double, string> reporter = null)
		{
			var contentProvider = ApplicationController.Instance.Library.GetContentProvider(item) as ISceneContentProvider;
			return contentProvider?.CreateItem(item, reporter);
		}

		public static void Rename(this ILibraryItem item)
		{
			if (item == null)
			{
				return;
			}

			DialogWindow.Show(
				new InputBoxPage(
					"Rename Item".Localize(),
					"Name".Localize(),
					item.Name,
					"Enter New Name Here".Localize(),
					"Rename".Localize(),
					(newName) =>
					{
						item.Name = newName;
					}));
		}
	}
}
