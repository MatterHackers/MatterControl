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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Library
{
	public class GitHubPartsContainer : LibraryContainer
	{
		public struct Directory
		{
			public List<FileData> files;
			public string name;
			public List<Directory> subDirs;
		}

		// Structs used to hold file data
		public struct FileData
		{
			public string contents;
			public string name;
		}

		internal struct FileInfo
		{
			public LinkFields _links;
			public string download_url;
			public string name;
			public string type;
		}

		// JSON parsing methods
		internal struct LinkFields
		{
			public string self;
		}

		private PrinterConfig printer;

		public string Account { get; }

		public string Repository { get; }

		public string RepoDirectory { get; }

		public string AccessToken { get; }

		public GitHubPartsContainer(PrinterConfig printer, string containerName, string account, string repositor, string repoDirectory, string accessToken)
		{
			this.ChildContainers = new List<ILibraryContainerLink>();
			this.Items = new List<ILibraryItem>();
			this.Name = containerName;
			this.printer = printer;
			this.Account = account;
			this.Repository = repositor;
			this.RepoDirectory = repoDirectory;
			this.AccessToken = accessToken;
		}

		public override async void Load()
		{
			var oemParts = AggContext.StaticData.GetFiles(Path.Combine("OEMSettings", "SampleParts"));
			Items = oemParts.Select(s => new StaticDataItem(s)).ToList<ILibraryItem>();

			await GetRepo();

			OnContentChanged();
		}

		// Get all files from a repo
		public async Task<Directory> GetRepo()
		{
			HttpClient client = new HttpClient();
			Directory root = await ReadDirectory("root",
				client,
				$"https://api.github.com/repos/{Account}/{Repository}/contents/{RepoDirectory}",
				AccessToken);
			client.Dispose();
			return root;
		}

		private async Task<Directory> ReadDirectory(string name, HttpClient client, string uri, string access_token)
		{
			// get the directory contents
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
			request.Headers.Add("Authorization",
				"Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(string.Format("{0}:{1}", access_token, "x-oauth-basic"))));
			request.Headers.Add("User-Agent", "lk-github-client");

			// parse result
			HttpResponseMessage response = await client.SendAsync(request);
			string jsonStr = await response.Content.ReadAsStringAsync();
			response.Dispose();
			FileInfo[] dirContents = JsonConvert.DeserializeObject<FileInfo[]>(jsonStr);

			// read in data
			Directory result;
			result.name = name;
			result.subDirs = new List<Directory>();
			result.files = new List<FileData>();
			foreach (FileInfo file in dirContents)
			{
				if (file.type == "dir")
				{
					this.ChildContainers.Add(
						new DynamicContainerLink(
							() => file.name,
							AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder_20x20.png")),
							AggContext.StaticData.LoadIcon(Path.Combine("Library", "calibration_library_folder.png")),
							() => new GitHubPartsContainer(printer, file.name, Account, Repository, RepoDirectory, AccessToken),
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
				else
				{
					// get the file contents;
					HttpRequestMessage downLoadUrl = new HttpRequestMessage(HttpMethod.Get, file.download_url);
					downLoadUrl.Headers.Add("Authorization",
						"Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(string.Format("{0}:{1}", access_token, "x-oauth-basic"))));
					request.Headers.Add("User-Agent", "lk-github-client");

					HttpResponseMessage contentResponse = await client.SendAsync(downLoadUrl);
					string content = await contentResponse.Content.ReadAsStringAsync();
					contentResponse.Dispose();

					FileData data;
					data.name = file.name;
					data.contents = content;

					result.files.Add(data);
				}
			}

			return result;
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