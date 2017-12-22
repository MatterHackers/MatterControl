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
using MatterHackers.Agg.Image;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.Library
{
	public abstract class WritableContainer : LibraryContainer, ILibraryWritableContainer
	{
		public event EventHandler<ItemChangedEventArgs> ItemContentChanged;

		public virtual void OnItemContentChanged(ItemChangedEventArgs args)
		{
			this.ItemContentChanged?.Invoke(this, args);
		}

		public virtual void Add(IEnumerable<ILibraryItem> items)
		{
		}

		public virtual void Remove(IEnumerable<ILibraryItem> items)
		{
		}

		public virtual void Rename(ILibraryItem item, string revisedName)
		{
		}

		public virtual void Save(ILibraryItem item, IObject3D content)
		{
			if (item is FileSystemFileItem fileItem)
			{
				// Serialize the scene to disk using a modified Json.net pipeline with custom ContractResolvers and JsonConverters
				File.WriteAllText(fileItem.Path, content.ToJson());

				this.OnItemContentChanged(new ItemChangedEventArgs(fileItem));
			}
		}

		public virtual void Move(IEnumerable<ILibraryItem> items, ILibraryWritableContainer sourceContainer)
		{
			foreach(var item in items.OfType<ILibraryContentStream>().ToList())
			{
				var enumerable = new[] { item };

				this.Add(enumerable);
				sourceContainer.Remove(enumerable);
			}

		}

		public virtual void SetThumbnail(ILibraryItem item, int width, int height, ImageBuffer imageBuffer)
		{
		}

		public virtual bool AllowAction(ContainerActions containerActions)
		{
			return true;
		}
	}
}
