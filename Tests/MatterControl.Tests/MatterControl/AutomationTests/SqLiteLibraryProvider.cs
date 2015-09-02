using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;
using System;
using System.IO;

namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
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
					testRunner.ClickByName("Library Tab", secondsToWait: 5);

					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

					resultsHarness.AddTestResult(testRunner.ClickByName("3D View Edit", secondsToWait: 3));

					resultsHarness.AddTestResult(testRunner.ClickByName("3D View Copy", secondsToWait: 3));
					resultsHarness.AddTestResult(testRunner.ClickByName("3D View Delete", secondsToWait: 3));
					resultsHarness.AddTestResult(testRunner.ClickByName("Save As Menu", secondsToWait: 3));
					resultsHarness.AddTestResult(testRunner.ClickByName("Save As Menu Item", secondsToWait: 3));

					testRunner.Wait(1);

					testRunner.Type("Test Part");
					resultsHarness.AddTestResult(MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection"));

					resultsHarness.AddTestResult(testRunner.ClickByName("Save As Save Button", secondsToWait: 1));

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
			bool showWindow;
			string testDBFolder = "MC_One_Queue_No_Library";
			MatterControlUtilities.DataFolderState staticDataState = MatterControlUtilities.MakeNewStaticDataForTesting(testDBFolder);
			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(out showWindow);
			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, 450);
			MatterControlUtilities.RestoreStaticDataAfterTesting(staticDataState, true);
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 7); // make sure we ran all our tests
		}
	}
}