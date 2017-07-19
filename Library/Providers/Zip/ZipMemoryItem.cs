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
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.Library
{
	public class ZipMemoryItem : FileSystemItem, ILibraryContentStream
	{
		public ZipMemoryItem(string filePath, string relativePath, long fileSize)
			: base(filePath)
		{
			this.RelativePath = relativePath;
			this.Name = System.IO.Path.GetFileName(relativePath);
			this.FileSize = fileSize;
		}

		public string RelativePath { get; set; }

		public string ContentType => System.IO.Path.GetExtension(this.Name).ToLower().Trim('.');
		
		public string AssetPath { get; } = null;

		public string FileName => System.IO.Path.GetFileName(this.Name);

		/// <summary>
		// Gets the size, in bytes, of the current file.
		/// </summary>
		public long FileSize { get; private set; }

		public async Task<StreamAndLength> GetContentStream(Action<double, string> reportProgress)
		{
			var memStream = await Task.Run(() =>
			{
				var memoryStream = new MemoryStream();

				using (var file = File.OpenRead(this.Path))
				using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
				{
					var zipStream = zip.Entries.Where(e => e.FullName == this.RelativePath).FirstOrDefault()?.Open();
					zipStream.CopyTo(memoryStream);
				}

				memoryStream.Position = 0;

				return memoryStream;
			});

			return new StreamAndLength()
			{
				Stream = memStream,
				Length = memStream.Length
			};
		}

		/*
		public async Task<IObject3D> GetContent(Action<double, string> reportProgress)
		{
			var streamAndLength = await GetContentStream(null);
			IObject3D object3D = Object3D.Load(streamAndLength.Stream, System.IO.Path.GetExtension(Name));
			streamAndLength.Stream.Dispose();

			return object3D;
		}

		public void SetContent(IObject3D item)
		{
			throw new NotImplementedException();
		} */
	}
}
