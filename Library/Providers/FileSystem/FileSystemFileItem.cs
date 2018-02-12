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
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.Library
{
	public class FileSystemFileItem : FileSystemItem, ILibraryAssetStream
	{
		public string FileName => System.IO.Path.GetFileName(this.Path);

		public string ContentType => System.IO.Path.GetExtension(this.Path).ToLower().Trim('.');

		public string AssetPath => this.Path;

		/// <summary>
		// Gets the size, in bytes, of the current file.
		/// </summary>
		public long FileSize { get; private set; }

		public FileSystemFileItem(string path) : base(path)
		{
			var fileInfo = new FileInfo(path);
			if (fileInfo.Exists)
			{
				this.FileSize = fileInfo.Length;
			}
		}

		public Task<StreamAndLength> GetContentStream(Action<double, string> reportProgress)
		{
			if (File.Exists(this.Path)
				&& (ApplicationController.Instance.IsLoadableFile(this.Path)
					|| (System.IO.Path.GetExtension(this.Path) is string extension
						&& string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))))
			{
				var stream = File.OpenRead(this.Path);
				return Task.FromResult(new StreamAndLength()
				{
					Stream = stream,
					Length = stream.Length
				});
			}

			return Task.FromResult<StreamAndLength>(null);
		}
	}
}
