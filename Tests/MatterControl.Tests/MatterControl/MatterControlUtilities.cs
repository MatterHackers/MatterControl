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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PrinterEmulator;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NUnit.Framework;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrinterCommunication.Io;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation")]
	public static class MatterControlUtilities
	{
		private static bool saveImagesForDebug = true;

		private static event EventHandler unregisterEvents;

		private static int testID = 0;

		private static string runName = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss");

		public static string PathToDownloadsSubFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "-Temporary");

		private static SystemWindow rootSystemWindow;

		public static void RemoveAllFromQueue(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Queue... Menu");
			testRunner.Delay(1);
			testRunner.ClickByName(" Remove All Menu Item");
		}

		public static void CreateDownloadsSubFolder()
		{
			if (Directory.Exists(PathToDownloadsSubFolder))
			{
				foreach (string filePath in Directory.GetFiles(PathToDownloadsSubFolder))
				{
					File.Delete(filePath);
				}
			}
			else
			{
				Directory.CreateDirectory(PathToDownloadsSubFolder);
			}
		}
		
		public static void DeleteDownloadsSubFolder()
		{
			Directory.Delete(PathToDownloadsSubFolder, true);
		}

		public static void SignOut(AutomationRunner testRunner)
		{
			testRunner.ClickByName("User Options Menu");
			testRunner.ClickByName("Sign Out Menu Item");
			testRunner.Delay(.5);

			// Rather than waiting a fixed amount of time, we wait for the ReloadAll to complete before returning
			testRunner.WaitForReloadAll(() => testRunner.ClickByName("Yes Button"));
		}

		public static void WaitForReloadAll(this AutomationRunner testRunner, Action reloadAllAction)
		{
			// Wire up a block and release mechanism to wait until the sign in process has completed
			AutoResetEvent resetEvent = new AutoResetEvent(false);
			ApplicationController.Instance.DoneReloadingAll.RegisterEvent((s, e) => resetEvent.Set(), ref unregisterEvents);

			// Start the procedure that begins a ReloadAll event in MatterControl
			reloadAllAction();

			// Wait up to 10 seconds for the DoneReloadingAll event
			resetEvent.WaitOne(10 * 1000);

			// Remove our DoneReloadingAll listener
			unregisterEvents(null, null);

			// Wait for any post DoneReloadingAll code to finish up and return
			testRunner.Delay(.2);
		}

		public static string PathToExportGcodeFolder
		{
			get { return TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "ExportedGcode", runName); }
		}

		public static string GetTestItemPath(string queueItemToLoad)
		{
			return TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "QueueItems", queueItemToLoad);
		}

		public static void CloseMatterControl(this AutomationRunner testRunner)
		{
			rootSystemWindow?.Close();
		}

		public enum PrepAction
		{
			CloseSignInAndPrinterSelect,
		};

		public static void Select3DPart(this AutomationRunner testRunner, string partNameToSelect)
		{
			if (testRunner.NameExists("3D View Edit", .2))
			{
				testRunner.ClickByName("3D View Edit");
			}
			testRunner.DragDropByName("InteractionLayer", "InteractionLayer", offsetDrop: new Agg.Point2D(10, 15), mouseButtons: MouseButtons.Right);

			testRunner.Delay(1);
			testRunner.ClickByName(partNameToSelect);
		}

		public static void CloseSignInAndPrinterSelect(this AutomationRunner testRunner, PrepAction preAction = PrepAction.CloseSignInAndPrinterSelect)
		{
			SystemWindow systemWindow;
			testRunner.GetWidgetByName("View3DWidget", out systemWindow, 10);
			// make sure we wait for MC to be up and running
			testRunner.WaitforDraw(systemWindow);

			// If there is a auth pannel make sure we try and close it
			// Non-MCCentral builds won't have the plugin. Reduce the wait time for these cases
			if (testRunner.WaitForName("Connection Wizard Skip Sign In Button", 1))
			{
				testRunner.ClickByName("Connection Wizard Skip Sign In Button");
			}

			if (testRunner.WaitForName("Cancel Wizard Button", 1))
			{
				testRunner.ClickByName("Cancel Wizard Button");
			}
		}

		public static void ChangeToQueueContainer(this AutomationRunner testRunner)
		{
			testRunner.NavigateToFolder("Print Queue Row Item Collection");
		}

		public class PrintEmulatorProcess : Process
		{
			protected override void Dispose(bool disposing)
			{
				try
				{
					this.Kill();
				}
				catch { }

				base.Dispose(disposing);
			}
		}

		public static Emulator LaunchAndConnectToPrinterEmulator(this AutomationRunner testRunner, string make = "Airwolf 3D", string model = "HD", bool runSlow = false)
		{
			// Load the TestEnv config
			var config = TestAutomationConfig.Load();

			// Override the heat up time
			Emulator.DefaultHeatUpTime = config.HeatupTime;

			// Override the temp stablization time
			WaitForTempStream.WaitAfterReachTempTime = config.TempStabilizationTime;

			// Create the printer
			testRunner.AddAndSelectPrinter(make, model);

			// Force the configured printer to use the emulator driver
			ActiveSliceSettings.Instance.SetValue("driver_type", "Emulator");

			// edit the com port
			testRunner.SwitchToPrinterSettings();

			var serialPortDropDown = testRunner.GetWidgetByName("com_port Field", out _, 1);

			testRunner.WaitFor(() => serialPortDropDown.Enabled); // Wait until the serialPortDropDown is ready to click it. Ensures the printer is loaded.

			testRunner.ClickByName("com_port Field");

			testRunner.ClickByName("Emulator Menu Item");

			// connect to the created printer
			testRunner.ClickByName("Connect to printer button");

			testRunner.WaitForName("Disconnect from printer button");

			// Access through static instance must occur after Connect has occurred and the port has spun up
			Emulator.Instance.RunSlow = runSlow;

			return Emulator.Instance;
		}

		public static void CancelPrint(this AutomationRunner testRunner)
		{
			// TODO: Improve this to more accurately find the print task row and click its Stop button
			testRunner.ClickByName("Stop Task Button");

			if (testRunner.WaitForName("Yes Button", 1))
			{
				testRunner.ClickByName("Yes Button");
			}
		}

		public static void WaitForLayer(this Emulator emulator, PrinterSettings printerSettings, int layerNumber, double secondsToWait = 30)
		{
			var resetEvent = new AutoResetEvent(false);

			var heightAtTargetLayer = printerSettings.GetValue<double>(SettingsKey.layer_height) * layerNumber;

			// Wait for emulator to hit target layer
			emulator.ZPositionChanged += (s, e) =>
			{
				// Wait for print to start, then slow down the emulator and continue. Failing to slow down frequently causes a timing issue where the print
				// finishes before we make it down to 'CloseMatterControlViaUi' and thus no prompt to close appears and the test fails when clicking 'Yes Button'
				if (emulator.ZPosition >= heightAtTargetLayer)
				{
					resetEvent.Set();
				}
			};

			resetEvent.WaitOne((int) (secondsToWait * 1000));
		}

		public static bool CompareExpectedSliceSettingValueWithActualVaue(string sliceSetting, string expectedValue)
		{
			foreach (string iniPath in Directory.GetFiles(ApplicationDataStorage.Instance.GCodeOutputPath, "*.ini"))
			{
				var settings = PrinterSettingsLayer.LoadFromIni(iniPath);

				if (settings.TryGetValue(sliceSetting, out string currentValue))
				{
					return currentValue.Trim() == expectedValue;
				}
			}

			return false;
		}

		public static void DeleteSelectedPrinter(AutomationRunner testRunner)
		{
			// delete printer
			testRunner.ClickByName("Printer Overflow Menu");
			testRunner.ClickByName("Delete Printer Menu Item");

			testRunner.WaitForReloadAll(() => testRunner.ClickByName("Yes Button"));
		}

		public static void AddAndSelectPrinter(this AutomationRunner testRunner, string make, string model)
		{
			// If SelectMake is not visible and the ConnectionWizard is, click Skip
			if (!testRunner.NameExists("Select Make", 0.1))
			{
				// Go to the new tab screen
				testRunner.ClickByName("Create New");
				testRunner.ClickByName("Create Printer");
			}

			testRunner.ClickByName("Select Make");
			testRunner.WaitFor(() => testRunner.WidgetExists<PopupWidget>());
			testRunner.Type(make);
			testRunner.Type("{Enter}");
			testRunner.WaitFor(() => !testRunner.WidgetExists<PopupWidget>());


			testRunner.ClickByName("Select Model");
			testRunner.WaitFor(() => testRunner.WidgetExists<PopupWidget>());
			testRunner.Type(model);
			testRunner.Type("{Enter}");
			testRunner.WaitFor(() => !testRunner.WidgetExists<PopupWidget>());

			// An unpredictable period of time will pass between Clicking Save, everything reloading and us returning to the caller.
			// Block until ReloadAll has completed then close and return to the caller, at which point hopefully everything is reloaded.
			testRunner.WaitForReloadAll(() => testRunner.ClickByName("Save & Continue Button"));

			testRunner.WaitFor(() => testRunner.WidgetExists<SetupStepInstallDriver>());
			testRunner.ClickByName("Cancel Wizard Button");
			testRunner.WaitFor(() => !testRunner.WidgetExists<SetupStepInstallDriver>());
		}

		public static void OpenPrintersDropdown(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Create New");
			testRunner.ClickByName("Printers... Menu");
		}

		public static void ClosePrintersDropdown(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Printers... Menu");
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

		public static void NavigateToFolder(this AutomationRunner testRunner, string libraryRowItemName)
		{
			EnsureFoldersVisible(testRunner);
			testRunner.DoubleClickByName(libraryRowItemName);
		}

		public static void EnsureFoldersVisible(this AutomationRunner testRunner)
		{
			var checkBox = (ExpandCheckboxButton)testRunner.GetWidgetByName("Show Folders Toggle", out _);
			if (!checkBox.Checked)
			{
				var resetEvent = new AutoResetEvent(false);

				// Wire up event listener
				var listView = testRunner.GetWidgetByName("LibraryView", out _) as ListView;
				EventHandler contentReloaded = (s, e) =>
				{
					resetEvent.Set();
				};
				listView.ContentReloaded += contentReloaded;

				// Start reload
				testRunner.ClickByName("Show Folders Toggle");

				// Wait for reload
				resetEvent.WaitOne();

				// Release event listener
				listView.ContentReloaded -= contentReloaded;
			}

		}

		public static void NavigateToLibraryHome(this AutomationRunner testRunner)
		{
			while (!testRunner.NameExists("Local Library Row Item Collection", .5))
			{
				testRunner.ClickByName("Library Up Button");
				testRunner.Delay(1);
			}

			testRunner.Delay(.5);
		}

		/// <summary>
		/// Types the specified text into the dialog and sends {Enter} to complete the interaction
		/// </summary>
		/// <param name="testRunner">The TestRunner to interact with</param>
		/// <param name="textValue">The text to type</param>
		public static void CompleteDialog(this AutomationRunner testRunner, string textValue, double secondsToWait = 2)
		{
			// AutomationDialog requires no delay
			if (AggContext.FileDialogs is AutomationDialogProvider)
			{
				// Wait for text widget to have focus
				var widget = testRunner.GetWidgetByName("Automation Dialog TextEdit", out _, 5);
				testRunner.WaitFor(() => widget.ContainsFocus);
			}
			else
			{
				testRunner.Delay(secondsToWait);
			}

			testRunner.Type(textValue);

			testRunner.Type("{Enter}");
			testRunner.WaitForWidgetDisappear("Automation Dialog TextEdit", 5);
		}

		public static void AddItemToBedplate(this AutomationRunner testRunner, string containerName = "Calibration Parts Row Item Collection", string partName = "Row Item Calibration - Box.stl")
		{
			if (!testRunner.NameExists(partName, 1) && !string.IsNullOrEmpty(containerName))
			{
				testRunner.NavigateToFolder(containerName);
			}

			var partWidget = testRunner.GetWidgetByName(partName, out _) as ListViewItemBase;
			if (!partWidget.IsSelected)
			{
				testRunner.ClickByName(partName);
			}
			testRunner.ClickByName("Print Library Overflow Menu");

			var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
			var scene = view3D.InteractionLayer.Scene;
			var preAddCount = scene.Children.Count();

			testRunner.ClickByName("Add to Plate Menu Item");
			// wait for the object to be added
			testRunner.WaitFor(() => scene.Children.Count == preAddCount + 1);
			// wait for the object to be done loading
			var insertionGroup = scene.Children.LastOrDefault() as InsertionGroup;
			if (insertionGroup != null)
			{
				testRunner.WaitFor(() => scene.Children.LastOrDefault() as InsertionGroup != null, 10);
			}
		}

		public static void SaveBedplateToFolder(this AutomationRunner testRunner, string newFileName, string folderName)
		{
			testRunner.ClickByName("Bed Options Menu");
			testRunner.ClickByName("Save As Menu Item");

			testRunner.Delay(1);

			testRunner.Type(newFileName);

			testRunner.NavigateToFolder(folderName);

			testRunner.ClickByName("Accept Button");

			// Give the SaveAs window time to close before returning to the caller
			testRunner.Delay(2);
		}

		public static void WaitForPrintFinished(this AutomationRunner testRunner, int maxSeconds = 500)
		{
			testRunner.WaitFor(() => ApplicationController.Instance.ActivePrinter.Connection.CommunicationState == CommunicationStates.FinishedPrint, maxSeconds);
		}

		public static void WaitForCommunicationStateDisconnected(this AutomationRunner testRunner, int maxSeconds = 500)
		{
			testRunner.WaitFor(() => ApplicationController.Instance.ActivePrinter.Connection.CommunicationState == CommunicationStates.Disconnected, maxSeconds);
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
			//StackTrace st = new StackTrace(false);
			//Debug.WriteLine("\r\n ***** Running automation test: {0} {1} ", st.GetFrames().Skip(1).First().GetMethod().Name, DateTime.Now);

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

			// Override the default SystemWindow type without config.json
			AggContext.Config.ProviderTypes.SystemWindow = "MatterHackers.Agg.UI.OpenGLSystemWindow, agg_platform_win32";

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			AggContext.StaticData = new FileSystemStaticData(staticDataPathOverride);
#endif
			// Popping one directory above MatterControl, then back down into MatterControl ensures this works in MCCentral as well and MatterControl
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl"));

			if (queueItemFolderToAdd != QueueTemplate.None)
			{
				MatterControlUtilities.AddItemsToQueue(queueItemFolderToAdd.ToString());
			}

			if (defaultTestImages == null)
			{
				defaultTestImages = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "TestImages");
			}

			UserSettings.Instance.set(UserSettingsKey.ThumbnailRenderingMode, "orthographic");
			//GL.HardwareAvailable = false;

			var config = TestAutomationConfig.Load();
			if (config.UseAutomationDialogs)
			{
				AggContext.Config.ProviderTypes.DialogProvider = "MatterHackers.Agg.Platform.AutomationDialogProvider, GuiAutomation";
			}

			// Extract mouse speed from config
			AutomationRunner.TimeToMoveMouse = config.TimeToMoveMouse;
			AutomationRunner.UpDelaySeconds = config.MouseUpDelay;

			var (width, height) = RootSystemWindow.GetStartupBounds();

			rootSystemWindow = Application.LoadRootWindow(
				overrideWidth == -1 ? width : overrideWidth, 
				overrideHeight == -1 ? height : overrideHeight);

			await AutomationRunner.ShowWindowAndExecuteTests(
				rootSystemWindow,
				testMethod,
				maxTimeToRun,
				defaultTestImages,
				config.UseAutomationMouse ? AutomationRunner.InputType.SimulatedDrawMouse : AutomationRunner.InputType.Native,
				closeWindow: () =>
				{
					if (ApplicationController.Instance.ActivePrinter.Connection.CommunicationState == CommunicationStates.Printing)
					{
						ApplicationController.Instance.ActivePrinter.Connection.Disable();
					}

					rootSystemWindow.Close();
				});
		}

		public static void LibraryAddSelectionToQueue(AutomationRunner testRunner)
		{
			testRunner.ClickByName("Print Library Overflow Menu");
			testRunner.ClickByName("Add to Queue Menu Item");
		}

		public static void LibraryEditSelectedItem(AutomationRunner testRunner)
		{
			testRunner.ClickByName("Edit Menu Item");
			testRunner.Delay(1); // wait for the new window to open
		}

		public static void LibraryRenameSelectedItem(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Print Library Overflow Menu");
			testRunner.ClickByName("Rename Menu Item");
		}

		public static void LibraryRemoveSelectedItem(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Print Library Overflow Menu");
			testRunner.ClickByName("Remove Menu Item");
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

		public static void StartSlicing(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Generate Gcode Button");
		}

		/// <summary>
		/// Switch to the primary SliceSettings tab
		/// </summary>
		/// <param name="testRunner"></param>
		public static void OpenPrintPopupMenu(this AutomationRunner testRunner)
		{
			var printerConnection = ApplicationController.Instance.ActivePrinter.Connection;

			if (printerConnection.CommunicationState != CommunicationStates.Connected
				&& printerConnection.CommunicationState != CommunicationStates.FinishedPrint)
			{
				testRunner.ClickByName("Connect to printer button");
				testRunner.WaitFor(() => printerConnection.CommunicationState == CommunicationStates.Connected);
			}

			// Wait for button to become enabled
			var printerPopup = testRunner.GetWidgetByName("PrintPopupMenu", out _);
			testRunner.WaitFor(() => printerPopup.Enabled);

			testRunner.ClickByName("PrintPopupMenu");

			testRunner.ClickByName("Advanced Section");
		}

		/// <summary>
		/// Open the Print popup menu and click the Start Print button
		/// </summary>
		/// <param name="testRunner"></param>
		public static void StartPrint(this AutomationRunner testRunner)
		{
			testRunner.OpenPrintPopupMenu();
			testRunner.ScrollIntoView("Start Print Button");
			testRunner.ClickByName("Start Print Button");
		}

		public static void OpenGCode3DOverflowMenu(this AutomationRunner testRunner)
		{
			var button = testRunner.GetWidgetByName("Layers3D Button", out _) as ICheckbox;
			if (!button.Checked)
			{
				testRunner.ClickByName("Layers3D Button");
			}

			testRunner.ClickByName("View3D Overflow Menu");
		}

		/// <summary>
		/// Switch to the primary SliceSettings tab
		/// </summary>
		/// <param name="testRunner"></param>
		public static void SwitchToSliceSettings(this AutomationRunner testRunner)
		{
			EnsurePrinterSidebarOpen(testRunner);

			testRunner.ClickByName("Slice Settings Tab");
			testRunner.ClickByName("General Tab");
		}

		/// <summary>
		/// Switch to printer settings
		/// </summary>
		/// <param name="testRunner"></param>
		public static void SwitchToPrinterSettings(this AutomationRunner testRunner)
		{
			testRunner.SwitchToSliceSettings();

			testRunner.ClickByName("Printer Overflow Menu");
			testRunner.ClickByName("Configure Printer Menu Item");
			testRunner.ClickByName("Printer Tab");
		}

		public static void InlineTitleEdit(this AutomationRunner testRunner, string controlName, string replaceString)
		{
			testRunner.ClickByName(controlName + " Edit");
			testRunner.ClickByName(controlName + " Field");
			var textWidget = testRunner.GetWidgetByName(controlName + " Field", out _);
			textWidget.Text = replaceString;
			testRunner.ClickByName(controlName + " Save");
		}

		public static void SelectSliceSettingsField(this AutomationRunner testRunner, string userLevel, string slicerConfigName)
		{
			var rootLevel = SettingsOrganizer.Instance.UserLevels[userLevel];

			var settingData = SettingsOrganizer.Instance.GetSettingsData(slicerConfigName);

			var subGroup = rootLevel.GetContainerForSetting(slicerConfigName);

			var category = subGroup.Group.Category;

			// Click tab
			testRunner.ClickByName(category.Name + " Tab");

			// Click field
			testRunner.ClickByName($"{settingData.PresentationName} Field");
		}

		/// <summary>
		/// Switch to Printer -> Controls
		/// </summary>
		/// <param name="testRunner"></param>
		public static void SwitchToControlsTab(this AutomationRunner testRunner)
		{
			// Change to Printer Controls
			EnsurePrinterSidebarOpen(testRunner);
			testRunner.ClickByName("Controls Tab");
		}

		private static void EnsurePrinterSidebarOpen(AutomationRunner testRunner)
		{
			// If the sidebar exists, we need to expand and pin it
			if (testRunner.WaitForName("Slice Settings Sidebar", 0.2))
			{
				testRunner.ClickByName("Slice Settings Sidebar");
				testRunner.ClickByName("Pin Settings Button");
			}


		}

		/// <summary>
		/// Adds the given asset names to the local library and validates the result
		/// </summary>
		/// <param name="testRunner"></param>
		/// <param name="assetNames">The test assets to add to the library</param>
		public static void AddTestAssetsToLibrary(this AutomationRunner testRunner, params string[] assetNames)
		{
			// Switch to the Local Library tab
			testRunner.NavigateToFolder("Local Library Row Item Collection");

			// Assert that the requested items are *not* in the list
			foreach (string assetName in assetNames)
			{
				string friendlyName = Path.GetFileNameWithoutExtension(assetName);
				Assert.IsFalse(testRunner.WaitForName($"Row Item {friendlyName}", .1), $"{friendlyName} part should not exist at test start");
			}

			// Add Library item
			testRunner.ClickByName("Library Add Button");

			// Generate the full, quoted paths for the requested assets
			string fullQuotedAssetPaths = string.Join(" ", assetNames.Select(name => $"\"{MatterControlUtilities.GetTestItemPath(name)}\""));
			testRunner.CompleteDialog(fullQuotedAssetPaths);

			// Assert that the added items *are* in the list
			foreach (string assetName in assetNames)
			{
				string friendlyName = Path.GetFileNameWithoutExtension(assetName);
				Assert.IsTrue(testRunner.WaitForName($"Row Item {friendlyName}"), $"{friendlyName} part should exist after adding");
			}
		}

		/// <summary>
		/// Control clicks each specified item
		/// </summary>
		/// <param name="testRunner"></param>
		/// <param name="widgetNames">The widgets to click</param>
		public static void SelectListItems(this AutomationRunner testRunner, params string[] widgetNames)
		{
			// Control click all items
			Keyboard.SetKeyDownState(Keys.ControlKey, down: true);
			foreach (var widgetName in widgetNames)
			{
				testRunner.ClickByName(widgetName);
			}
			Keyboard.SetKeyDownState(Keys.ControlKey, down: false);
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
		/// The number of seconds to move the mouse when going to a new position.
		/// </summary>
		public double TimeToMoveMouse { get; set; } = .5;

		public bool UseAutomationDialogs { get; set; }

		public bool UseAutomationMouse { get; set; }

		public double MouseUpDelay { get; set; } = 0.2;

		/// <summary>
		/// The number of seconds the emulator should take to heat up and given target
		/// </summary>
		public double HeatupTime { get; set; } = 0.5;

		/// <summary>
		/// The number of seconds to wait after reaching the target temp before continuing. Analogous to 
		/// firmware dwell time for temperature stabilization
		/// </summary>
		public double TempStabilizationTime { get; set; } = 0.5;

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