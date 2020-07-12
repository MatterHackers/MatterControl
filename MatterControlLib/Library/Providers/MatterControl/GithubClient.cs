/*
Copyright (c) 2020, Lars Brubaker
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
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Library
{
	// Github classes
	public class GithubClient
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

		// Get all files from a repo
		public static async Task<Directory> getRepo(string owner, string name, string access_token)
		{
			HttpClient client = new HttpClient();
			Directory root = await readDirectory("root", client, string.Format("https://api.github.com/repos/{0}/{1}/contents/", owner, name), access_token);
			client.Dispose();
			return root;
		}

		// recursively get the contents of all files and subdirectories within a directory
		private static async Task<Directory> readDirectory(string name, HttpClient client, string uri, string access_token)
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
				{ // read in the subdirectory
					Directory sub = await readDirectory(file.name, client, file._links.self, access_token);
					result.subDirs.Add(sub);
				}
				else
				{ // get the file contents;
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
	}
}