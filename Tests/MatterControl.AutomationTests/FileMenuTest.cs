using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class FileMenuTest
	{
		[Test]
		public async Task FileMenuAddPrinter()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Delay(1);
				testRunner.ClickByName("Add Printer Menu Item");
				testRunner.Delay(1);
				Assert.IsTrue(testRunner.WaitForName("Select Make", 3));

				testRunner.ClickByName("Cancel Wizard Button");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task AddToQueueMenuItemAddsSingleFile()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Delay(1);
				testRunner.ClickByName("Add File To Queue Menu Item");
				testRunner.Delay(2);

				string queueItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");

				int queueBeforeCount = QueueData.Instance.ItemCount;

				testRunner.Type(queueItemPath);
				testRunner.Delay(1);
				testRunner.Type("{Enter}");
				testRunner.Delay(2);
				Assert.IsTrue(testRunner.WaitForName("Queue Item Fennec_Fox", 2));

				int queueAfterCount = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueAfterCount == queueBeforeCount + 1);

				return Task.CompletedTask;
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test]
		public async Task AddToQueueMenuItemAddsMultipleFiles()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Delay(1);
				testRunner.ClickByName("Add File To Queue Menu Item");
				testRunner.Delay(2);

				string queueItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");

				string pathToSecondQueueItem = MatterControlUtilities.GetTestItemPath("Batman.stl");
				string textForBothQueueItems = string.Format("\"{0}\" \"{1}\"", queueItemPath, pathToSecondQueueItem);

				int queueBeforeAddCount = QueueData.Instance.ItemCount;

				testRunner.Type(textForBothQueueItems);
				testRunner.Delay(2);
				testRunner.Type("{Enter}");
				testRunner.Delay(2);
				Assert.IsTrue(testRunner.WaitForName("Queue Item Fennec_Fox", 2));
				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman", 2));

				int queueAfterAddCount = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueAfterAddCount == queueBeforeAddCount + 2);

				return Task.CompletedTask;
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test]
		public async Task AddToQueueMenuItemAddsZipFiles()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Delay(1);
				testRunner.ClickByName("Add File To Queue Menu Item");
				testRunner.Delay(2);

				int beforeCount = QueueData.Instance.ItemCount;

				string pathToType = MatterControlUtilities.GetTestItemPath("Batman.zip");
				testRunner.Type(pathToType);
				testRunner.Delay(1);
				testRunner.Type("{Enter}");
				testRunner.Delay(1);

				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman", 1));
				Assert.IsTrue(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 1));
				Assert.IsTrue(QueueData.Instance.ItemCount == beforeCount + 2);

				return Task.CompletedTask;
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}
