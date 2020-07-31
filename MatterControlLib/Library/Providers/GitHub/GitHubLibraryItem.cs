/*
Copyright (c) 2019, John Lewin
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

using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Library
{
	public class GitHubLibraryItem : ILibraryAssetStream
	{
		public GitHubLibraryItem(string name, string url)
		{
			this.Name = name;
			this.Url = url;
		}

		public string AssetPath => CachePath;

		public string Category { get; set; }

		public string ContentType => Path.GetExtension(Url);

		public DateTime DateCreated { get; } = DateTime.Now;

		public DateTime DateModified { get; } = DateTime.Now;

		public string FileName => Name;

		public long FileSize { get; private set; }

		public string ID { get; set; }

		public bool IsLocked { get; internal set; }

		public bool IsProtected { get; internal set; }

		public bool IsVisible => true;

		public virtual bool LocalContentExists => File.Exists(CachePath);

		public string Name { get; set; }
		public string Url { get; }
		public string ThumbnailUrl { get; internal set; }

		private string CachePath => GetLibraryPath(FileKey, FileExtension);

		public string FileKey { get; private set; }

		public string FileExtension { get; private set; }

		public static string GetLibraryPath(string fileKey, string fileExtension)
		{
			return Path.Combine(ApplicationDataStorage.Instance.CloudLibraryPath, $"{fileKey}.{fileExtension}");
		}

		public async Task<StreamAndLength> GetStream(Action<double, string> reportProgress)
		{
			if (!this.LocalContentExists)
			{
				// Acquire the content if missing
				await DownloadDigitalItem(reportProgress, CachePath);
			}

			if (File.Exists(CachePath))
			{
				var stream = File.OpenRead(CachePath);

				return new StreamAndLength()
				{
					Length = (int)stream.Length,
					Stream = stream
				};
			}

			return null;
		}

		private async Task<string> DownloadDigitalItem(Action<double, string> reportProgress, string libraryFilePath)
		{
			string url = "";

			// Check for library cache file and download if missing
			if (!File.Exists(libraryFilePath))
			{
				// Get a temporary path to write to during download. If we complete without error, swap this file into the libraryFilePath path
				string tempFilePath = ApplicationDataStorage.Instance.GetTempFileName(FileExtension);

				using (var writeStream = File.Create(tempFilePath))
				{
					await DownloadWithProgress(url, writeStream, reportProgress);
				}

				reportProgress?.Invoke(0, "");

				// Ensure the target directory exists. The AboutWidget.RemoveDirectory logic may remove the Library
				// directory at runtime during cache purge
				Directory.CreateDirectory(Path.GetDirectoryName(libraryFilePath));

				// Delete the target file if necessary
				if (File.Exists(libraryFilePath))
				{
					File.Delete(libraryFilePath);
				}

				// Move the downloaded file to the target path
				File.Move(tempFilePath, libraryFilePath);
			}

			return libraryFilePath;
		}

		private async Task DownloadWithProgress(string url, FileStream fileStream, Action<double, string> reportProgress)
		{
			try
			{
				// get the file contents;
				HttpRequestMessage downLoadUrl = new HttpRequestMessage(HttpMethod.Get, url);
				GitHubContainer.AddCromeHeaders(downLoadUrl);

				string content = "";
				using (HttpClient client = new HttpClient())
				{
					using (HttpResponseMessage contentResponse = await client.SendAsync(downLoadUrl))
					{
						content = await contentResponse.Content.ReadAsStringAsync();
					}
				}
			}
			catch
			{
				// Previous code swallowed errors, doing the same for now
			}
		}
	}
}