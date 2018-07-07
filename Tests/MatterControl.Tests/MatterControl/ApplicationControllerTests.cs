/*
Copyright (c) 2018, John Lewin
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

using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.Tests.Automation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class ApplicationControllerTests
	{
		[Test]
		public async Task LoadCachableShouldFallbackToStaticData()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl", "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(5));

			string cacheScope = Path.Combine("Some", "Specific", "Scope");

			// Load cacheable content from StaticData when the collector returns null and no cached content exists
			var versionInfo = await ApplicationController.LoadCacheableAsync<VersionInfo>(
				cacheKey: "HelloWorld-File1",
				cacheScope: cacheScope,
				collector: async () =>
				{
					// Hypothetical http request with 304 indicating our results cached results have not expired
					return null;
				},
				staticDataFallbackPath: "BuildInfo.txt");

			Assert.IsNotNull(versionInfo, "LoadCacheable should fall back to StaticData content if collection fails");

			string cachePath = ApplicationController.CacheablePath(cacheScope, "HelloWorld-File1");
			Assert.IsFalse(File.Exists(cachePath), "After fall back to StaticData content, cache should not contain fall back content");
		}

		[Test]
		public async Task LoadCachableShouldStoreCollectedResults()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl", "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(5));

			string cacheScope = Path.Combine("Some", "Specific", "Scope");

			// Load cacheable content from collector, writing results to cache
			var versionInfo = await ApplicationController.LoadCacheableAsync<VersionInfo>(
				cacheKey: "HelloWorld-File1",
				cacheScope: cacheScope,
				collector: async () =>
				{
					return JsonConvert.DeserializeObject<VersionInfo>("{\"BuildVersion\": \"HelloFromCollector\"}");
				},
				staticDataFallbackPath: "BuildInfo.txt");

			Assert.IsTrue(versionInfo.BuildVersion == "HelloFromCollector", "LoadCacheable should use content from collector");

			string cachePath = ApplicationController.CacheablePath(cacheScope, "HelloWorld-File1");
			Assert.IsTrue(File.Exists(cachePath), "Collected results should be written to cache at expected path");

			Assert.IsTrue(File.ReadAllText(cachePath).Contains("HelloFromCollector"), "Cached content should equal collected content");
		}
	}
}
