﻿/*
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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Tests.Automation;
using Xunit;


namespace MatterControl.Tests.MatterControl
{
	//[TestFixture, Parallelizable(ParallelScope.Children)]
	public class PathTests
	{
		[Fact]
		public Task CacheablePathTest()
		{
			StaticData.RootPath = StaticData.RootPath = MatterControlUtilities.StaticDataPath;

			string path = ApplicationController.CacheablePath("scope", "key.file");

			Assert.Equal(
				path.Substring(path.IndexOf("MatterControl")),
				Path.Combine("MatterControl", "data", "temp", "cache", "scope", "key.file"));//,
            //"Unexpected CacheablePath Value");

			return Task.CompletedTask;
		}

		[Fact]
		public Task CacheDirectoryTest()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;

			string path = ApplicationDataStorage.Instance.CacheDirectory;

			Assert.Equal(
				path.Substring(path.IndexOf("MatterControl")),
				Path.Combine("MatterControl", "data", "temp", "cache"));//,
            //"Unexpected CacheDirectory Value");

			return Task.CompletedTask;
		}

		[Fact]
		public Task TempPathTest()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;

			string path = ApplicationDataStorage.Instance.ApplicationTempDataPath;

			Assert.Equal(
				path.Substring(path.IndexOf("MatterControl")),
				Path.Combine("MatterControl", "data", "temp"));//,
            //"Unexpected ApplicationTempDataPath Value");

			return Task.CompletedTask;
		}
	}
}
