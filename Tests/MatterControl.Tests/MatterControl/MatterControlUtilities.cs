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

using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation")]
	public static class MatterControlUtilities
	{
		private static bool saveImagesForDebug = true;

		private static int testID = 0;

		private static string runName = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss");

		private static void RemoveAllFromQueue(AutomationRunner testRunner)
		{
			Assert.IsTrue(testRunner.ClickByName("Queue... Menu", 2));
			Assert.IsTrue(testRunner.ClickByName(" Remove All Menu Item", 2));
		}

		public static string DefaultTestImages
		{
			get
			{
				return Path.Combine("..", "..", "..", "TestData", "TestImages");
			}
		}

		public static void CreateDownloadsSubFolder()
		{

			Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Temporary"));

		}

		public static string PathToDownloadsSubFolder
		{
			get
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Temporary");
			}
		}

		public static void CleanupDownloadsDirectory(string path)
		{
			Directory.Delete(path, true);
		}


		public static string PathToExportGcodeFolder
		{
			get { return Path.GetFullPath(Path.Combine("..", "..", "..", "..", "Tests", "TestData", "ExportedGcode", runName)); }
		}

		public static string GetTestItemPath(string queueItemToLoad)
		{
			string pathToQueueItemFolder = Path.Combine("..", "..", "..", "..", "Tests", "TestData", "QueueItems");
			return Path.GetFullPath(Path.Combine(pathToQueueItemFolder, queueItemToLoad));
		}

		public static void CloseMatterControl(AutomationRunner testRunner)
		{
			SystemWindow mcWindowLocal = MatterControlApplication.Instance;
			Assert.IsTrue(testRunner.ClickByName("File Menu", 2));
			Assert.IsTrue(testRunner.ClickByName("Exit Menu Item", 2));
			testRunner.Wait(.2);
			if (mcWindowLocal.Parent != null)
			{
				mcWindowLocal.CloseOnIdle();
			}
		}

		public enum PrepAction
		{
			CloseLoginAndPrinterSelect,
		};

		public static void PrepForTestRun(AutomationRunner testRunner, PrepAction preAction = PrepAction.CloseLoginAndPrinterSelect)
		{
			switch (preAction)
			{
				case PrepAction.CloseLoginAndPrinterSelect:
					testRunner.ClickByName("Connection Wizard Skip Sign In Button", 5);
					testRunner.ClickByName("Cancel Wizard Button", 5);
					break;
			}
		}

		public static bool CompareExpectedSliceSettingValueWithActualVaue(string sliceSetting, string expectedValue)
		{
			string tempFolderPath = Path.Combine("..", "..", "..", "..", "Tests", "temp");
			string fullPath = Path.Combine(tempFolderPath, runName, "Test0", "data", "gcode");

			string [] gcodeFiles = Directory.GetFiles(fullPath);

			foreach (string file in gcodeFiles)
			{
				if(file.Contains(".ini"))
				{

					FileInfo f = new FileInfo(file);
					string fullName = f.FullName;
					string[] lines = File.ReadAllLines(fullName);
					foreach (string line in lines)
					{

						if (line.Contains(sliceSetting))
						{
							line.Trim(' ');
							string[] settingNameAndValue = line.Split('=');
							string settingName = settingNameAndValue[0].Trim();
							string settingValue = string.Empty;
							if (settingNameAndValue.Length == 2)
							{
								settingValue = settingNameAndValue[1].Trim();
							}

							if(settingValue == expectedValue)
							{
								return true;
							}
						}
					}
				}
			}

			return false;
		}

		public static void SelectAndAddPrinter(AutomationRunner testRunner, string make, string model, bool firstAdd)
		{

			string manufacturer = make + " Menu Item";
			string printer = model + " Menu Item";
			string printerProfile = String.Format("{0} {1} Profile", make, model);


			testRunner.ClickByName("Select a Printer Button");
			testRunner.Wait(1);

			if (!firstAdd)
			{
				testRunner.ClickByName("Add new printer button");
				testRunner.Wait(1);
			}

			testRunner.ClickByName("Select Make");
			testRunner.Wait(1);

			testRunner.ClickByName(manufacturer);
			testRunner.Wait(1);

			testRunner.ClickByName("Select Model");
			testRunner.Wait(1);

			testRunner.ClickByName(printer);
			testRunner.Wait(1);

			testRunner.ClickByName("Save & Continue Button");
			testRunner.Wait(1);

			testRunner.ClickByName("Setup Connection Cancel Button");
			testRunner.Wait(2);

			testRunner.ClickByName(printerProfile);
			testRunner.Wait(1);
		}


		private static void OutputImage(ImageBuffer imageToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				ImageTgaIO.Save(imageToOutput, fileName);
			}
		}

		private static void OutputImage(GuiWidget widgetToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				OutputImage(widgetToOutput.BackBuffer, fileName);
			}
		}

		private static void OutputImages(GuiWidget control, GuiWidget test)
		{
			OutputImage(control, "image-control.tga");
			OutputImage(test, "image-test.tga");
		}

		/// <summary>
		/// Overrides the AppData location, ensuring each test starts with a fresh MatterControl database.
		/// </summary>
		public static void OverrideAppDataLocation()
		{
			string tempFolderPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "Tests","temp"));

			ApplicationDataStorage.Instance.OverrideAppDataLocation(
				Path.Combine(tempFolderPath, runName, $"Test{testID++}"));
		}

		public static void AddItemsToQueue(string queueItemFolderToLoad)
		{

			//DEFAULT LOCATION OF MCP FILE (LOCATION IS CORRECT)
			string mcpPath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "default.mcp");

			Directory.CreateDirectory(Path.GetDirectoryName(mcpPath));

			if (!File.Exists(mcpPath))
			{
				File.WriteAllText(mcpPath, JsonConvert.SerializeObject(new ManifestFile()
					{
						ProjectFiles = new System.Collections.Generic.List<PrintItem>()
					}, Formatting.Indented));
			}

			var queueItemData = JsonConvert.DeserializeObject<ManifestFile>(File.ReadAllText(mcpPath));

			string queueData = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "testitems");

			//CREATE EMPTY TESTPARTS FOLDER
			Directory.CreateDirectory(queueData);

			string queueItemTestDataFolder = Path.Combine("..", "..", "..", "TestData", "QueueItems");

			foreach (string file in Directory.GetFiles(Path.Combine(queueItemTestDataFolder, queueItemFolderToLoad)))
			{
				string newFilePath = Path.Combine(queueData, Path.GetFileName(file));
				File.Copy(file, newFilePath, true);
				queueItemData.ProjectFiles.Add(new PrintItem()
					{
						FileLocation = newFilePath,
						Name = Path.GetFileNameWithoutExtension(file),
						DateAdded = DateTime.Now
					});
			}

			File.WriteAllText(mcpPath, JsonConvert.SerializeObject(queueItemData, Formatting.Indented));

			Assert.IsTrue(queueItemData != null && queueItemData.ProjectFiles.Count > 0);

		}

		public static LibraryProvider CurrentProvider()
		{
			return ApplicationController.Instance.CurrentLibraryDataView.CurrentLibraryProvider;
		}

		public static bool NavigateToFolder(AutomationRunner testRunner, string libraryRowItemName)
		{
			bool goodNavigate = true;
			SearchRegion libraryRowItemRegion = testRunner.GetRegionByName(libraryRowItemName, 3);
			goodNavigate &= testRunner.ClickByName(libraryRowItemName);
			goodNavigate &= testRunner.MoveToByName(libraryRowItemName);
			testRunner.Wait(.5);
			goodNavigate &= testRunner.ClickByName("Open Collection", searchRegion: libraryRowItemRegion);
			testRunner.Wait(.5);

			return goodNavigate;
		}

		public static AutomationTesterHarness RunTest(
			Action<AutomationTesterHarness> testToRun, 
			string staticDataPathOverride = null, 
			double maxTimeToRun = 60, 
			QueueTemplate queueItemFolderToAdd = QueueTemplate.None)
		{
			// Walk back a step in the stack and output the callers name
			StackTrace st = new StackTrace(false);
			Console.WriteLine("\r\nRunning automation test: " + st.GetFrames().Skip(1).First().GetMethod().Name);

			if (staticDataPathOverride == null)
			{
				staticDataPathOverride = Path.Combine("..", "..", "..", "..", "StaticData");
			}

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(staticDataPathOverride);
#endif
			MatterControlUtilities.OverrideAppDataLocation();

			if (queueItemFolderToAdd != QueueTemplate.None)
			{
				string queueTemplateDirectory = queueItemFolderToAdd.ToString();
				MatterControlUtilities.AddItemsToQueue(queueTemplateDirectory);
			}

			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance();
			return AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, maxTimeToRun);
		}
	}

	/// <summary>
	/// Represents a queue template folder on disk (located at Tests/TestData/QueueItems) that should be synced into the default
	/// queue during test init. The enum name and folder name *must* be the same in order to function
	/// </summary>
	public enum QueueTemplate
	{
		None,
		Three_Queue_Items
	}
}