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

using System.IO;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests
{
	[TestFixture, RunInApplicationDomain]
	public class LibraryProviderSqliteTests
	{
		private bool dataReloaded = false;
		private string meshPathAndFileName = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "TestMeshes", "LibraryProviderData", "Box20x20x10.stl");

		[SetUp]
		public void SetupBeforeTest()
		{
			dataReloaded = false;
		}

#if !__ANDROID__
		[Test]
		public void LibraryProviderSqlite_NavigationWorking()
		{
			StaticData.Instance = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			LibraryProviderSQLite testProvider = new LibraryProviderSQLite(null, null, null, "Local Library");
			testProvider.DataReloaded += (sender, e) => { dataReloaded = true; };
			Thread.Sleep(3000); // wait for the library to finish initializing
			UiThread.InvokePendingActions();
			Assert.AreEqual(0, testProvider.CollectionCount, "Start with a new database for these tests.");
			Assert.AreEqual(3, testProvider.ItemCount, "Start with a new database for these tests.");

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
			UiThread.InvokePendingActions();

			Assert.IsTrue(testProvider.ItemCount == 4);
			Assert.IsTrue(dataReloaded == true);
			string fileNameWithExtension = Path.GetFileNameWithoutExtension(meshPathAndFileName);
			Assert.IsTrue(NamedItemExists(fileNameWithExtension));

			// make sure the provider locater is correct

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

			//MatterControlUtilities.RestoreStaticDataAfterTesting(staticDataState, true);
		}
#endif

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