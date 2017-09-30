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

using System.IO;

namespace MatterHackers.MatterControl.Library
{
	/// <summary>
	/// A library item rooted to a local file system path
	/// </summary>
	public class FileSystemItem : ILibraryItem
	{
		private string fileName;

		public string Path { get; set; }
		public string Category { get; set; }
		public string ThumbnailKey { get; set; } = "";
		public virtual bool IsProtected => false;
		public virtual bool IsVisible => true;
		public virtual bool LocalContentExists => true;
		
		public FileSystemItem(string path)
		{
			this.Path = path;
		}

		public string ID => this.Path.GetHashCode().ToString();

		public virtual string Name
		{
			get
			{
				if (fileName == null)
				{
					fileName = System.IO.Path.GetFileName(this.Path);
				}

				return fileName;
			}
			set
			{
				fileName = value;
			}
		}
	}

	public class MockLibraryItem : ILibraryItem
	{
		public string ID { get; set; }

		public string Name { get; set; }

		public bool IsProtected => true;

		public bool IsVisible => true;
	}
}
