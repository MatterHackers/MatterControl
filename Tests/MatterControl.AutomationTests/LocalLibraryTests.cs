using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	// Most of these tests are disabled. Local Library needs to be added by InitializeLibrary() (MatterHackers.MatterControl.ApplicationController).
	[TestFixture, Category("MatterControl.UI.Automation"), Parallelizable(ParallelScope.Children)]
	public class LocalLibraryTests
	{
		[Test, ChildProcessTest]
		public async Task LocalLibraryAddButtonAddSingleItemToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();
				testRunner.AddTestAssetsToLibrary(new[] { "Batman.stl" });

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task LocalLibraryAddButtonAddsMultipleItemsToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();
				testRunner.AddTestAssetsToLibrary(new[] { "Rook.amf", "Batman.stl" });

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task LocalLibraryAddButtonAddAMFToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();
				testRunner.AddTestAssetsToLibrary(new[] { "Rook.amf" });

				return Task.CompletedTask;
			}, overrideWidth: 1024, overrideHeight: 800);
		}

		[Test, ChildProcessTest]
		public async Task ParentFolderRefreshedOnPathPop()
		{
			// Expected: When descending into a child folder and moving items into the parent, popping the path to the parent should refresh and show the moved content
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Local Library
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				string folderID = testRunner.CreateChildFolder("New Folder");

				testRunner.DoubleClickByName(folderID);

				// Add Library item
				testRunner.InvokeLibraryAddDialog();
				testRunner.Delay(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				string newFileID = "Row Item Batman";

				testRunner.ClickByName(newFileID);

				testRunner.LibraryMoveSelectedItem();

				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Click Move
				testRunner.ClickByName("Accept Button");

				// Wait for closed window/closed row
				testRunner.WaitForWidgetDisappear("Move Item Window", 5);
				testRunner.WaitForWidgetDisappear("Row Item Batman", 2);

				// Return to the Local Library folder
				testRunner.ClickByName("Library Up Button");

				// Assert that the expected item appears in the parent after popping the path
				testRunner.ClickByName(newFileID);

				return Task.CompletedTask;
			});
		}


		[Test, ChildProcessTest]
		public async Task LocalLibraryAddButtonAddZipToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Local Library
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Make sure that Item does not exist before the test begins
				Assert.IsFalse(testRunner.WaitForName("Row Item Batman", 1), "Batman part should not exist at test start");
				Assert.IsFalse(testRunner.WaitForName("Row Item 2013-01-25 Mouthpiece V2", 1), "Mouthpiece part should not exist at test start");

				// Add Library item
				testRunner.InvokeLibraryAddDialog();
				testRunner.Delay(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.zip"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				testRunner.WaitForName("Batman.zip Row Item Collection");

				testRunner.DoubleClickByName("Batman.zip Row Item Collection");

				Assert.IsTrue(testRunner.WaitForName("Row Item Batman.stl"), "Batman part should exist after adding");
				Assert.IsTrue(testRunner.WaitForName("Row Item 2013-01-25_Mouthpiece_v2.stl"), "Mouthpiece part should exist after adding");

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task DoubleClickSwitchesToOpenTab()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				MatterControlUtilities.CreateDownloadsSubFolder();
				testRunner.AddAndSelectPrinter();

				testRunner.AddItemToBed()
					.ClickByName("Save Menu SplitButton", offset: new Agg.Point2D(30, 0))
					.ClickByName("Save As Menu Item")
					.NavigateToFolder("Downloads Row Item Collection")
					.NavigateToFolder("-Temporary Row Item Collection")
					.ClickByName("Design Name Edit Field")
					.Type("Cube Design")
					.ClickByName("Accept Button");

				var mainViewWidget = ApplicationController.Instance.MainView;
				var tabControl = mainViewWidget.TabControl;
				Assert.AreEqual(5, mainViewWidget.TabControl.AllTabs.Count());

				// open the design for editing
				testRunner.ClickByName("Library Tab")
					.NavigateToFolder("Downloads Row Item Collection")
					.NavigateToFolder("-Temporary Row Item Collection")
					.DoubleClickByName("Row Item Cube Design.mcx")
					.WaitFor(() => mainViewWidget.TabControl.AllTabs.Count() == 6);

				// we have opened a new tab
				Assert.AreEqual(6, mainViewWidget.TabControl.AllTabs.Count());
				// we are on the design tab
				Assert.AreEqual(5, tabControl.SelectedTabIndex);
				Assert.AreEqual("New Design", tabControl.SelectedTabKey);

				// double click it again and prove that it goes to the currently open tab
				testRunner.ClickByName("Library Tab")
					.DoubleClickByName("Row Item Cube Design.mcx");

				// we have not opened a new tab
				Assert.AreEqual(6, mainViewWidget.TabControl.AllTabs.Count());
				// we are on the design tab
				Assert.AreEqual(5, tabControl.SelectedTabIndex);

				// rename in the library tab
				// assert tab name has change

				// rename from the tab
				// assert name in library tab has changed
				MatterControlUtilities.DeleteDownloadsSubFolder();

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task RenameButtonRenamesLocalLibraryFolder()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Local Library
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Create New Folder
				string folderID = testRunner.CreateChildFolder("New Folder");

				testRunner.ClickByName(folderID);
				testRunner.Delay(.2);

				testRunner.LibraryRenameSelectedItem();

				testRunner.Delay(.5);
				testRunner.Type("Renamed Library Folder");

				testRunner.ClickByName("InputBoxPage Action Button");
				testRunner.Delay(.2);

				// Make sure the renamed Library Folder exists
				Assert.IsTrue(testRunner.WaitForName("Renamed Library Folder Row Item Collection"), "Renamed folder should exist");

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task RemoveButtonClickedRemovesSingleItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				testRunner.AddTestAssetsToLibrary(new[] { "Rook.amf" });

				// Select and remove item
				testRunner.ClickByName("Row Item Rook");
				testRunner.LibraryRemoveSelectedItem();

				// Make sure that the item has been removed
				Assert.IsFalse(testRunner.WaitForName("Row Item Rook", .5));

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task RemoveButtonClickedRemovesMultipleItems()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				testRunner.AddTestAssetsToLibrary(new[] { "Rook.amf", "Batman.stl" });

				// Select both items
				testRunner.SelectListItems("Row Item Rook", "Row Item Batman");

				// Remove items
				testRunner.LibraryRemoveSelectedItem();
				testRunner.Delay(1);

				// Make sure both selected items are removed
				Assert.IsFalse(testRunner.WaitForName("Row Item Rook", 1), "Rook part should *not* exist after remove");
				Assert.IsFalse(testRunner.WaitForName("Row Item Batman", 1), "Batman part *not* exist after remove");

				return Task.CompletedTask;
			});
		}
	}
}
