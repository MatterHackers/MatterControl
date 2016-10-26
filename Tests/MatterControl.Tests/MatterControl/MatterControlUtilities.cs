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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NUnit.Framework;

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

		public static void CreateDownloadsSubFolder()
		{
			Directory.CreateDirectory(PathToDownloadsSubFolder);
		}

		public static string PathToDownloadsSubFolder
		{
			get
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "-Temporary");
			}
		}

		public static void DeleteDownloadsSubFolder()
		{
			Directory.Delete(PathToDownloadsSubFolder, true);
		}

		public static void SignOut(AutomationRunner testRunner)
		{
			testRunner.ClickByName("User Options Menu", 2);
			testRunner.ClickByName("Sign Out Menu Item", 2);
			testRunner.Wait(.5);

			testRunner.ClickByName("Yes Button");
			testRunner.Wait(5);
		}

		public static string PathToExportGcodeFolder
		{
			get { return TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "ExportedGcode", runName); }
		}

		public static string GetTestItemPath(string queueItemToLoad)
		{
			return TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "QueueItems", queueItemToLoad);
		}

		private static void CloseMatterControlViaMenu(AutomationRunner testRunner)
		{
			SystemWindow mcWindowLocal = MatterControlApplication.Instance;
			testRunner.ClickByName("File Menu", 5);
			testRunner.ClickByName("Exit Menu Item", 5);

			testRunner.Wait(.2);
			if (mcWindowLocal.Parent != null)
			{
				mcWindowLocal.CloseOnIdle();
			}
		}

		public enum PrepAction
		{
			CloseSignInAndPrinterSelect,
		};

		public static void PrepForTestRun(AutomationRunner testRunner, PrepAction preAction = PrepAction.CloseSignInAndPrinterSelect)
		{
			switch (preAction)
			{
				case PrepAction.CloseSignInAndPrinterSelect:

					// Non-MCCentral builds won't have the plugin. Reduce the wait time for these cases
					if (testRunner.WaitForName("Connection Wizard Skip Sign In Button", 0.5))
					{
						testRunner.ClickByName("Connection Wizard Skip Sign In Button");
					}

					testRunner.ClickByName("Cancel Wizard Button", 5);
					break;
			}
		}

		public class PrintEmulatorProcess: Process
		{
			protected override void Dispose(bool disposing)
			{
				this.Kill();
				base.Dispose(disposing);
			}
		}

		public static Process LaunchAndConnectToPrinterEmulator(this AutomationRunner testRunner, string make = "Airwolf 3D", string model = "HD", bool runSlow = false)
		{
			// Load the TestEnv config
			var config = TestAutomationConfig.Load();

			// Create the printer
			MatterControlUtilities.AddAndSelectPrinter(testRunner, make, model);

			var process = new PrintEmulatorProcess();
			process.StartInfo = new ProcessStartInfo()
			{
				FileName = "python",
				Arguments = string.Format("{0} {1}{2}", 
					StaticData.Instance.MapPath("../PrinterEmulator.py"), 
					config.Printer, 
					runSlow ? " slow" : ""),

				WindowStyle = ProcessWindowStyle.Minimized
			};

			process.Start();

			// edit the com port
			testRunner.ClickByName("Edit Printer Button");
			testRunner.Wait(2);

			testRunner.ClickByName("Com Port Dropdown");

			testRunner.ClickByName(config.MCPort + " Menu Item", 1);

			testRunner.ClickByName("Cancel Wizard Button");

			// connect to the created printer
			testRunner.ClickByName("Connect to printer button", 2);

			testRunner.Wait(2);

			return process;
		}

		public static bool CompareExpectedSliceSettingValueWithActualVaue(string sliceSetting, string expectedValue)
		{
			string fullPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "temp", runName, "Test0", "data", "gcode");

			foreach (string file in Directory.GetFiles(fullPath))
			{
				if (file.Contains(".ini"))
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

							if (settingValue == expectedValue)
							{
								return true;
							}
						}
					}
				}
			}

			return false;
		}

		public static void DeleteSelectedPrinter(AutomationRunner testRunner)
		{
			// delete printer
			testRunner.ClickByName("Edit Printer Button", 5);
			testRunner.Wait(.5);
			testRunner.ClickByName("Delete Printer Button", 5);
			testRunner.Wait(.5);
			testRunner.ClickByName("Yes Button", 5);
			testRunner.Wait(2);
		}

		public static void AddAndSelectPrinter(AutomationRunner testRunner, string make, string model)
		{
			string manufacturer = make + " Menu Item";
			string printer = model + " Menu Item";
			string printerProfile = String.Format("{0} {1} Profile", make, model);

			testRunner.ClickByName("Printers... Menu", 2);

			testRunner.ClickByName("Add New Printer... Menu Item", 2);

			testRunner.ClickByName("Connection Wizard Skip Sign In Button", 2);

			testRunner.ClickByName("Select Make", 2);

			testRunner.Wait(.2);
			testRunner.Type(make);
			testRunner.Type("{Enter}");

			testRunner.ClickByName("Select Model", 2);
			testRunner.ClickByName(printer, 2);

			testRunner.ClickByName("Save & Continue Button", 2);
			testRunner.Wait(2);

			testRunner.ClickByName("Cancel Wizard Button", 2);
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
		public static void OverrideAppDataLocation(string matterControlDirectory)
		{
			string tempFolderPath = Path.Combine(matterControlDirectory, "Tests", "temp", runName, $"Test{testID++}");
			ApplicationDataStorage.Instance.OverrideAppDataLocation(tempFolderPath);
		}

		public static void AddItemsToQueue(string queueItemFolderToLoad)
		{
			// Default location of mcp file
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

			// Create empty TestParts folder
			Directory.CreateDirectory(queueData);

			string queueItemsDirectory = TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl", "Tests", "TestData", "QueueItems", queueItemFolderToLoad);

			foreach (string file in Directory.GetFiles(queueItemsDirectory))
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

		public static bool NavigateToFolder(this AutomationRunner testRunner, string libraryRowItemName)
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

		public static async Task RunTest(
			AutomationTest testMethod,
			string staticDataPathOverride = null,
			double maxTimeToRun = 60,
			QueueTemplate queueItemFolderToAdd = QueueTemplate.None,
			int overrideWidth = -1, 
			int overrideHeight = -1,
			string defaultTestImages = null)
		{
			// Walk back a step in the stack and output the callers name
			StackTrace st = new StackTrace(false);
			Debug.WriteLine("\r\n ***** Running automation test: {0} {1} ", st.GetFrames().Skip(1).First().GetMethod().Name, DateTime.Now);

			if (staticDataPathOverride == null)
			{
				// Popping one directory above MatterControl, then back down into MatterControl ensures this works in MCCentral as well and MatterControl
				staticDataPathOverride = TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl", "StaticData");
			}

#if DEBUG
			string outputDirectory = "Debug";
#else
			string outputDirectory = "Release";
#endif

			Environment.CurrentDirectory = TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl", "bin", outputDirectory); 

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new FileSystemStaticData(staticDataPathOverride);
#endif
			// Popping one directory above MatterControl, then back down into MatterControl ensures this works in MCCentral as well and MatterControl
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl"));

			if (queueItemFolderToAdd != QueueTemplate.None)
			{
				string queueTemplateDirectory = queueItemFolderToAdd.ToString();
				MatterControlUtilities.AddItemsToQueue(queueTemplateDirectory);
			}

			if (defaultTestImages == null)
			{
				defaultTestImages = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "TestImages");
			}

			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(overrideWidth, overrideHeight);

			var config = TestAutomationConfig.Load();

			await AutomationRunner.ShowWindowAndExecuteTests(matterControlWindow, testMethod, maxTimeToRun, defaultTestImages, config.AutomationInputType);
		}

		public static void LibraryAddSelectionToQueue(AutomationRunner testRunner)
		{
			testRunner.ClickByName("LibraryActionMenu");
			testRunner.ClickByName("Add to Queue Menu Item", 1);
		}

		public static void LibraryEditSelectedItem(AutomationRunner testRunner)
		{
			testRunner.ClickByName("LibraryActionMenu");
			testRunner.ClickByName("Edit Menu Item", 1);
		}

		public static void LibraryRenameSelectedItem(AutomationRunner testRunner)
		{
			testRunner.ClickByName("LibraryActionMenu");
			testRunner.ClickByName("Rename Menu Item", 1);
		}

		public static void LibraryRemoveSelectedItem(AutomationRunner testRunner)
		{
			testRunner.ClickByName("LibraryActionMenu");
			testRunner.ClickByName("Remove Menu Item", 1);
		}
		public static string ResolveProjectPath(this TestContext context, int stepsToProjectRoot, params string[] relativePathSteps)
		{
			string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			var allPathSteps = new List<string> { assemblyPath };
			allPathSteps.AddRange(Enumerable.Repeat("..", stepsToProjectRoot));

			if (relativePathSteps.Any())
			{
				allPathSteps.AddRange(relativePathSteps);
			}

			return Path.GetFullPath(Path.Combine(allPathSteps.ToArray()));
		}

		/// <summary>
		/// Set the working directory to the location of the executing assembly. This is essentially the Nunit2 behavior
		/// </summary>
		/// <param name="context"></param>
		public static void SetCompatibleWorkingDirectory(this TestContext context)
		{
			Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		}

		public static void SwitchToAdvancedSettings(AutomationRunner testRunner)
		{
			if (testRunner.NameExists("SettingsAndControls"))
			{
				testRunner.ClickByName("SettingsAndControls", 1);
				testRunner.Wait(.5);
			}
			testRunner.ClickByName("User Level Dropdown", 1);
			testRunner.ClickByName("Advanced Menu Item", 1);
			testRunner.Wait(.5);
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

	public class TestAutomationConfig
	{
		private static readonly string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MHTest.config");

		/// <summary>
		/// The ClientToken used by tests to emulate an external client
		/// </summary>
		public string TestEnvClientToken { get; set; }

		/// <summary>
		/// The serial port that MatterControl will communicate with for the Com0Com connection
		/// </summary>
		public string MCPort { get; set; }

		/// <summary>
		/// The serial port that Python will communicate with to emulate printer firmware
		/// </summary>
		public string Printer { get; set; }

		[JsonConverter(typeof(StringEnumConverter))]
		public AutomationRunner.InputType AutomationInputType { get; set; } = AutomationRunner.InputType.Native;

		public static TestAutomationConfig Load()
		{
			TestAutomationConfig config = null;

			if (!File.Exists(configPath))
			{
				config = new TestAutomationConfig();
				config.Save();
			}
			else
			{
				config = JsonConvert.DeserializeObject<TestAutomationConfig>(File.ReadAllText(configPath));
			}

			// if no com port set, issue instructions on how to set it
			if (string.IsNullOrEmpty(config.MCPort) || string.IsNullOrEmpty(config.Printer))
			{
				throw new Exception("You must set the port and printer in: " + configPath);
			}

			return config;
		}

		/// <summary>
		/// Persist the current settings to the 'MHTest.config' in the user profile - %userprofile%\MHTest.config
		/// </summary>
		public void Save()
		{
			File.WriteAllText(configPath, JsonConvert.SerializeObject(this, Formatting.Indented));
		}
	}
}