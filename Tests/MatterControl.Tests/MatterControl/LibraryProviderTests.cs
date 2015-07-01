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

using MatterHackers.MatterControl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.DataStorage;
using System.IO;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterControl.Tests
{
    [TestFixture]
    public class LibraryProviderTests
    {
		string pathToMesh = Path.Combine("..", "..", "..", "TestData", "TestMeshes", "LibraryProviderData");
		string meshFileName;

		bool dataReloaded = false;
		bool itemAdded = false;
		bool itemRemoved = false;
		private event EventHandler unregisterEvents;

		public LibraryProviderTests()
		{
#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", ".."));
#endif
		}

		[SetUp]
		public void SetupBeforeTest()
		{
			meshFileName = Path.Combine(pathToMesh, "Box20x20x10.stl");

			dataReloaded = false;
			itemAdded = false;
			itemRemoved = false;

			LibraryProvider.DataReloaded.RegisterEvent((sender, e) => { dataReloaded = true; }, ref unregisterEvents);
			LibraryProvider.ItemAdded.RegisterEvent((sender, e) => { itemAdded = true; }, ref unregisterEvents);
			LibraryProvider.ItemRemoved.RegisterEvent((sender, e) => { itemRemoved = true; }, ref unregisterEvents);
		}

		[TearDown]
		public void TeardownAfterTest()
		{
			unregisterEvents(this, null);
		}

		[Test, Category("LibraryProviderFileSystem")]
		public void LibraryProviderFileSystem_NavigationWorking()
		{
			LibraryProviderFileSystem testProvider = new LibraryProviderFileSystem(pathToMesh, "TestPath", null);
			Assert.IsTrue(testProvider.CollectionCount == 0, "Start with a new database for these tests.");
			Assert.IsTrue(testProvider.ItemCount == 1, "Start with a new database for these tests.");
			PrintItemWrapper itemAtRoot = testProvider.GetPrintItemWrapper(0);
			List<ProviderLocatorNode> providerLocator = itemAtRoot.PrintItem.GetLibraryProviderLocator();
			Assert.IsTrue(providerLocator.Count == 1);

			// create a collection and make sure it is on disk
			string collectionName = "Collection1";
			string createdDirectory = Path.Combine(pathToMesh, collectionName);
			Assert.IsTrue(!Directory.Exists(createdDirectory));
			Assert.IsTrue(dataReloaded == false);
			testProvider.AddCollectionToLibrary(collectionName);
			Assert.IsTrue(testProvider.CollectionCount == 1);
			Assert.IsTrue(dataReloaded == true);
			Assert.IsTrue(Directory.Exists(createdDirectory));

			dataReloaded = false;
			// make sure removing it gets rid of it
			Assert.IsTrue(dataReloaded == false);
			testProvider.RemoveCollection(testProvider.GetCollectionItem(0));
			Assert.IsTrue(dataReloaded == true);
			Assert.IsTrue(!Directory.Exists(createdDirectory));

			// test GetProviderForItem
		}

		[Test, Category("LibraryProviderSqlite")]
		public void LibraryProviderSqlite_NavigationWorking()
        {
			LibraryProviderSQLite testProvider = new LibraryProviderSQLite(null, null);
			Assert.IsTrue(testProvider.CollectionCount == 0, "Start with a new database for these tests.");
			Assert.IsTrue(testProvider.ItemCount == 1, "Start with a new database for these tests.");
			PrintItemWrapper itemAtRoot = testProvider.GetPrintItemWrapper(0);
			List<ProviderLocatorNode> providerLocator = itemAtRoot.PrintItem.GetLibraryProviderLocator();
			Assert.IsTrue(providerLocator.Count == 1);

			// create a collection and make sure it is on disk
			string collectionName = "Collection1";
			//Assert.IsTrue(); // assert that the record does not exist in the DB
			Assert.IsTrue(dataReloaded == false);
			testProvider.AddCollectionToLibrary(collectionName);
			Assert.IsTrue(testProvider.CollectionCount == 1);
			Assert.IsTrue(dataReloaded == true);
			// Assert.IsTrue(); // assert that the record does exist in the DB

			dataReloaded = false;
			// make sure removing it gets rid of it
			Assert.IsTrue(dataReloaded == false);
			testProvider.RemoveCollection(testProvider.GetCollectionItem(0));
			Assert.IsTrue(dataReloaded == true);
			Assert.IsTrue(testProvider.CollectionCount == 0);
			//Assert.IsTrue(); // assert that the record does not exist in the DB
		}
    }
}
