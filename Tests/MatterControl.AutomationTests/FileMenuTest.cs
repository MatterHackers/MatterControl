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
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Delay(1);
				testRunner.ClickByName("Add File To Queue Menu Item");
				testRunner.Delay(2);

				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.Type(MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");
				testRunner.Delay(2);

				testRunner.NavigateToFolder("Print Queue Row Item Collection");

				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec_Fox"));
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by one after adding Fennec part");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task AddToQueueMenuItemAddsMultipleFiles()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Delay(1);
				testRunner.ClickByName("Add File To Queue Menu Item");
				testRunner.Delay(2);

				int expectedCount = QueueData.Instance.ItemCount + 2;

				testRunner.Type(
					string.Format("\"{0}\" \"{1}\"",
						MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"),
						MatterControlUtilities.GetTestItemPath("Batman.stl")));

				testRunner.Delay(2);
				testRunner.Type("{Enter}");
				testRunner.Delay(2);

				testRunner.NavigateToFolder("Print Queue Row Item Collection");

				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec_Fox", 2));
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman", 2));

				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by two after adding Fennec and Batman parts");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task AddToQueueMenuItemAddsZipFiles()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Delay(1);
				testRunner.ClickByName("Add File To Queue Menu Item");
				testRunner.Delay(2);

				int expectedCount = QueueData.Instance.ItemCount + 2;

				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.zip"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");
				testRunner.Delay(1);

				testRunner.NavigateToFolder("Print Queue Row Item Collection");

				Assert.IsTrue(testRunner.WaitForName("Row Item Batman", 1));
				Assert.IsTrue(testRunner.WaitForName("Row Item 2013-01-25_Mouthpiece_v2", 1));

				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by two after adding contents of Batmap.zip");

				return Task.CompletedTask;
			});
		}
	}
}
