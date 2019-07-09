/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterControl.Printing;
using MatterControl.Printing.Pipelines;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PrinterEmulator;
using Newtonsoft.Json;
using NUnit.Framework;
using SQLiteWin32;

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

		public static void SignOutUser(this AutomationRunner testRunner)
		{
			testRunner.ClickSignOut();

			// Rather than waiting a fixed amount of time, we wait for the ReloadAll to complete before returning
			testRunner.WaitForReloadAll(() => testRunner.ClickByName("Yes Button"));
		}

		public static void ClickSignOut(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("User Options Menu");
			testRunner.ClickByName("Sign Out Menu Item");
			testRunner.Delay(.5);
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

		public static void WaitForPage(this AutomationRunner testRunner, string headerText)
		{
			// Helper methods
			bool HeaderExists(string text)
			{
				var header = testRunner.GetWidgetByName("HeaderRow", out _);
				var textWidget = header.Children<TextWidget>().FirstOrDefault();

				return textWidget?.Text.StartsWith(text) ?? false;
			}

			testRunner.WaitFor(() => HeaderExists(headerText));

			Assert.IsTrue(HeaderExists(headerText), "Expected page not found: " + headerText);
		}


		public static string PathToExportGcodeFolder
		{
			get => TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "ExportedGcode", runName);
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
			CloseSignInAndPrinterSelect
		};

		public static void ExpandEditTool(this AutomationRunner testRunner, string expandCheckboxButtonName)
		{
			var mirrorPanel = testRunner.GetWidgetByName(expandCheckboxButtonName, out _);
			var checkBox = mirrorPanel.Children<ExpandCheckboxButton>().FirstOrDefault();
			if (checkBox?.Checked != true)
			{
				testRunner.ClickByName(expandCheckboxButtonName);
			}
		}

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

		public static void WaitForFirstDraw(this AutomationRunner testRunner)
		{
			testRunner.GetWidgetByName("PartPreviewContent", out SystemWindow systemWindow, 10);
			// make sure we wait for MC to be up and running
			testRunner.WaitforDraw(systemWindow);
		}

		public static void OpenEmptyPartTab(this AutomationRunner testRunner)
		{
			SystemWindow systemWindow;
			testRunner.GetWidgetByName("Hardware Tab", out systemWindow, 10);
			testRunner.WaitforDraw(systemWindow);

			// Latest product starts at empty part tab

			// close the welcome message
			if (testRunner.NameExists("Cancel Wizard Button", 10))
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
				catch
				{
				}

				base.Dispose(disposing);
			}
		}

		public static Emulator LaunchAndConnectToPrinterEmulator(this AutomationRunner testRunner, string make = "Airwolf 3D", string model = "HD", bool runSlow = false)
		{
			var hardwareTab = testRunner.GetWidgetByName("Hardware Tab", out SystemWindow systemWindow, 10);

			// make sure we wait for MC to be up and running
			testRunner.WaitforDraw(systemWindow);

			// Load the TestEnv config
			var config = TestAutomationConfig.Load();

			// Override the heat up time
			Emulator.DefaultHeatUpTime = config.HeatupTime;

			// Override the temp stabilization time
			WaitForTempStream.WaitAfterReachTempTime = config.TempStabilizationTime;

			// Create the printer
			testRunner.AddAndSelectPrinter(make, model);

			// edit the com port
			testRunner.SwitchToPrinterSettings();

			var serialPortDropDown = testRunner.GetWidgetByName("com_port Field", out _, 1);

			testRunner.WaitFor(() => serialPortDropDown.Enabled); // Wait until the serialPortDropDown is ready to click it. Ensures the printer is loaded.

			testRunner.ClickByName("com_port Field");

			testRunner.ClickByName("Emulator Menu Item");

			// connect to the created printer
			testRunner.ClickByName("Connect to printer button");

			testRunner.WaitForName("Disconnect from printer button");

			// replace the old behavior of clicking the 'Already Loaded' button by setting to filament_has_been_loaded.
			ApplicationController.Instance.ActivePrinters.First().Settings.SetValue(SettingsKey.filament_has_been_loaded, "1");

			// Access through static instance must occur after Connect has occurred and the port has spun up
			Emulator.Instance.RunSlow = runSlow;

			return Emulator.Instance;
		}

		public static void CancelPrint(this AutomationRunner testRunner)
		{
			// If the pause/resume dialog is open, dismiss it before canceling the print
			if (testRunner.WaitForName("Yes Button", 1))
			{
				testRunner.ClickByName("Yes Button");
			}

			// TODO: Improve this to more accurately find the print task row and click its Stop button
			if (testRunner.WaitForName("Stop Task Button", .2))
			{
				testRunner.ClickByName("Stop Task Button");
			}

			// Wait for and dismiss the new PrintCompleted dialog
			testRunner.WaitForName("Ok Button");
			testRunner.ClickByName("Ok Button");
		}

		public static void WaitForLayer(this Emulator emulator, PrinterSettings printerSettings, int layerNumber, double secondsToWait = 30)
		{
			var resetEvent = new AutoResetEvent(false);

			var heightAtTargetLayer = printerSettings.GetValue<double>(SettingsKey.layer_height) * layerNumber;

			// Wait for emulator to hit target layer
			emulator.DestinationChanged += (s, e) =>
			{
				// Wait for print to start, then slow down the emulator and continue. Failing to slow down frequently causes a timing issue where the print
				// finishes before we make it down to 'CloseMatterControlViaUi' and thus no prompt to close appears and the test fails when clicking 'Yes Button'
				if (emulator.Destination.Z >= heightAtTargetLayer)
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
			// Click 'Delete Printer' menu item
			testRunner.ClickByName("Printer Overflow Menu");
			testRunner.ClickByName("Delete Printer Menu Item");

			// Confirm Delete
			testRunner.WaitForName("HeaderRow");
			testRunner.ClickByName("Yes Button");
		}

		public static void AddAndSelectPrinter(this AutomationRunner testRunner, string make = "Airwolf 3D", string model = "HD")
		{
			testRunner.GetWidgetByName("PartPreviewContent", out SystemWindow systemWindow, 10);

			// make sure we wait for MC to be up and running
			testRunner.WaitforDraw(systemWindow);

			// close the welcome message
			testRunner.EnsureWelcomePageClosed();

			// Click 'Add Printer' if not on screen
			if (!testRunner.NameExists("AddPrinterWidget", 0.2))
			{
				if (!testRunner.NameExists("Create Printer", 0.2))
				{
					// go to the start page
					testRunner.ClickByName("Hardware Tab");
					testRunner.ClickByName("Create Printer");
				}
				else
				{
					if (testRunner.NameExists("Print Button", .2))
					{
						testRunner.ClickByName("Print Button");
					}
					else
					{
						testRunner.ClickByName("Create Printer");
					}
				}
			}

			// Wait for the tree to load before filtering
			testRunner.WaitFor(() =>
			{
				var widget = testRunner.GetWidgetByName("AddPrinterWidget", out _) as AddPrinterWidget;
				return widget.TreeLoaded;
			});

			// Apply filter
			testRunner.ClickByName("Search");
			testRunner.Type(model);
			testRunner.Type("{Enter}");

			// Click printer node
			testRunner.Delay();
			testRunner.ClickByName($"Node{make}{model}");

			// Continue to next page
			testRunner.ClickByName("Next Button");

			testRunner.Delay();

			testRunner.WaitFor(() => testRunner.ChildExists<SetupStepComPortOne>());
			testRunner.ClickByName("Cancel Wizard Button");

			testRunner.WaitFor(() => !testRunner.ChildExists<SetupStepComPortOne>());
		}

		public static void EnsureWelcomePageClosed(this AutomationRunner testRunner)
		{
			// Close the WelcomePage window if active
			if (testRunner.GetWidgetByName("HeaderRow", out _) is GuiWidget headerRow
				&& headerRow.Parents<DialogPage>().FirstOrDefault() is Tour.WelcomePage welcomePage
				&& testRunner.NameExists("Cancel Wizard Button", 1))
			{
				testRunner.ClickByName("Cancel Wizard Button");
			}
		}

		public static void WaitForAndCancelPrinterSetupPage(this AutomationRunner testRunner)
		{
			testRunner.WaitFor(() =>
			{
				return testRunner.GetWidgetByName("HeaderRow", out _) is GuiWidget headerRow
					&& headerRow.Parents<DialogPage>().FirstOrDefault() is SetupStepMakeModelName;
			});

			testRunner.ClickByName("Cancel Wizard Button");
		}

		public static void SwitchToHardwareTab(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Hardware Tab");
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
			ApplicationDataStorage.Instance.OverrideAppDataLocation(tempFolderPath, () => DesktopSqlite.CreateInstance());
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

		public static void OpenUserPopupMenu(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("User Options Menu");
		}

		public static void NavigateToFolder(this AutomationRunner testRunner, string libraryRowItemName)
		{
			testRunner.EnsureContentMenuOpen();
			testRunner.EnsureFoldersVisible();

			switch (libraryRowItemName)
			{
				case "SD Card Row Item Collection":
					if (ApplicationController.Instance.DragDropData.View3DWidget?.Printer is PrinterConfig printer)
					{
						testRunner.DoubleClickByName($"{printer.Settings.GetValue(SettingsKey.printer_name)} Row Item Collection");

						testRunner.Delay();

						testRunner.ClickByName(libraryRowItemName);
					}

					break;

				case "Calibration Parts Row Item Collection":
				case "Cloud Library Row Item Collection":
				case "Print Queue Row Item Collection":
				case "Local Library Row Item Collection":
					if (!testRunner.NameExists("Library Row Item Collection"))
					{
						testRunner.ClickByName("Bread Crumb Button Home");
						testRunner.Delay();
					}

					// If visible, navigate into Libraries container before opening target
					if (testRunner.NameExists("Library Row Item Collection"))
					{
						testRunner.DoubleClickByName("Library Row Item Collection");
						testRunner.Delay();
					}

					break;
			}

			testRunner.DoubleClickByName(libraryRowItemName);
		}

		public static void EnsureFoldersVisible(this AutomationRunner testRunner)
		{
			var checkBox = (ExpandCheckboxButton)testRunner.GetWidgetByName("Show Folders Toggle", out _, secondsToWait: 0.2);
			if (checkBox?.Checked == false)
			{
				var resetEvent = new AutoResetEvent(false);

				// Wire up event listener
				var listView = testRunner.GetWidgetByName("LibraryView", out _) as LibraryListView;
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

		public static void EnsureContentMenuOpen(this AutomationRunner testRunner)
		{
			if (!testRunner.WaitForName("FolderBreadCrumbWidget", secondsToWait: 0.2))
			{
				testRunner.ClickByName("Add Content Menu");
			}
		}

		public static void OpenRequiredSetupAndConfigureMaterial(this AutomationRunner testRunner)
		{
			// Complete new material selection requirement
			testRunner.ClickByName("PrintPopupMenu");
			testRunner.ClickByName("SetupPrinter");

			// Configure ABS as selected material
			//testRunner.ClickByName("Material DropDown List");
			//testRunner.ClickByName("ABS Menu");

			// Currently material selection is not required, simply act of clicking 'Select' clears setup required
			testRunner.ClickByName("Already Loaded Button");
		}

		public static void NavigateToLibraryHome(this AutomationRunner testRunner)
		{
			testRunner.EnsureContentMenuOpen();
			testRunner.ClickByName("Bread Crumb Button Home");
			testRunner.Delay(.5);
		}

		public static void InvokeLibraryAddDialog(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Print Library Overflow Menu");
			testRunner.ClickByName("Add Menu Item");
		}

		public static void InvokeLibraryCreateFolderDialog(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Print Library Overflow Menu");
			testRunner.ClickByName("Create Folder Menu Item");
		}

		public static string CreateChildFolder(this AutomationRunner testRunner, string folderName)
		{
			testRunner.InvokeLibraryCreateFolderDialog();
			testRunner.WaitForName("InputBoxPage Action Button");
			testRunner.Type(folderName);
			testRunner.ClickByName("InputBoxPage Action Button");

			string folderID = $"{folderName} Row Item Collection";

			Assert.IsTrue(testRunner.WaitForName(folderID), $"{folderName} exists");

			return folderID;
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

			testRunner.ClickByName("Add to Bed Menu Item");
			// wait for the object to be added
			testRunner.WaitFor(() => scene.Children.Count == preAddCount + 1);
			// wait for the object to be done loading
			var insertionGroup = scene.Children.LastOrDefault() as InsertionGroupObject3D;
			if (insertionGroup != null)
			{
				testRunner.WaitFor(() => scene.Children.LastOrDefault() as InsertionGroupObject3D != null, 10);
			}
		}

		public static void SaveBedplateToFolder(this AutomationRunner testRunner, string newFileName, string folderName)
		{
			testRunner.ClickByName("Save Menu SplitButton", offset: new Point2D(8, 0));

			testRunner.ClickByName("Save As Menu Item");

			testRunner.Delay(1);

			testRunner.Type(newFileName);

			testRunner.NavigateToFolder(folderName);

			testRunner.ClickByName("Accept Button");

			// Give the SaveAs window time to close before returning to the caller
			testRunner.Delay(2);
		}

		public static void WaitForPrintFinished(this AutomationRunner testRunner, PrinterConfig printer, int maxSeconds = 500)
		{
			testRunner.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.FinishedPrint, maxSeconds);
			// click the ok button on the print complete dialog
			testRunner.ClickByName("Ok Button");
		}

		/// <summary>
		/// Gets a reference to the first and only active printer. Throws if called when multiple active printers exists
		/// </summary>
		/// <param name="testRunner"></param>
		/// <returns>The first active printer</returns>
		public static PrinterConfig FirstPrinter(this AutomationRunner testRunner)
		{
			Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "FirstPrinter() is only valid in single printer scenarios");

			return ApplicationController.Instance.ActivePrinters.First();
		}

		public static void CloseFirstPrinterTab(this AutomationRunner testRunner)
		{
			// Close all printer tabs
			var mainViewWidget = testRunner.GetWidgetByName("PartPreviewContent", out _) as MainViewWidget;
			if (mainViewWidget.TabControl.AllTabs.First(t => t.TabContent is PrinterTabPage) is GuiWidget widget)
			{
				var closeWidget = widget.Descendants<ImageWidget>().First();
				Assert.AreEqual("Close Tab Button", closeWidget.Name, "Expected widget ('Close Tab Button') not found");

				testRunner.ClickWidget(closeWidget);
			}
		}

		public static void WaitForCommunicationStateDisconnected(this AutomationRunner testRunner, PrinterConfig printer, int maxSeconds = 500)
		{
			testRunner.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.Disconnected, maxSeconds);
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
			//AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.Agg.UI.OpenGLWinformsWindowProvider, agg_platform_win32";
			AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.MatterControl.WinformsSingleWindowProvider, MatterControl.Winforms";

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

			// Automation runner must do as much as program.cs to spin up platform
			string platformFeaturesProvider = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl.Winforms";
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.InitPluginFinder();
			AppContext.Platform.ProcessCommandline();

			var (width, height) = RootSystemWindow.GetStartupBounds();

			rootSystemWindow = Application.LoadRootWindow(
				overrideWidth == -1 ? width : overrideWidth,
				overrideHeight == -1 ? height : overrideHeight);

			OemSettings.Instance.ShowShopButton = false;

			if (!config.UseAutomationMouse)
			{
				AutomationRunner.InputMethod = new WindowsInputMethods();
			}

			await AutomationRunner.ShowWindowAndExecuteTests(
				rootSystemWindow,
				testMethod,
				maxTimeToRun,
				defaultTestImages,
				closeWindow: () =>
				{
					foreach (var printer in ApplicationController.Instance.ActivePrinters)
					{
						if (printer.Connection.CommunicationState == CommunicationStates.Printing)
						{
							printer.Connection.Disable();
						}
					}

					rootSystemWindow.Close();
				});
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
			testRunner.ClickByName("Yes Button");
		}

		public static void LibraryMoveSelectedItem(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Print Library Overflow Menu");
			testRunner.ClickByName("Move Menu Item");
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

		public static void OpenPrintPopupMenu(this AutomationRunner testRunner)
		{
			var printerConnection = ApplicationController.Instance.DragDropData.View3DWidget.Printer.Connection;

			if (printerConnection.CommunicationState != CommunicationStates.Connected
				&& printerConnection.CommunicationState != CommunicationStates.FinishedPrint)
			{
				testRunner.ClickByName("Connect to printer button");
				testRunner.WaitFor(() => printerConnection.CommunicationState == CommunicationStates.Connected);
			}

			// Open PopupMenu
			testRunner.ClickByName("PrintPopupMenu");

			// Wait for child control
			testRunner.WaitForName("Start Print Button");
		}

		/// <summary>
		/// Open the Print popup menu and click the Start Print button
		/// </summary>
		/// <param name="testRunner"></param>
		public static void StartPrint(this AutomationRunner testRunner, string pauseAtLayers = null)
		{
			// Open popup
			testRunner.OpenPrintPopupMenu();

			if (pauseAtLayers != null)
			{
				testRunner.OpenPrintPopupAdvanced();

				testRunner.ClickByName("Layer(s) To Pause Field");
				testRunner.Type(pauseAtLayers);
			}

			testRunner.ClickByName("Start Print Button");
		}

		public static void OpenPrintPopupAdvanced(this AutomationRunner testRunner)
		{
			// Expand advanced panel if needed
			if (!testRunner.NameExists("Layer(s) To Pause Field", .2))
			{
				testRunner.ClickByName("Advanced Section");
			}

			// wait for child
			testRunner.WaitForName("Layer(s) To Pause Field");
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
		}

		public static void Complete9StepLeveling(this AutomationRunner testRunner, int numUpClicks = 1)
		{
			void waitForPageAndAdvance(string headerText)
			{
				testRunner.WaitForPage(headerText);
				testRunner.ClickByName("Next Button");
			}

			testRunner.Delay();

			waitForPageAndAdvance("Print Leveling Overview");

			waitForPageAndAdvance("Heating the printer");

			for (int i = 0; i < 3; i++)
			{
				var section = (i * 3) + 1;

				testRunner.WaitForPage($"Step {section} of 9");
				for (int j = 0; j < numUpClicks; j++)
				{
					testRunner.Delay();
					testRunner.ClickByName("Move Z positive");
				}

				testRunner.WaitForPage($"Step {section} of 9");
				testRunner.ClickByName("Next Button");

				testRunner.WaitForPage($"Step {section + 1} of 9");
				testRunner.ClickByName("Next Button");

				testRunner.WaitForPage($"Step {section + 2} of 9");
				testRunner.ClickByName("Next Button");
			}

			testRunner.ClickByName("Done Button");

			testRunner.Delay();
			if (testRunner.NameExists("Already Loaded Button", 0.2))
			{
				testRunner.ClickByName("Already Loaded Button");
			}

			// Close the staged wizard window
			testRunner.ClickByName("Cancel Wizard Button");
		}

		/// <summary>
		/// Switch to printer settings
		/// </summary>
		/// <param name="testRunner"></param>
		public static void SwitchToPrinterSettings(this AutomationRunner testRunner)
		{
			EnsurePrinterSidebarOpen(testRunner);

			if (!testRunner.NameExists("Printer Tab", 0.1))
			{
				testRunner.ClickByName("Printer Overflow Menu");
				testRunner.ClickByName("Configure Printer Menu Item");
			}
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

		public static SliceSettingData NavigateToSliceSettingsField(this AutomationRunner testRunner, SettingsLayout.SettingsSection rootLevel, string slicerConfigName)
		{
			var settingData = PrinterSettings.SettingsData[slicerConfigName];

			var subGroup = settingData.OrganizerSubGroup;

			var category = settingData.OrganizerSubGroup.Group.Category;

			// Click tab
			testRunner.ClickByName(category.Name + " Tab");

			// Open the subGroup if required
			var foundWidget = testRunner.GetWidgetByName(subGroup.Name + " Panel", out _);
			if (foundWidget != null)
			{
				var containerCheckBox = foundWidget.Descendants<ExpandCheckboxButton>().First();
				if (!containerCheckBox.Checked)
				{
					containerCheckBox.Checked = true;
					testRunner.Delay();
				}
			}

			return settingData;
		}

		public static void SelectSliceSettingsField(this AutomationRunner testRunner, SettingsLayout.SettingsSection settingsSection, string slicerConfigName)
		{
			var settingData = NavigateToSliceSettingsField(testRunner, settingsSection, slicerConfigName);
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

			if (!testRunner.NameExists("Controls Tab", 0.2))
			{
				testRunner.ClickByName("Printer Overflow Menu");
				testRunner.ClickByName("Show Controls Menu Item");
			}

			testRunner.ClickByName("Controls Tab");
		}

		/// <summary>
		/// Switch to Printer -> Terminal
		/// </summary>
		/// <param name="testRunner"></param>
		public static void SwitchToTerminalTab(this AutomationRunner testRunner)
		{
			// Change to Printer Controls
			EnsurePrinterSidebarOpen(testRunner);

			if (!testRunner.NameExists("Terminal Tab", 0.2))
			{
				testRunner.ClickByName("Printer Overflow Menu");
				testRunner.ClickByName("Show Terminal Menu Item");
			}

			testRunner.ClickByName("Terminal Tab");
		}

		/// <summary>
		/// Switch to Printer -> GCode Tab - NOTE: as a short term hack this helper as adds content to the bed and slices to ensure GCode view options appear as expected
		/// </summary>
		/// <param name="testRunner"></param>
		public static void SwitchToGCodeTab(this AutomationRunner testRunner)
		{
			testRunner.ClickByName("Layers3D Button");

			// TODO: Remove workaround needed to force GCode options to appear {{
			testRunner.AddItemToBedplate();
			testRunner.ClickByName("Generate Gcode Button");
			// TODO: Remove workaround needed to force GCode options to appear }}
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
		public static void AddTestAssetsToLibrary(this AutomationRunner testRunner, IEnumerable<string> assetNames, string targetLibrary = "Local Library Row Item Collection")
		{
			// Switch to the Local Library tab
			testRunner.NavigateToFolder(targetLibrary);

			// Assert that the requested items are *not* in the list
			foreach (string assetName in assetNames)
			{
				string friendlyName = Path.GetFileNameWithoutExtension(assetName);
				Assert.IsFalse(testRunner.WaitForName($"Row Item {friendlyName}", .1), $"{friendlyName} part should not exist at test start");
			}

			// Add Library item
			testRunner.InvokeLibraryAddDialog();

			// Generate the full, quoted paths for the requested assets
			string fullQuotedAssetPaths = string.Join(" ", assetNames.Select(name => $"\"{MatterControlUtilities.GetTestItemPath(name)}\""));
			testRunner.CompleteDialog(fullQuotedAssetPaths);

			// Assert that the added items *are* in the list
			foreach (string assetName in assetNames)
			{
				string friendlyName = Path.GetFileNameWithoutExtension(assetName);
				string fileName = Path.GetFileName(assetName);

				// Look for either expected format (print queue differs from libraries)
				Assert.IsTrue(
					testRunner.WaitForName($"Row Item {friendlyName}", 2)
					|| testRunner.WaitForName($"Row Item {fileName}", 2),
					$"{friendlyName} part should exist after adding");
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
		Three_Queue_Items,
		ReSliceParts
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

		/// <summary>
		/// Determines if we use actual system file dialogs or simulated file dialogs.
		/// </summary>
		public bool UseAutomationDialogs { get; set; } = true;

		public bool UseAutomationMouse { get; set; } = true;

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