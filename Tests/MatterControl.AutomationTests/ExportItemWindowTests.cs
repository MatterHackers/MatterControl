using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.Library.Export;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterControl.Tests.MatterControl;
using NUnit.Framework;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("Agg.UI.Automation"), Apartment(ApartmentState.STA), RunInApplicationDomain]
	public class ExportGcodeFromExportWindow
	{
		[Test]
		public async Task ExportAsGcode()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.WaitForFirstDraw();

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				//Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Print Queue Row Item Collection");
				testRunner.InvokeLibraryAddDialog();

				//Get parts to add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");
				testRunner.Delay()
					.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"))
					.Delay()
					.Type("{Enter}");

				//Get test results 
				testRunner.ClickByName("Row Item Batman.stl")
					.ClickByName("Print Library Overflow Menu")
					.ClickByName("Export Menu Item")
					.WaitForName("Export Item Window");

				string gcodeOutputPath = MatterControlUtilities.PathToExportGcodeFolder;
				Directory.CreateDirectory(gcodeOutputPath);
				string fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Batman");

				testRunner.ClickByName("Machine File (G-Code) Button")
					.ClickByName("Export Button")
					.Delay()
					.Type(fullPathToGcodeFile)
					.Type("{Enter}")
					.WaitFor(() => File.Exists(fullPathToGcodeFile + ".gcode"), 10);

				Assert.IsTrue(File.Exists(fullPathToGcodeFile + ".gcode"), "Exported file not found");

				// add an item to the bed
				fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Cube");
				testRunner.AddItemToBedplate()
					.ClickByName("PrintPopupMenu")
					.ClickByName("Export GCode Button")
					.Type(fullPathToGcodeFile)
					.Type("{Enter}");

				testRunner.WaitFor(() => File.Exists(fullPathToGcodeFile + ".gcode"), 10);
				Assert.IsTrue(File.Exists(fullPathToGcodeFile + ".gcode"), "Exported file not found");

				return Task.FromResult(0);
			});
		}

		[Test]
		public async Task ExportDesignTabAsSTL()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.WaitForFirstDraw();

				// save from design tab
				var gcodeOutputPath = MatterControlUtilities.PathToExportGcodeFolder;
				var fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Cube2");
				Directory.CreateDirectory(gcodeOutputPath);
				testRunner.EnsureWelcomePageClosed()
					.ClickByName("Create New")
					.AddItemToBedplate()					
					.ClickByName("Bed Options Menu")
					.ClickByName("Export Menu Item")
					.WaitForName("Export Item Window");

				testRunner.ClickByName("STL File Button")
					.ClickByName("Export Button")
					.Delay()
					.Type(fullPathToGcodeFile)
					.Type("{Enter}")
					.WaitFor(() => File.Exists(fullPathToGcodeFile + ".stl"), 10);

				testRunner.WaitFor(() => File.Exists(fullPathToGcodeFile + ".stl"), 10);
				Assert.IsTrue(File.Exists(fullPathToGcodeFile + ".stl"), "Exported file not found");

				return Task.FromResult(0);
			});
		}

		[Test]
		public async Task ExportStreamG92HandlingTest()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.WaitForFirstDraw();

				testRunner.CloneAndSelectPrinter("No Retraction after Purge");

				var printer = testRunner.FirstPrinter();

				//Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Print Queue Row Item Collection");
				testRunner.InvokeLibraryAddDialog();

				//Get parts to add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");
				testRunner.Delay()
					.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"))
					.Delay()
					.Type("{Enter}");

				//Get test results 
				testRunner.ClickByName("Row Item Batman.stl")
					.ClickByName("Print Library Overflow Menu")
					.ClickByName("Export Menu Item")
					.WaitForName("Export Item Window");

				testRunner.ClickByName("Machine File (G-Code) Button")
					.ClickByName("Export Button");

				string gcodeOutputPath = MatterControlUtilities.PathToExportGcodeFolder;

				Directory.CreateDirectory(gcodeOutputPath);

				string fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Batman");
				testRunner.Type(fullPathToGcodeFile);
				testRunner.Type("{Enter}");

				var filename = fullPathToGcodeFile + ".gcode";
				testRunner.WaitFor(() => File.Exists(filename), 10)
					.WaitFor(() => !IsFileLocked(filename), 1000)
					.Delay(2);

				// validate that the actual printer output has the right lines
				var expectedLines = new string[]
				{
					"G28                    ; home all axes",
					"M280 P0 S160",
					"G4 P400",
					"M280 P0 S90",
					"M109 S205",
					"G1 X5 Y5 Z3.13 F1800",
					"G92 E0                 ; Purge line",
					"G1 X5.83 Y5 Z3.04 E0.833 F900",
					"G1 X6.67 Y5 Z2.96 E1.667",
					"G1 X7.5 Y5 Z2.87 E2.5",
					"G1 X8.33 Y5 Z2.79 E3.333",
					"G1 X9.17 Y5 Z2.7 E4.167",
					"G1 X10 Y5 Z2.62 E5",
					"G92 E0                 ; Purge line",
					"G1 X10 Y5 Z2.62 E-2 F2400",
					"M75                    ; start print timer",
				};

				var actualLines = File.ReadAllLines(filename);
				ValidateLinesStartingWithFirstExpected(expectedLines, actualLines);

				// make sure the file has the expected header

				return Task.FromResult(0);
			}, maxTimeToRun: 200);
		}

		private bool IsFileLocked(string file)
		{
			try
			{
				using (FileStream stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None))
				{
					stream.Close();
				}
			}
			catch (IOException)
			{
				//the file is unavailable because it is:
				//still being written to
				//or being processed by another thread
				//or does not exist (has already been processed)
				return true;
			}

			//file is not locked
			return false;
		}

		private void ValidateLinesStartingWithFirstExpected(string[] expectedLines, string[] actualLines)
		{
			// search actual lines until we find the first expectedLine
			for (int i = 0; i < actualLines.Length; i++)
			{
				if (actualLines[i] == expectedLines[0])
				{
					for (int j = 0; j < expectedLines.Length; j++)
					{
						Assert.AreEqual(actualLines[i + j], expectedLines[j], "All lines should match");
						// Debug.WriteLine("\"" + actualLines[i + j] + "\",");
					}

					return;
				}
			}

			throw new Exception("Did not find the first expected line");
		}
	}
}
