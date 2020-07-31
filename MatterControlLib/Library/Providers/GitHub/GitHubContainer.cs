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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Library
{
	public class GitHubContainer : LibraryContainer
	{
		public struct Directory
		{
			public List<FileData> Files;
			public string Name;
			public List<Directory> SubDirs;
		}

		// Used to hold file data
		public struct FileData
		{
			public string Contents;
			public string Name;
		}

		internal struct FileInfo
		{
			public LinkFields _links;
			public string DownloadUrl;
			public string Name;
			public string Type;
		}

		// JSON parsing methods
		internal struct LinkFields
		{
			public string Self;
		}

		private PrinterConfig printer;

		public string Account { get; }

		public string Repository { get; }

		public string RepoDirectory { get; }

		public GitHubContainer(PrinterConfig printer, string containerName, string account, string repositor, string repoDirectory)
		{
			this.ChildContainers = new List<ILibraryContainerLink>();
			this.Items = new List<ILibraryItem>();
			this.Name = containerName;
			this.printer = printer;
			this.Account = account;
			this.Repository = repositor;
			this.RepoDirectory = repoDirectory;
		}

		public override async void Load()
		{
			try
			{
				await GetRepo();
			}
			catch
			{
				// show an error
			}

			OnContentChanged();
		}

		// Get all files from a repo
		public async Task<Directory> GetRepo()
		{
			HttpClient client = new HttpClient();
			Directory root = await ReadDirectory("root",
				client,
				$"https://api.github.com/repos/{Account}/{Repository}/contents/{RepoDirectory}");
			client.Dispose();
			return root;
		}

		private async Task<Directory> ReadDirectory(string name, HttpClient client, string uri)
		{
			// get the directory contents
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
			AddCromeHeaders(request);

			// parse result
			HttpResponseMessage response = await client.SendAsync(request);
			string jsonStr = await response.Content.ReadAsStringAsync();
			response.Dispose();
			FileInfo[] dirContents = JsonConvert.DeserializeObject<FileInfo[]>(jsonStr);

			// read in data
			Directory result;
			result.Name = name;
			result.SubDirs = new List<Directory>();
			result.Files = new List<FileData>();
			foreach (FileInfo file in dirContents)
			{
				if (file.Type == "dir")
				{
					this.ChildContainers.Add(
						new DynamicContainerLink(
							() => file.Name,
							AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder_20x20.png")),
							AggContext.StaticData.LoadIcon(Path.Combine("Library", "calibration_library_folder.png")),
							() => new GitHubContainer(printer, file.Name, Account, Repository, file.Name),
							() =>
							{
								return true;
							})
						{
							IsReadOnly = true
						});

					// read in the subdirectory
					// Directory sub = await ReadDirectory(file.name, client, file._links.self, access_token);
					// result.subDirs.Add(sub);
				}
				else if (file.Type == "file")
				{
					this.Items.Add(new GitHubLibraryItem(file.Name, file.DownloadUrl));
				}
			}

			return result;
		}

		public static void AddCromeHeaders(HttpRequestMessage request)
		{
			request.Headers.Add("Connection", "keep-alive");
			request.Headers.Add("Upgrade-Insecure-Requests", "1");
			request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.105 Safari/537.36");
			request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
			request.Headers.Add("Sec-Fetch-Site", "none");
			request.Headers.Add("Sec-Fetch-Mode", "navigate");
			request.Headers.Add("Sec-Fetch-User", "?1");
			request.Headers.Add("Sec-Fetch-Dest", "document");
			request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
		}

		private class StaticDataItem : ILibraryAssetStream
		{
			public StaticDataItem()
			{
			}

			public StaticDataItem(string relativePath)
			{
				this.AssetPath = relativePath;
			}

			public string AssetPath { get; }

			public string Category { get; } = "";

			public string ContentType => Path.GetExtension(AssetPath).ToLower().Trim('.');

			public DateTime DateCreated { get; } = DateTime.Now;

			public DateTime DateModified { get; } = DateTime.Now;

			public string FileName => Path.GetFileName(AssetPath);

			public long FileSize { get; } = -1;

			public string ID => agg_basics.GetLongHashCode(AssetPath).ToString();

			public bool IsProtected => true;

			public bool IsVisible => true;

			public bool LocalContentExists => true;

			public string Name => this.FileName;

			public Task<StreamAndLength> GetStream(Action<double, string> progress)
			{
				return Task.FromResult(new StreamAndLength()
				{
					Stream = AggContext.StaticData.OpenStream(AssetPath),
					Length = -1
				});
			}
		}
	}
}