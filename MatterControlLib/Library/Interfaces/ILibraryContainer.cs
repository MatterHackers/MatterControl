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

using MatterHackers.Agg.Image;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Library
{
	public enum ContainerActions
	{
		AddItems,

		AddContainers,

		RenameItems,

		RemoveItems
	}

	public interface ILibraryContainer : IDisposable
	{
		event EventHandler ContentChanged;

		List<ILibraryContainerLink> ChildContainers { get; }

		string CollectionKeyName { get; }

		ICustomSearch CustomSearch { get; }

		LibrarySortBehavior DefaultSort { get; }

		Type DefaultView { get; }

		string ID { get; }

		bool IsProtected { get; }

		List<ILibraryItem> Items { get; }

		string Name { get; }

		ILibraryContainer Parent { get; set; }

		string ContainerHeaderMarkdown { get; }

		void Activate();

		void Deactivate();

		Task<ImageBuffer> GetThumbnail(ILibraryItem item, int width, int height);

		void Load();
	}
}