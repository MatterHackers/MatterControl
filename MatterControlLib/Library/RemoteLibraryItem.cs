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
using System.Net.Http;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.Library
{
	public class RemoteLibraryItem : ILibraryAssetStream, IRequireInitialize
	{
		private string url;
		private HttpClient httpClient;

		public RemoteLibraryItem(string url, string name)
		{
			httpClient = new HttpClient();

			this.url = url;
			this.Name = name ?? "Unknown".Localize();
			this.ID = agg_basics.GetLongHashCode(url).ToString();
		}

		public string ID { get; set; }

		public string Name { get; set; }

		public string FileName => $"{this.Name}.{this.ContentType}";

		public bool IsProtected => false;

		public bool IsVisible => true;

		public DateTime DateCreated { get; } = DateTime.Now;

		public DateTime DateModified { get; } = DateTime.Now;

		public string ContentType { get; private set; } = "jpg";

		public string Category => "General";

		public string AssetPath { get; set; }

		public long FileSize { get; private set; } = 0;

		public bool LocalContentExists => false;

		public async Task<StreamAndLength> GetStream(Action<double, string> progress)
		{
			var response = await httpClient.GetAsync(this.url);

			var headers = response.Content.Headers;

			return new StreamAndLength()
			{
				Stream = await response.Content.ReadAsStreamAsync(),
				Length = headers.ContentLength ?? 0
			};
		}

		public async Task Initialize()
		{
			var request = new HttpRequestMessage(HttpMethod.Head, url);

			var response = await httpClient.SendAsync(request);

			var headers = response.Content.Headers;

			this.ContentType = headers.ContentType.MediaType.Replace("image/", "");
			this.FileSize = headers.ContentLength ?? 0;
		}
	}
}