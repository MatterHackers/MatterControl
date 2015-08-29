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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.DataStorage;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI")]
	public class MatterControlUITests
	{
		private static bool saveImagesForDebug = true;

		private void RemoveAllFromQueue(AutomationRunner testRunner)
		{
			Assert.IsTrue(testRunner.ClickByName("Queue... Menu", secondsToWait: 2));
			Assert.IsTrue(testRunner.ClickByName(" Remove All Menu Item", secondsToWait: 2));
		}

		public static string DefaultTestImages
		{
			get
			{
				return Path.Combine("..", "..", "..", "TestData", "TestImages");
			}
		}

		public static void CloseMatterControl(AutomationRunner testRunner)
		{
			SystemWindow mcWindowLocal = MatterControlApplication.Instance;
			Assert.IsTrue(testRunner.ClickByName("File Menu", secondsToWait: 2));
			Assert.IsTrue(testRunner.ClickByName("Exit Menu Item", secondsToWait: 2));
			testRunner.Wait(.2);
			if (mcWindowLocal.Parent != null)
			{
				mcWindowLocal.CloseOnIdle();
			}
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void CreateFolderStarsOutWithTextFiledFocusedAndEditable()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUITests.DefaultTestImages);

				// Now do the actions specific to this test. (replace this for new tests)
				{
					testRunner.ClickByName("Library Tab");
					testRunner.ClickByName("Create Folder Button");

					testRunner.Wait(.5);
					testRunner.Type("Test Text");
					testRunner.Wait(.5);

					SystemWindow containingWindow;
					GuiWidget textInputWidget = testRunner.GetWidgetByName("Create Folder - Text Input", out containingWindow);
					MHTextEditWidget textWidgetMH = textInputWidget as MHTextEditWidget;
					resultsHarness.AddTestResult(textWidgetMH != null, "Found Text Widget");
					resultsHarness.AddTestResult(textWidgetMH.Text == "Test Text", "Had the right text");
					containingWindow.CloseOnIdle();
					testRunner.Wait(.5);

					CloseMatterControl(testRunner);
				}
			};

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
			bool showWindow;
			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(out showWindow);
			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, 10);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
		}

		/// <summary>
		/// Bug #94150618 - Left margin is not applied on GuiWidget
		/// </summary>
		[Test]
		public void TopToBottomContainerAppliesExpectedMarginToToggleView()
		{
			int marginSize = 40;
			int dimensions = 300;

			GuiWidget outerContainer = new GuiWidget(dimensions, dimensions);

			FlowLayoutWidget topToBottomContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
			};
			outerContainer.AddChild(topToBottomContainer);

			CheckBox toggleBox = ImageButtonFactory.CreateToggleSwitch(true);
			toggleBox.HAnchor = HAnchor.ParentLeftRight;
			toggleBox.VAnchor = VAnchor.ParentBottomTop;
			toggleBox.Margin = new BorderDouble(marginSize);
			toggleBox.BackgroundColor = RGBA_Bytes.Red;
			toggleBox.DebugShowBounds = true;

			topToBottomContainer.AddChild(toggleBox);
			topToBottomContainer.AnchorAll();
			topToBottomContainer.PerformLayout();

			outerContainer.DoubleBuffer = true;
			outerContainer.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
			outerContainer.OnDraw(outerContainer.NewGraphics2D());

			// For troubleshooting or visual validation
			OutputImages(outerContainer, outerContainer);

			var bounds = toggleBox.BoundsRelativeToParent;
			Assert.IsTrue(bounds.Left == marginSize, "Left margin is incorrect");
			Assert.IsTrue(bounds.Right == dimensions - marginSize, "Right margin is incorrect");
			Assert.IsTrue(bounds.Top == dimensions - marginSize, "Top margin is incorrect");
			Assert.IsTrue(bounds.Bottom == marginSize, "Bottom margin is incorrect");
		}

		private void OutputImage(ImageBuffer imageToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				ImageTgaIO.Save(imageToOutput, fileName);
			}
		}

		private void OutputImage(GuiWidget widgetToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				OutputImage(widgetToOutput.BackBuffer, fileName);
			}
		}

		private void OutputImages(GuiWidget control, GuiWidget test)
		{
			OutputImage(control, "image-control.tga");
			OutputImage(test, "image-test.tga");
		}

		public class DataFolderState
		{
			internal bool undoDataRename;
			internal string userDataPath;
			internal string renamedUserDataPath;
		}

		public static DataFolderState MakeNewStaticDataForTesting(string testDBFolderName = null)
		{
			DataFolderState state = new DataFolderState();
			state.userDataPath = MatterHackers.MatterControl.DataStorage.ApplicationDataStorage.ApplicationUserDataPath;
			state.renamedUserDataPath = Path.Combine(Path.GetDirectoryName(state.userDataPath), "-MatterControl");

			int testCount = 0;
			while (Directory.Exists(state.renamedUserDataPath + testCount.ToString()))
			{
				testCount++;
			}

			state.renamedUserDataPath = state.renamedUserDataPath + testCount.ToString();

			state.undoDataRename = false;
			if (Directory.Exists(state.userDataPath))
			{
				Directory.Move(state.userDataPath, state.renamedUserDataPath);
				state.undoDataRename = true;
			}

			if (testDBFolderName != null)
			{
				string fullPathToDataContents = Path.Combine("..", "..", "..", "TestData", "TestDatabaseStates", testDBFolderName);
				CopyTestDataDBFolderToTemporaryMCAppDataDirectory(fullPathToDataContents);
				state.undoDataRename = true;
				return state;
			}

			Datastore.Instance.Initialize();

			return state;
		}

		public static void RestoreStaticDataAfterTesting(DataFolderState state, bool closeDataBase)
		{
			if (state.undoDataRename)
			{
				Thread.Sleep(500);
				if (closeDataBase)
				{
					Datastore.Instance.Exit();
				}
				Directory.Delete(state.userDataPath, true);
				Stopwatch time = Stopwatch.StartNew();
				// Wait for up to some amount of time for the directory to be gone.
				while (Directory.Exists(state.userDataPath)
					&& time.ElapsedMilliseconds < 100)
				{
					Thread.Sleep(1); // make sure we are not eating all the cpu time.
				}
				Directory.Move(state.renamedUserDataPath, state.userDataPath);
			}
		}

		public static void CopyTestDataDBFolderToTemporaryMCAppDataDirectory(string testDataDBDirectory)
		{
			string matterControlAppDataFolder = MatterHackers.MatterControl.DataStorage.ApplicationDataStorage.ApplicationUserDataPath;
			
			foreach(string folder in Directory.GetDirectories(testDataDBDirectory, "*", SearchOption.AllDirectories))
			{
				string directoryToCopyFilesTo = folder.Replace(testDataDBDirectory, matterControlAppDataFolder);
				Directory.CreateDirectory(directoryToCopyFilesTo);
			}

			foreach(string fileName in Directory.GetFiles(testDataDBDirectory, "*", SearchOption.AllDirectories))
			{
				string newFileFullName = fileName.Replace(testDataDBDirectory, matterControlAppDataFolder);
				File.Copy(fileName, newFileFullName, true);
			}

		}

	}
}