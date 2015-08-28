﻿/*
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
using MatterHackers.MatterControl.UI;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MatterControl.Tests
{
	[TestFixture, RunInApplicationDomain]
	public class LibraryProviderSqliteTests
	{
		private bool dataReloaded = false;
		private string meshFileName = "Box20x20x10.stl";
		private string meshPathAndFileName;
		private string pathToMesh = Path.Combine("..", "..", "..", "TestData", "TestMeshes", "LibraryProviderData");

		public LibraryProviderSqliteTests()
		{
			#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
			#endif
		}

		private event EventHandler unregisterEvents;

#if !__ANDROID__
		[Test, RunInApplicationDomain]
		public void LibraryProviderSqlite_NavigationWorking()
		{
			MatterControlUITests.DataFolderState staticDataState = MatterControlUITests.MakeNewStaticDataForTesting();

			LibraryProviderSQLite testProvider = new LibraryProviderSQLite(null, null, null, "Local Library");
			testProvider.DataReloaded += (sender, e) => { dataReloaded = true; };
			Thread.Sleep(3000); // wait for the library to finish initializing
			Assert.IsTrue(testProvider.CollectionCount == 0, "Start with a new database for these tests.");
			Assert.IsTrue(testProvider.ItemCount == 1, "Start with a new database for these tests.");

			// create a collection and make sure it is on disk
			dataReloaded = false; // it has been loaded for the default set of parts
			string collectionName = "Collection1";
			Assert.IsTrue(!NamedCollectionExists(collectionName)); // assert that the record does not exist in the DB
			Assert.IsTrue(dataReloaded == false);
			testProvider.AddCollectionToLibrary(collectionName);
			Assert.IsTrue(testProvider.CollectionCount == 1);
			Assert.IsTrue(dataReloaded == true);
			Assert.IsTrue(NamedCollectionExists(collectionName)); // assert that the record does exist in the DB

			PrintItemWrapper itemAtRoot = testProvider.GetPrintItemWrapperAsync(0).Result;

			// add an item works correctly
			dataReloaded = false;
			Assert.IsTrue(!NamedItemExists(collectionName));
			Assert.IsTrue(dataReloaded == false);

			testProvider.AddFilesToLibrary(new string[] { meshPathAndFileName });
			Thread.Sleep(3000); // wait for the add to finish

			Assert.IsTrue(testProvider.ItemCount == 2);
			Assert.IsTrue(dataReloaded == true);
			string fileNameWithExtension = Path.GetFileNameWithoutExtension(meshPathAndFileName);
			Assert.IsTrue(NamedItemExists(fileNameWithExtension));

			// make sure the provider locator is correct

			// remove item works
			dataReloaded = false;
			Assert.IsTrue(dataReloaded == false);
			testProvider.RemoveItem(0);
			Assert.IsTrue(dataReloaded == true);
			Assert.IsTrue(!NamedItemExists(fileNameWithExtension));

			// remove collection gets rid of it
			dataReloaded = false;
			Assert.IsTrue(dataReloaded == false);
			testProvider.RemoveCollection(0);
			Assert.IsTrue(dataReloaded == true);
			Assert.IsTrue(testProvider.CollectionCount == 0);
			Assert.IsTrue(!NamedCollectionExists(collectionName)); // assert that the record does not exist in the DB

			MatterControlUITests.RestoreStaticDataAfterTesting(staticDataState, true);
		}
#endif

		[SetUp]
		public void SetupBeforeTest()
		{
			meshPathAndFileName = Path.Combine(pathToMesh, meshFileName);

			dataReloaded = false;
		}

		[TearDown]
		public void TeardownAfterTest()
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
		}

		private bool NamedCollectionExists(string nameToLookFor)
		{
			string query = string.Format("SELECT * FROM PrintItemCollection WHERE Name = '{0}' ORDER BY Name ASC;", nameToLookFor);
			IEnumerable<PrintItemCollection> result = (IEnumerable<PrintItemCollection>)Datastore.Instance.dbSQLite.Query<PrintItemCollection>(query);
			foreach (PrintItemCollection collection in result)
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
			IEnumerable<PrintItem> result = (IEnumerable<PrintItem>)Datastore.Instance.dbSQLite.Query<PrintItem>(query);
			foreach (PrintItem collection in result)
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