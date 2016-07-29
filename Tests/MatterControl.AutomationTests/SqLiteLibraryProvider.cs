using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;
using System;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class SqLiteLibraryProviderTests
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void LibraryQueueViewRefreshesOnAddItem()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					testRunner.ClickByName("Library Tab", 5);

					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

					resultsHarness.AddTestResult(testRunner.ClickByName("3D View Edit", 3));

					resultsHarness.AddTestResult(testRunner.ClickByName("3D View Copy", 3), "Click Copy");
					// wait for the copy to finish
					testRunner.Wait(.1);
					resultsHarness.AddTestResult(testRunner.ClickByName("3D View Remove", 3), "Click Delete");
					resultsHarness.AddTestResult(testRunner.ClickByName("Save As Menu", 3), "Click Save As Menu");
					resultsHarness.AddTestResult(testRunner.ClickByName("Save As Menu Item", 3), "Click Save As");

					testRunner.Wait(1);

					testRunner.Type("Test Part");
					resultsHarness.AddTestResult(MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection"));

					resultsHarness.AddTestResult(testRunner.ClickByName("Save As Save Button", 1));

					// ensure that it is now in the library folder (that the folder updated)
					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item " + "Test Part", 5), "The part we added should be in the library");

					MatterControlUtilities.CloseMatterControl(testRunner); 
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 8); // make sure we ran all our tests
		}
	}
}