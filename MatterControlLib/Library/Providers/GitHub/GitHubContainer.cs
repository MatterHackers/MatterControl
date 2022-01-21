/*
Copyright (c) 2019, John Lewin
Copyright (c) 2021, Lars Brubaker
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.VersionManagement;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Library
{
	public class GitHubContainer : LibraryContainer
	{
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
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
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter

		public string Account { get; }

		public string Repository { get; }

		public string RepoDirectory { get; }

		private object locker = new object();

		public GitHubContainer(string containerName, string account, string repositor, string repoDirectory)
		{
			this.ChildContainers = new SafeList<ILibraryContainerLink>();
			this.Name = containerName;
			this.Account = account;
			this.Repository = repositor;
			this.RepoDirectory = repoDirectory;

			// Initialize a default CollectionData with a "Loading..." entry
			this.Items = new SafeList<ILibraryItem>();
			this.Items.Add(new MessageItem("Loading".Localize() + "..."));
		}

		public override void Load()
		{
			var uri = $"https://api.github.com/repos/{Account}/{Repository}/contents/{RepoDirectory}";
			// get the directory contents
			WebCache.RetrieveText(uri,
				(content) =>
				{
					lock (locker)
					{
						if (content.Contains("rate limit exceeded"))
						{
							HeaderMarkdown = content;
							OnContentChanged();
							return;
						}
						ParseJson(content);
					}
				},
				false,
				AddCromeHeaders);
		}

		private void ParseJson(string jsonStr)
		{
			// parse result
			FileInfo[] dirContents = JsonConvert.DeserializeObject<FileInfo[]>(jsonStr);

			var childContainers = new SafeList<ILibraryContainerLink>();

			this.Items.Clear();

			// read in data
			foreach (FileInfo file in dirContents)
			{
				if (file.type == "dir")
				{
					if (file.name == ".images")
					{
						ImageUrlCache = new List<(string name, string url)>();
						// do the initial load
						LoadFolderImageUrlCache().Wait();
					}
					else
					{
						childContainers.Add(new GitHubContainerLink(file.name,
							Account,
							Repository,
							RepoDirectory + "/" + file.name));
					}
				}
				else if (file.type == "file")
				{
					if (Path.GetExtension(file.name).ToLower() == ".library")
					{
						childContainers.Add(new GitHubLibraryLink(Path.GetFileNameWithoutExtension(file.name),
							Account,
							Repository,
							file.download_url));
					}
					else if (file.name == "index.md")
					{
						var uri = $"https://raw.githubusercontent.com/{Account}/{Repository}/main/{RepoDirectory}/index.md";
						WebCache.RetrieveText(uri,
							(content) =>
							{
								HeaderMarkdown = content;
								OnContentChanged();
							},
							true,
							AddCromeHeaders);
					}
					else
					{
						this.Items.Add(new GitHubLibraryItem(file.name, file.download_url));
					}
				}
			}

			Task.Run(async () =>
			{
				foreach (var item in this.Items)
				{
					// check if we have any of the images cached
					var thumbnail = await Task.Run(() => ApplicationController.Instance.Thumbnails.LoadCachedImage(item, 256, 256));

					if (thumbnail != null
						&& thumbnail.Width == 256)
					{
						// save so it is easy to create the upload data for GitHub folders
						var filename = ApplicationController.CacheablePath(
							Path.Combine("GitHubImages", this.Repository, this.RepoDirectory.Replace("/", "\\"), ".images"),
							$"{Path.GetFileNameWithoutExtension(item.Name)}.png");
						if (!File.Exists(filename))
						{
							ImageIO.SaveImageData(filename, thumbnail);
						}
					}
				}
			});

			this.ChildContainers = childContainers;

			OnContentChanged();
		}

		private List<(string name, string url)> ImageUrlCache = null;

		public override Task<ImageBuffer> GetThumbnail(ILibraryItem item, int width, int height)
		{
			var existingThumbnail = base.GetThumbnail(item, width, height);

			if (existingThumbnail.Result == null)
			{
				// if there is a local cache check it first
				if (ImageUrlCache != null)
				{
					// load again to make sure we block until the load is done
					LoadFolderImageUrlCache().Wait();
					var itemImage = CheckForImage(item.Name, ImageUrlCache);
					if (itemImage != null)
					{
						itemImage.SetRecieveBlender(new BlenderPreMultBGRA());
						return Task.FromResult<ImageBuffer>(itemImage);
					}
				}
			}

			return existingThumbnail;
		}

		private ImageBuffer CheckForImage(string id, List<(string name, string url)> cache)
		{
			foreach (var imageUrl in cache)
			{
				if (Path.GetFileNameWithoutExtension(imageUrl.name) == Path.GetFileNameWithoutExtension(id))
				{
					// download the image and cache it
					var image = new ImageBuffer(LibraryConfig.DefaultItemIcon);
					image.SetRecieveBlender(new BlenderPreMultBGRA());
					WebCache.RetrieveImageAsync(image, imageUrl.url, false);
					return image;
				}
			}

			return null;
		}

		private async Task LoadFolderImageUrlCache()
		{
			if (ImageUrlCache.Count == 0)
			{
				ImageUrlCache.Add(("recursion block", "No Image Files"));

				// Check if we can find the thumbnail in the GitHub .images directory
				var uri = $"https://api.github.com/repos/{Account}/{Repository}/contents/{RepoDirectory}/.images";
				// get the directory contents
				var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
				AddCromeHeaders(requestMessage);
				using (var client = new HttpClient())
				{
					using (HttpResponseMessage response = await client.SendAsync(requestMessage))
					{
						var content = await response.Content.ReadAsStringAsync();
						if (!string.IsNullOrEmpty(content) && !content.Contains("\"Not Found\""))
						{
							FileInfo[] dirContents = JsonConvert.DeserializeObject<FileInfo[]>(content);

							// read in data
							foreach (FileInfo file in dirContents)
							{
								if (file.type == "file")
								{
									ImageUrlCache.Add((file.name, file.download_url));
								}
							}
						}
					}
				}
			}
		}

		public static void AddCromeHeaders(HttpRequestMessage request)
		{
#if true
			// request.Headers.Add("User-Agent", "MatterControl");
			request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MatterControl", LatestVersionRequest.VersionKey.CurrentBuildNumber));
			var token = UserSettings.Instance.get("GitHubPat");
			if (!string.IsNullOrEmpty(token))
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Token", token);
			}
#else
			request.Headers.Add("Connection", "keep-alive");
			request.Headers.Add("Upgrade-Insecure-Requests", "1");
			request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.105 Safari/537.36");
			request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
			if (!string.IsNullOrEmpty(token))
			{
				request.Headers.Add("Authorization", $"token {token}");
			}
			request.Headers.Add("Sec-Fetch-Site", "none");
			request.Headers.Add("Sec-Fetch-Mode", "navigate");
			request.Headers.Add("Sec-Fetch-User", "?1");
			request.Headers.Add("Sec-Fetch-Dest", "document");
			request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
#endif
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

			public event EventHandler NameChanged;

			public string Name
			{
				get => this.FileName;
				set { } // do nothing (can't rename)
			}

			public Task<StreamAndLength> GetStream(Action<double, string> progress)
			{
				return Task.FromResult(new StreamAndLength()
				{
					Stream = StaticData.Instance.OpenStream(AssetPath),
					Length = -1
				});
			}
		}

		public class GitHubContainerLink : ILibraryContainerLink
		{
			private readonly string owner;
			private readonly string repository;

			protected string Path { get; }

			public GitHubContainerLink(string containerName, string owner, string repository, string path)
			{
				this.Name = containerName;
				this.owner = owner;
				this.repository = repository;
				this.Path = path;
			}

			public bool IsReadOnly { get; set; } = true;

			public bool UseIncrementedNameDuringTypeChange { get; set; }

			public string ID => Name
				.GetLongHashCode(owner
					.GetLongHashCode(repository
						.GetLongHashCode(Path
							.GetLongHashCode()))).ToString();

			private string _name;
			public string Name
			{
				get => _name; set
				{
					if (_name != value)
					{
						_name = value;
						NameChanged?.Invoke(this, EventArgs.Empty);
					}
				}
			}

			public event EventHandler NameChanged;

			public bool IsProtected => false;

			public bool IsVisible => true;

			public DateTime DateModified => DateTime.Now;

			public DateTime DateCreated => DateTime.Now;

			public virtual Task<ILibraryContainer> GetContainer(Action<double, string> reportProgress)
			{
				return Task.FromResult<ILibraryContainer>(new GitHubContainer(Name, owner, repository, Path));
			}
		}

		public class GitHubLibraryLink : GitHubContainerLink
		{
			public GitHubLibraryLink(string containerName, string owner, string repository, string path)
				: base(containerName, owner, repository, path)
			{
			}

			public override async Task<ILibraryContainer> GetContainer(Action<double, string> reportProgress)
			{
				var content = WebCache.GetCachedText(Path, false, AddCromeHeaders);
				return await LibraryJsonFile.ContainerFromJson(Name, content).GetContainer(null);
			}
		}
	}
}