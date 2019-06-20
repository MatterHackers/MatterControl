﻿/*
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;

namespace MatterHackers.MatterControl.Library
{
	public abstract class LibraryContainer : ILibraryContainer
	{
		public event EventHandler ContentChanged;

		public string ID { get; set; }

		public string Name { get; set; }

		public Type DefaultView { get; protected set; }

		public List<ILibraryContainerLink> ChildContainers { get; set; } = new List<ILibraryContainerLink>();

		public bool IsProtected { get; protected set; } = true;

		public virtual Task<ImageBuffer> GetThumbnail(ILibraryItem item, int width, int height)
		{
			if (item is LocalZipContainerLink)
			{
				return Task.FromResult(AggContext.StaticData.LoadIcon(Path.Combine("Library", "zip_folder.png")).AlphaToPrimaryAccent().SetPreMultiply());
			}

			return Task.FromResult<ImageBuffer>(null);
		}

		public List<ILibraryItem> Items { get; set; } = new List<ILibraryItem>();

		public ILibraryContainer Parent { get; set; }

		public string StatusMessage { get; set; } = "";

		public virtual ICustomSearch CustomSearch { get; } = null;

		public SortBehavior DefaultSort { get; set; }

		/// <summary>
		/// Reloads the container when contents have changes and fires ContentChanged to notify listeners
		/// </summary>
		protected void ReloadContent()
		{
			// Call the container specific reload implementation
			this.Load();

			// Notify
			this.OnContentChanged();
		}

		protected void OnContentChanged()
		{
			this.ContentChanged?.Invoke(this, null);
		}

		public abstract void Load();

		public virtual void Dispose()
		{
		}

		public virtual void Activate()
		{
		}

		public virtual void Deactivate()
		{
		}
	}
}
