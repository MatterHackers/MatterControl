using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class FileMenuTest
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task FileMenuAddPrinter()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Wait(1);
				testRunner.ClickByName("Add Printer Menu Item");
				testRunner.Wait(1);
				Assert.IsTrue(testRunner.WaitForName("Select Make", 3));

				testRunner.ClickByName("Cancel Wizard Button");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task AddToQueueMenuItemAddsSingleFile()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Wait(1);
				testRunner.ClickByName("Add File To Queue Menu Item");
				testRunner.Wait(2);

				string queueItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");

				int queueBeforeCount = QueueData.Instance.ItemCount;

				testRunner.Type(queueItemPath);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");
				testRunner.Wait(2);
				Assert.IsTrue(testRunner.WaitForName("Queue Item Fennec_Fox", 2));

				int queueAfterCount = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueAfterCount == queueBeforeCount + 1);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task AddToQueueMenuItemAddsMultipleFiles()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Wait(1);
				testRunner.ClickByName("Add File To Queue Menu Item");
				testRunner.Wait(2);

				string queueItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");

				string pathToSecondQueueItem = MatterControlUtilities.GetTestItemPath("Batman.stl");
				string textForBothQueueItems = string.Format("\"{0}\" \"{1}\"", queueItemPath, pathToSecondQueueItem);

				int queueBeforeAddCount = QueueData.Instance.ItemCount;

				testRunner.Type(textForBothQueueItems);
				testRunner.Wait(2);
				testRunner.Type("{Enter}");
				testRunner.Wait(2);
				Assert.IsTrue(testRunner.WaitForName("Queue Item Fennec_Fox", 2));
				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman", 2));

				int queueAfterAddCount = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueAfterAddCount == queueBeforeAddCount + 2);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task AddToQueueMenuItemAddsZipFiles()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Wait(1);
				testRunner.ClickByName("Add File To Queue Menu Item");
				testRunner.Wait(2);

				int beforeCount = QueueData.Instance.ItemCount;

				string pathToType = MatterControlUtilities.GetTestItemPath("Batman.zip");
				testRunner.Type(pathToType);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");
				testRunner.Wait(1);

				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman", 1));
				Assert.IsTrue(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 1));
				Assert.IsTrue(QueueData.Instance.ItemCount == beforeCount + 2);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}
