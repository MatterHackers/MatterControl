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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, Category("LibraryContainerTests"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class LibraryContainerTests
	{
		[Test]
		public Task TestExistsForEachContainerType()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// Find all test methods on this test class
			var thisClassMethods = this.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

			// Find and validate all ILibraryContainer types, skipping abstract classes
			foreach (var containerType in PluginFinder.FindTypes<ILibraryContainer>().Where(fieldType => !fieldType.IsAbstract))
			{
				string expectedTestName = $"{containerType.Name}Test";
				Assert.AreEqual(
					1,
					thisClassMethods.Where(m => m.Name == expectedTestName).Count(),
					"Test for LibraryContainer missing, not yet created or typo'd - Expected: " + expectedTestName);
			}

			return Task.CompletedTask;
		}

		[Test]
		public async Task NoContentChangedOnLoad()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			bool onIdlePumpActive = true;

			var uiPump = Task.Run(() =>
			{
				while (onIdlePumpActive)
				{
					UiThread.InvokePendingActions();
					Thread.Sleep(10);
				};

				Console.Write("Exiting");
			});

			// Find and validate all ILibraryContainer types, skipping abstract classes
			foreach (var containerType in PluginFinder.FindTypes<ILibraryContainer>().Where(fieldType => !fieldType.IsAbstract))
			{
				var args = new List<object>();

				if (containerType == typeof(FileSystemContainer))
				{
					args.Add(TestContext.CurrentContext.ResolveProjectPath(4));
				}
				else if (containerType == typeof(RootLibraryContainer))
				{
					// TODO: Not sure how to test RootLibrary given content loads after MatterControl init is finished, skipping for now
					continue;
				}

				if (Activator.CreateInstance(containerType, args.ToArray()) is ILibraryContainer libraryContainer)
				{
					if (libraryContainer is ZipMemoryContainer zipContainer)
					{
						zipContainer.Path = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "TestParts", "Batman.zip");
						zipContainer.RelativeDirectory = Path.GetDirectoryName(zipContainer.Path);
					}

					int changedCount = 0;
					libraryContainer.ContentChanged += (s, e) =>
					{
						changedCount++;
					};

					await Task.Run(() =>
					{
						libraryContainer.Load();
					});

					// Allow time for invalid additional reloads
					await Task.Delay(300);

					// Verify Reload is called;
					Assert.AreEqual(0, changedCount, "Expected reload count not hit - container should fire reload event after acquiring content");
				}
			}

			onIdlePumpActive = false;
		}

		[Test]
		public async Task AddFiresContentChangedEvent()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			string filePath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "TestParts", "Batman.stl");

			bool onIdlePumpActive = true;

			var uiPump = Task.Run(() =>
			{
				while (onIdlePumpActive)
				{
					UiThread.InvokePendingActions();
					Thread.Sleep(10);
				};

				Console.Write("Exiting");
			});

			Type writable = typeof(ILibraryWritableContainer);

			// Find and validate all ILibraryContainer types, skipping abstract classes
			foreach (var containerType in PluginFinder.FindTypes<ILibraryContainer>().Where(fieldType => !fieldType.IsAbstract))
			{
				var args = new List<object>();

				if (containerType == typeof(FileSystemContainer))
				{
					Directory.CreateDirectory(ApplicationDataStorage.Instance.ApplicationTempDataPath);
					args.Add(ApplicationDataStorage.Instance.ApplicationTempDataPath);
				}
				else if (containerType == typeof(RootLibraryContainer)
					|| !writable.IsAssignableFrom(containerType))
				{
					// TODO: Not sure how to test RootLibrary given content loads after MatterControl init is finished, skipping for now
					continue;
				}

				if (Activator.CreateInstance(containerType, args.ToArray()) is ILibraryWritableContainer libraryContainer)
				{
					if (libraryContainer is ZipMemoryContainer zipContainer)
					{
						zipContainer.Path = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "TestParts", "Batman.zip");
						zipContainer.RelativeDirectory = Path.GetDirectoryName(zipContainer.Path);
					}

					int changedCount = 0;
					libraryContainer.ContentChanged += (s, e) =>
					{
						changedCount++;
					};

					var waitUntil = DateTime.Now.AddSeconds(15);

					var result = Task.Run(() =>
					{
						libraryContainer.Load();
						libraryContainer.Add(new[] { new FileSystemFileItem(filePath) });
					});

					// Wait for reload
					while (DateTime.Now <= waitUntil)
					{
						if (changedCount > 0)
						{
							break;
						}

						await Task.Delay(200);
					}

					// Allow time for invalid additional reloads
					await Task.Delay(300);

					Console.WriteLine($"ContentChanged for {containerType.Name}");

					// Verify Reload is called;
					Assert.AreEqual(1, changedCount, $"Expected reload count for {containerType.Name} not hit - container should fire reload event after acquiring content");
				}
			}

			onIdlePumpActive = false;
		}


		[Test, Ignore("Not implemented")]
		public async Task CalibrationPartsContainerTest()
		{
		}

		[Test, Ignore("Not implemented")]
		public async Task PrintHistoryContainerTest()
		{
		}

		[Test, Ignore("Not implemented")]
		public async Task SqliteLibraryContainerTest()
		{
		}

		[Test, Ignore("Not implemented")]
		public async Task PrintQueueContainerTest()
		{
		}

		[Test, Ignore("Not implemented")]
		public async Task PlatingHistoryContainerTest()
		{
		}

		[Test, Ignore("Not implemented")]
		public async Task SDCardContainerTest()
		{
		}

		[Test, Ignore("Not implemented")]
		public async Task FileSystemContainerTest()
		{
		}

		[Test, Ignore("Not implemented")]
		public async Task RootLibraryContainerTest()
		{
		}

		[Test, Ignore("Not implemented")]
		public async Task ZipMemoryContainerTest()
		{
		}
	}
}
