using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;
using System;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class FileMenuTest
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void FileMenuAddPrinter()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					testRunner.ClickByName("File Menu");
					testRunner.Wait(1);
					testRunner.ClickByName("Add Printer Menu Item");
					testRunner.Wait(1);
					resultsHarness.AddTestResult(testRunner.WaitForName("Select Make", 3));

					testRunner.ClickByName("Cancel Wizard Button");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);

			Assert.IsTrue(testHarness.AllTestsPassed(1));
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void AddToQueueMenuItemAddsSingleFile()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					testRunner.ClickByName("File Menu");
					testRunner.Wait(1);
					testRunner.ClickByName("Add File To Queue Menu Item");
					testRunner.Wait(2);

					string queueItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");

					int queueBeforeCount = QueueData.Instance.Count;

					testRunner.Type(queueItemPath);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");
					testRunner.Wait(2);
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item Fennec_Fox", 2));

					int queueAfterCount = QueueData.Instance.Count;

					resultsHarness.AddTestResult(queueAfterCount == queueBeforeCount + 1);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void AddToQueueMenuItemAddsMultipleFiles()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					testRunner.ClickByName("File Menu");
					testRunner.Wait(1);
					testRunner.ClickByName("Add File To Queue Menu Item");
					testRunner.Wait(2);

					string queueItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");

					string pathToSecondQueueItem = MatterControlUtilities.GetTestItemPath("Batman.stl");
					string textForBothQueueItems = String.Format("\"{0}\" \"{1}\"", queueItemPath, pathToSecondQueueItem);

					int queueBeforeAddCount = QueueData.Instance.Count;

					testRunner.Type(textForBothQueueItems);
					testRunner.Wait(2);
					testRunner.Type("{Enter}");
					testRunner.Wait(2);
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item Fennec_Fox", 2));
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item Batman", 2));
					
					int queueAfterAddCount = QueueData.Instance.Count;

					resultsHarness.AddTestResult(queueAfterAddCount == queueBeforeAddCount + 2);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void AddToQueueMenuItemAddsZipFiles()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					testRunner.ClickByName("File Menu");
					testRunner.Wait(1);
					testRunner.ClickByName("Add File To Queue Menu Item");
					testRunner.Wait(2);

					int beforeCount = QueueData.Instance.Count;

					string pathToType = MatterControlUtilities.GetTestItemPath("Batman.zip");
					testRunner.Type(pathToType);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");
					testRunner.Wait(1);

					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item Batman", 1));
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 1));
					resultsHarness.AddTestResult(QueueData.Instance.Count == beforeCount + 2);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}
	}
}
