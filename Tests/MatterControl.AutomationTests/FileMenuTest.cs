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
					resultsHarness.AddTestResult(testRunner.WaitForName("Printer Connection Window", 3));

					testRunner.ClickByName("Setup Connection Cancel Button");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 1); // make sure we ran all our tests
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

					testRunner.Type(queueItemPath);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");
					testRunner.Wait(2);
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item Fennec_Fox", 2));

					int queueCount = QueueData.Instance.Count;

					resultsHarness.AddTestResult(queueCount == 1);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
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

					testRunner.Type(textForBothQueueItems);
					testRunner.Wait(2);
					testRunner.Type("{Enter}");
					testRunner.Wait(2);
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item Fennec_Fox", 2));
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item Batman", 2));
					
					int queueCount = QueueData.Instance.Count;

					resultsHarness.AddTestResult(queueCount == 2);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 3); // make sure we ran all our tests
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

					string pathToType = MatterControlUtilities.GetTestItemPath("Batman.zip");
					testRunner.Type(pathToType);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");
					testRunner.Wait(1);


					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item Batman", 1));
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 1));
					resultsHarness.AddTestResult(QueueData.Instance.Count == 2);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 3); // make sure we ran all our tests
		}
	}
}
