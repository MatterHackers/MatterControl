/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Ignore("Product code still needs to be implemented"), Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class LibraryActionTests
	{
		[Test, Ignore("Not Finished")]
		public async Task ClickOnBuyButton()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Make sure image does not exist before we click the buy button
				testRunner.MatchLimit = 500000;
				bool imageExists = testRunner.ImageExists("MatterHackersStoreImage.png");
				Assert.IsTrue(imageExists == false, "Web page is not open");

				//Click Buy button and test that the MatterHackers store web page is open
				testRunner.ClickByName("Buy Materials Button");
				bool imageExists2 = testRunner.ImageExists("MatterHackersStoreImage.png", 10);
				Assert.IsTrue(imageExists2 == true, "Web page is open");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task ClickOnExportButton()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				// Tests that clicking the queue export button with a single item selected opens export item window
				testRunner.CloseSignInAndPrinterSelect();

				//Make sure that the export window does not exist
				bool exportWindowExists1 = testRunner.WaitForName("Export Item Window", 0);
				Assert.IsTrue(exportWindowExists1 == false, "Export window does not exist");

				testRunner.ClickByName("Queue Export Button");
				SystemWindow containingWindow;
				GuiWidget exportWindow = testRunner.GetWidgetByName("Export Item Window", out containingWindow, 5);
				Assert.IsTrue(exportWindow != null, "Export window does exist");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		/// <summary>
		/// Confirms the Export to Zip feature compresses and exports to a zip file and that file imports without issue
		/// </summary>
		/// <returns></returns>
		[Test]
		public async Task ExportToZipImportFromZip()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				// Ensure output file does not exist
				string exportZipPath = MatterControlUtilities.GetTestItemPath("TestExportZip.zip");
				if (File.Exists(exportZipPath))
				{
					File.Delete(exportZipPath);
				}

				testRunner.CloseSignInAndPrinterSelect();

				Assert.AreEqual(4, QueueData.Instance.ItemCount, "Queue should initially have 4 items");

				// Invoke Queue -> Export to Zip dialog
				testRunner.ClickByName("Queue... Menu");
				testRunner.Delay(.2);
				testRunner.ClickByName(" Export to Zip Menu Item");
				testRunner.Delay(2);
				testRunner.Type(exportZipPath);
				testRunner.Delay(2);
				testRunner.Type("{Enter}");

				testRunner.WaitFor(() => File.Exists(exportZipPath));
				Assert.IsTrue(File.Exists(exportZipPath), "Queue was exported to zip file, file exists on disk at expected path");

				// Import the exported zip file and confirm the Queue Count increases by 3 
				testRunner.ClickByName("Library Add Button");
				testRunner.Delay(1);
				testRunner.Type(exportZipPath);
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				testRunner.WaitFor(() => QueueData.Instance.ItemCount == 8);
				Assert.AreEqual(8, QueueData.Instance.ItemCount, "All parts imported successfully from exported zip");

				testRunner.Delay(.3);

				try
				{
					if (File.Exists(exportZipPath))
					{
						File.Delete(exportZipPath);
					}
				}
				catch { }

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Ignore("Test now works as expected but product does not implement expected functionality")]
		public async Task QueueExportIsDisabledIfEmpty()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("Queue... Menu");

				var exportButton = testRunner.GetWidgetByName(" Export to Zip Menu Item", out _, 5);
				Assert.IsNotNull(exportButton, "Export button should exist");
				Assert.IsTrue(exportButton.Enabled, "Export button should be enabled");

				testRunner.ClickByName(" Remove All Menu Item");

				testRunner.Delay(1);

				testRunner.ClickByName("Queue... Menu");
				testRunner.WaitFor(() => !exportButton.Enabled, 4);
				Assert.IsFalse(exportButton.Enabled, "Export button should be disabled after Queue Menu -> Remove All");

				return Task.CompletedTask;
			});
		}

		/// <summary>
		/// Tests that Queue Copy button increases the queue count by one and that a new queue item appears with the expected name
		/// </summary>
		/// <returns></returns>
		[Test]
		public async Task CopyButtonMakesACopyOfPartInTheQueue()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				int expectedQueueCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("Row Item Batman.stl");
				testRunner.Delay(.2);

				testRunner.ClickByName("Queue Copy Button");
				testRunner.WaitFor(() => QueueData.Instance.ItemCount == expectedQueueCount);

				Assert.AreEqual(expectedQueueCount, QueueData.Instance.ItemCount, "Copy button increases queue count by one");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman - copy"), "Copied Batman item exists with expected name");
				testRunner.Delay(.3);

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task SendMenuClickedWithoutCloudPlugins()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				SystemWindow parentWindow;

				testRunner.CloseSignInAndPrinterSelect();

				Assert.IsTrue(QueueData.Instance.ItemCount > 0, "Queue is not empty at test startup");

				testRunner.ClickByName("More...  Menu");
				testRunner.Delay(.2);

				testRunner.ClickByName("Send Menu Item");
				testRunner.Delay(.2);

				// WaitFor Ok button and ensure parent window has expected title and named button
				testRunner.WaitForName("Ok Button");
				var widget = testRunner.GetWidgetByName("Ok Button", out parentWindow);
				Assert.IsTrue(widget != null
					&& parentWindow.Title == "MatterControl - Alert", "Send Disabled warning appears when no plugins exists to satisfy behavior");

				testRunner.Delay(.2);

				// Close dialog before exiting
				testRunner.ClickByName("Ok Button");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Ignore("Not Finished")]
		public async Task ClickCreatePartSheetButton()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				bool queueEmpty = true;
				int queueItemCount = QueueData.Instance.ItemCount;

				if (queueItemCount == 3)
				{
					queueEmpty = false;
				}

				Assert.IsTrue(queueEmpty == false);
				testRunner.ClickByName("Queue... Menu");
				testRunner.Delay(.2);
				testRunner.ClickByName(" Create Part Sheet Menu Item");
				testRunner.Delay(2);

				string pathToSavePartSheet = MatterControlUtilities.GetTestItemPath("CreatePartSheet");
				string validatePartSheetPath = Path.Combine("..", "..", "..", "TestData", "QueueItems", "CreatePartSheet.pdf");

				testRunner.Type(pathToSavePartSheet);
				testRunner.Delay(1);
				testRunner.Type("{Enter}");
				testRunner.Delay(1);
				testRunner.Delay(5);

				bool partSheetCreated = File.Exists(validatePartSheetPath);

				testRunner.Delay(2);
				Assert.IsTrue(partSheetCreated == true);

				if (File.Exists(validatePartSheetPath))
				{
					File.Delete(validatePartSheetPath);
				}

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}
	}
}
