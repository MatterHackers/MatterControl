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

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.Library
{
	public class GitHubLibraryItem : ILibraryAssetStream
	{
		public GitHubLibraryItem(string name, string url)
		{
			this.FileName = name;
			this.Url = url;
		}

		public string AssetPath => CachePath;

		public string Category { get; set; }

		public string ContentType => FileExtension.ToLower();

		public DateTime DateCreated { get; } = DateTime.Now;

		public DateTime DateModified { get; } = DateTime.Now;

		public string FileName { get; set; }

		public long FileSize { get; private set; }

		public string ID => agg_basics.GetLongHashCode(Url).ToString();

		public bool IsLocked { get; internal set; }

		public bool IsProtected { get; internal set; }

		public bool IsVisible => true;

		public virtual bool LocalContentExists => File.Exists(CachePath);

		public event EventHandler NameChanged;

		public string Name
		{
			get
			{
				if (Path.GetExtension(FileName).ToLower() == ".mcx")
				{
					return Path.GetFileNameWithoutExtension(FileName);
				}

				return FileName;
			}

			set
			{
				// do nothing (can't rename)
			}
		}

		public string Url { get; }

		public string ThumbnailUrl { get; internal set; }

		private string CachePath => GetLibraryPath(FileKey, FileExtension);

		public string FileKey => Url.GetLongHashCode().ToString();

		public string FileExtension => Path.GetExtension(FileName).Substring(1);

		public static string GetLibraryPath(string fileKey, string fileExtension)
		{
			return Path.Combine(ApplicationDataStorage.Instance.LibraryAssetsPath, $"{fileKey}.{fileExtension}");
		}

		private bool IsOlderThan(string filename, int hours)
		{
			var timeHouresAgo = DateTime.Now.AddHours(-hours);
			return File.GetCreationTime(filename) <= timeHouresAgo;
		}

		public async Task<StreamAndLength> GetStream(Action<double, string> reportProgress)
		{
			// Acquire the content if missing
			await DownloadDigitalItem(reportProgress, CachePath);

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
			// Check for library cache file and download if missing
			if (!File.Exists(libraryFilePath) || IsOlderThan(libraryFilePath, 12))
			{
				// Get a temporary path to write to during download. If we complete without error, swap this file into the libraryFilePath path
				string tempFilePath = ApplicationDataStorage.Instance.GetTempFileName(FileExtension);

				using (var writeStream = File.Create(tempFilePath))
				{
					await DownloadWithProgress(writeStream, reportProgress);
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

		private async Task DownloadWithProgress(FileStream fileStream, Action<double, string> reportProgress)
		{
			try
			{
				// get the file contents;
				var requestMessage = new HttpRequestMessage(HttpMethod.Get, Url);
				GitHubContainer.AddCromeHeaders(requestMessage);

				using (var client = new HttpClient())
				{
					using (HttpResponseMessage response = await client.SendAsync(requestMessage))
					{
						using (var readData = await response.Content.ReadAsStreamAsync())
						{
							var totalBytes = response.Content.Headers.ContentLength;
							await HttpProgress.ProcessContentStream(totalBytes, readData, fileStream, reportProgress);
						}
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