/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.PlatformAbstract;
using System.IO;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.Agg.UI.Tests;

namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class BuyButtonTests
	{
		[Test, RequiresSTA, RunInApplicationDomain, Ignore("Not Finished")]
		public void ClickOnBuyButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					//Make sure image does not exist before we click the buy button
					testRunner.MatchLimit = 500000;
					bool imageExists = testRunner.ImageExists("MatterHackersStoreImage.png");
					resultsHarness.AddTestResult(imageExists == false, "Web page is not open");

					//Click Buy button and test that the MatterHackers store web page is open
					testRunner.ClickByName("Buy Materials Button", secondsToWait: 5);
					bool imageExists2 = testRunner.ImageExists("MatterHackersStoreImage.png", 10);
					resultsHarness.AddTestResult(imageExists2 == true, "Web page is open");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

#if !__ANDROID__
				// Set the static data to point to the directory of MatterControl
				StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
				bool showWindow;
				MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(out showWindow);
				AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, 60);

				Assert.IsTrue(testHarness.AllTestsPassed);
				Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class CreateButtonTest
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		//Test Works
		public void ClickOnCreateButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					//Make sure that plugin window does not exist
					bool pluginWindowExists1 = testRunner.WaitForName("Plugin Chooser Window", 0);
					resultsHarness.AddTestResult(pluginWindowExists1 == false, "Plugin window does not exist");

					testRunner.ClickByName("Design Tool Button", 5);

					//Test that the plugin window does exist after the create button is clicked
					SystemWindow containingWindow;
					GuiWidget pluginWindowExists = testRunner.GetWidgetByName("Plugin Chooser Window", out containingWindow, secondsToWait: 3);
					resultsHarness.AddTestResult(pluginWindowExists != null, "Plugin Chooser Window");
					pluginWindowExists.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
			bool showWindow;
			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(out showWindow);
			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, 60);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests

		}
	}


	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class ExportButtonTest
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		//Test Works
		public void ClickOnExportButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					//Make sure that the export window does not exist
					bool exportWindowExists1 = testRunner.WaitForName( "Export Window Queue", 0);
					resultsHarness.AddTestResult(exportWindowExists1 == false, "Export window does not exist");

					testRunner.ClickByName("Export Queue Button", 5);
					SystemWindow containingWindow;
					GuiWidget exportWindow = testRunner.GetWidgetByName("Export Window Queue", out containingWindow, secondsToWait: 5);
					resultsHarness.AddTestResult(exportWindow != null, "Export window does exist");
					exportWindow.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
			bool showWindow;
			string testDBFolder = "MC_Three_Queue_Items";
			MatterControlUtilities.DataFolderState staticDataState = MatterControlUtilities.MakeNewStaticDataForTesting(testDBFolder);
			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(out showWindow);
			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, 60);
			MatterControlUtilities.RestoreStaticDataAfterTesting(staticDataState, true);
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests

		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class ExportButtonDisabledNoQueueItems
	{
		[Test, RequiresSTA, RunInApplicationDomain, Ignore("Not Finished")]
		public void ExportButtonIsDisabledWithNoItemsInQueue()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					
					//bool exportButtonExists = testRunner.NameExists("Export Queue Button");
					bool exportButtonExists = testRunner.WaitForName("Export Queue Button", 10);
					testRunner.Wait(5);
					resultsHarness.AddTestResult(exportButtonExists == false, "Export button is disabled");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
			bool showWindow;
			string testDBFolder = "MC_Fresh_Installation";
			MatterControlUtilities.DataFolderState staticDataState = MatterControlUtilities.MakeNewStaticDataForTesting(testDBFolder);
			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(out showWindow);
			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, 300);
			MatterControlUtilities.RestoreStaticDataAfterTesting(staticDataState, true);
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 1); // make sure we ran all our tests
		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class QueueItemThumnailWidget
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void QueueThumbnailWidgetOpensPartPreview()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					bool partPreviewWindowExists1 = testRunner.WaitForName("Part Preview Window Thumbnail", 0);
					resultsHarness.AddTestResult(partPreviewWindowExists1 == false, "Part Preview Window Does Not Exist");

					testRunner.ClickByName("Queue Item Thumbnail");

					SystemWindow containingWindow;
					GuiWidget partPreviewWindowExists = testRunner.GetWidgetByName("Part Preview Window Thumbnail", out containingWindow, secondsToWait: 3);
					resultsHarness.AddTestResult(partPreviewWindowExists != null, "Part Preview Window Exists");
					partPreviewWindowExists.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
			bool showWindow;
			string testDBFolder = "MC_Three_Queue_Items";
			MatterControlUtilities.DataFolderState staticDataState = MatterControlUtilities.MakeNewStaticDataForTesting(testDBFolder);
			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(out showWindow);
			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, 300);
			MatterControlUtilities.RestoreStaticDataAfterTesting(staticDataState, true);
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
		}
	}

}













