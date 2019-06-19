/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.MatterControl
{
	public class WrappedLibraryContainer : ILibraryContainer
	{
		private ILibraryContainer _libraryContainer;

		public WrappedLibraryContainer(ILibraryContainer libraryContainer)
		{
			_libraryContainer = libraryContainer;
		}

		public List<ILibraryContainerLink> ExtraContainers { get; set; } = new List<ILibraryContainerLink>();

		public string ID => _libraryContainer.ID;

		public string Name => _libraryContainer.Name;

		public string StatusMessage => _libraryContainer.StatusMessage;

		public bool IsProtected => _libraryContainer.IsProtected;

		public Type DefaultView => _libraryContainer.DefaultView;

		public List<ILibraryContainerLink> ChildContainers => this.ExtraContainers.Concat(_libraryContainer.ChildContainers).ToList();

		public List<ILibraryItem> Items => _libraryContainer.Items;

		public ILibraryContainer Parent { get => _libraryContainer.Parent; set => _libraryContainer.Parent = value; }

		public ICustomSearch CustomSearch => _libraryContainer.CustomSearch;

		public SortBehavior DefaultSort => null;

		public event EventHandler ContentChanged;

		public void Activate()
		{
			_libraryContainer.Activate();
		}

		public void Deactivate()
		{
			_libraryContainer.Deactivate();
		}

		public void Dispose()
		{
			_libraryContainer.Dispose();
		}

		public Task<ImageBuffer> GetThumbnail(ILibraryItem item, int width, int height)
		{
			return _libraryContainer.GetThumbnail(item, width, height);
		}

		public void Load()
		{
			_libraryContainer.Load();
		}
	}
}