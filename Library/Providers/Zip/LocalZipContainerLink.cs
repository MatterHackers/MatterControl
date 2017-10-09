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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;

namespace MatterHackers.MatterControl.Library
{
	public class LocalZipContainerLink : FileSystemItem, ILibraryContainerLink
	{
		private static ImageBuffer thumbnail;

		static LocalZipContainerLink()
		{
			thumbnail = LibraryProviderHelpers.LoadInvertIcon("FileDialog", "folder_zip.png");
		}

		public bool IsReadOnly { get; } = false;

		public Stream ZipStream { get; set; }

		public override bool IsProtected { get; } = true;

		public LocalZipContainerLink(string filePath)
			: base(filePath)
		{
			this.ThumbnailKey = null;
		}

		private string currentDirectory = "";
		public string CurrentDirectory
		{
			get { return currentDirectory; }
			set
			{
				currentDirectory = value;
				this.Name = currentDirectory.Split('/').Last();
			}
		}

		public Task<ILibraryContainer> GetContainer(Action<double, string> reportProgress)
		{
			return Task.FromResult<ILibraryContainer>(
				new ZipMemoryContainer()
				{
					RelativeDirectory = this.currentDirectory,
					Path = this.Path
				});
		}
	}
}
