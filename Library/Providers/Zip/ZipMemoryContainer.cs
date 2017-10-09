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
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.Library
{
	public class ZipMemoryContainer : LibraryContainer
	{
		public ZipMemoryContainer()
		{
		}

		public string RelativeDirectory { get; set; }

		public string Path { get; set; }

		public override void Load()
		{
			//string hashCode = this.Url.GetHashCode().ToString();
			var items = new Dictionary<string, long>();
			var directories = new HashSet<string>();

			using (var file = File.OpenRead(this.Path))
			using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
			{
				foreach (var entry in zip.Entries)
				{
					if (entry.FullName.StartsWith(RelativeDirectory))
					{
						string remainingPath = entry.FullName.Substring(RelativeDirectory.Length)?.Trim().TrimStart('/');

						var segments = remainingPath.Split('/');
						var firstDirectory = segments.First();
						var lastSegment = segments.Last();

						if (!string.IsNullOrEmpty(lastSegment) && segments.Length == 1)
						{
							items.Add(entry.Name, entry.Length);
						}
						else if (remainingPath.Length > 0)
						{
							directories.Add(firstDirectory);
						}
					}
				}
			}

			this.Name = System.IO.Path.GetFileNameWithoutExtension(this.Path);

			this.ChildContainers = directories.Where(d => !string.IsNullOrEmpty(d)).Select(d =>
				new LocalZipContainerLink(this.Path)
				{
					CurrentDirectory = RelativeDirectory.Length == 0 ? d : $"{RelativeDirectory}/{d}"
				}).ToList<ILibraryContainerLink>();

			this.Items = items.Select(kvp => new ZipMemoryItem(this.Path, RelativeDirectory.Length == 0 ? kvp.Key : $"{RelativeDirectory}/{kvp.Key}", kvp.Value)).ToList<ILibraryItem>();
		}

		public override void Dispose()
		{
		}
	}
}
