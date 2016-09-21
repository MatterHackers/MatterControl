/*
Copyright (c) 2015, Lars Brubaker
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

using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.Agg;

namespace MatterControl.Tests
{
	[TestFixture]
	public class LibraryProviderTests
	{
		private bool dataReloaded = false;
		private const string meshFileName = "Box20x20x10.stl";
		private string meshPathAndFileName = TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl", "Tests", "TestData", "TestMeshes", "LibraryProviderData", meshFileName);

		public LibraryProviderTests()
		{
#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
#endif
		}

		// Timing issues make this test is too unstable to run. The DataReloaded event frequently resets the 
		// dataReloaded variable right after being set to false, resulting in a test failure where dataReloaded is
		// asserted to be false but is not. It repros best via command line but does fail in Visual Studio on release
		// builds if you run it enough times
		[Test, Category("FixNeeded")]
		public void LibraryProviderFileSystem_NavigationWorking()
		{
			string downloadsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			string testLibraryDirectory = Path.Combine(downloadsDirectory, "LibraryProviderFileSystemTest");
			if (Directory.Exists(testLibraryDirectory))
			{
				Directory.Delete(testLibraryDirectory, true);
			}

			Directory.CreateDirectory(testLibraryDirectory);

			LibraryProviderFileSystem testProvider = new LibraryProviderFileSystem(testLibraryDirectory, "TestPath", null, null);
			testProvider.DataReloaded += (s, e) => { dataReloaded = true; };

			Assert.IsTrue(testProvider.CollectionCount == 0, "Start with a new database for these tests.");
			Assert.IsTrue(testProvider.ItemCount == 0, "Start with a new database for these tests.");

			// create a collection and make sure it is on disk
			dataReloaded = false; // it has been loaded for the default set of parts

			string collectionName = "Collection1";
			string createdDirectory = Path.Combine(testLibraryDirectory, collectionName);

			Assert.IsFalse(Directory.Exists(createdDirectory), "CreatedDirectory should *not* exist");
			Assert.IsFalse(dataReloaded, "Reload should *not* have occurred");

			testProvider.AddCollectionToLibrary(collectionName);
			Thread.Sleep(500); // wait for the add to finish

			Assert.AreEqual(1, testProvider.CollectionCount, "Incorrect collection count");
			Assert.IsTrue(dataReloaded, "Reload should *have* occurred");
			Assert.IsTrue(Directory.Exists(createdDirectory), "CreatedDirectory *should* exist");

			// add an item works correctly
			LibraryProvider subProvider = testProvider.GetProviderForCollection(testProvider.GetCollectionItem(0));
			subProvider.DataReloaded += (sender, e) => { dataReloaded = true; };
			dataReloaded = false;
			//itemAdded = false;
			string subPathAndFile = Path.Combine(createdDirectory, meshFileName);
			Assert.IsFalse(File.Exists(subPathAndFile), "File should *not* exist: " + subPathAndFile);
			Assert.IsFalse(dataReloaded, "Reload should *not* have occurred");
			//Assert.IsTrue(itemAdded == false);

			// WIP: saving the name incorrectly for this location (does not need to be changed).
			subProvider.AddFilesToLibrary(new string[] { meshPathAndFileName });
			Thread.Sleep(3000); // wait for the add to finish

			PrintItemWrapper itemAtRoot = subProvider.GetPrintItemWrapperAsync(0).Result;

			Assert.IsTrue(subProvider.ItemCount == 1);
			Assert.IsTrue(dataReloaded == true);
			//Assert.IsTrue(itemAdded == true);
			Assert.IsTrue(File.Exists(subPathAndFile));

			// make sure the provider locator is correct

			// remove item works
			dataReloaded = false;
			Assert.IsTrue(dataReloaded == false);
			subProvider.RemoveItem(0);
			Thread.Sleep(500); // wait for the remove to finish
			Assert.IsTrue(dataReloaded == true);
			Assert.IsTrue(!File.Exists(subPathAndFile));

			// remove collection gets rid of it
			dataReloaded = false;
			Assert.IsTrue(dataReloaded == false);
			testProvider.RemoveCollection(0);
			Thread.Sleep(500); // wait for the remove to finish
			Assert.IsTrue(dataReloaded == true);
			Assert.IsTrue(testProvider.CollectionCount == 0);
			Assert.IsTrue(!Directory.Exists(createdDirectory));

			if (Directory.Exists(testLibraryDirectory))
			{
				Directory.Delete(testLibraryDirectory, true);
			}
		}

		[SetUp]
		public void SetupBeforeTest()
		{
			dataReloaded = false;
		}

		private bool NamedCollectionExists(string nameToLookFor)
		{
			string query = string.Format("SELECT * FROM PrintItemCollection WHERE Name = '{0}' ORDER BY Name ASC;", nameToLookFor);
			foreach (PrintItemCollection collection in Datastore.Instance.dbSQLite.Query<PrintItemCollection>(query))
			{
				if (collection.Name == nameToLookFor)
				{
					return true;
				}
			}

			return false;
		}

		private bool NamedItemExists(string nameToLookFor)
		{
			string query = string.Format("SELECT * FROM PrintItem WHERE Name = '{0}' ORDER BY Name ASC;", nameToLookFor);
			foreach (PrintItem collection in Datastore.Instance.dbSQLite.Query<PrintItem>(query))
			{
				if (collection.Name == nameToLookFor)
				{
					return true;
				}
			}

			return false;
		}
	}
}