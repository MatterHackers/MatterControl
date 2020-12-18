using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.SlicerConfiguration;
using NUnit.Framework;

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
				testRunner.Delay(1)
					.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"))
					.Delay(1)
					.Type("{Enter}");

				//Get test results 
				testRunner.ClickByName("Row Item Batman.stl")
					.ClickByName("Print Library Overflow Menu")
					.ClickByName("Export Menu Item")
					.Delay(2)
					.WaitForName("Export Item Window");

				testRunner.ClickByName("Machine File (G-Code) Button")
					.ClickByName("Export Button")
					.Delay(2);

				string gcodeOutputPath = MatterControlUtilities.PathToExportGcodeFolder;

				Directory.CreateDirectory(gcodeOutputPath);

				string fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Batman");
				testRunner.Type(fullPathToGcodeFile);
				testRunner.Type("{Enter}");

				testRunner.WaitFor(() => File.Exists(fullPathToGcodeFile + ".gcode"), 10);

				Assert.IsTrue(File.Exists(fullPathToGcodeFile + ".gcode"), "Exported file not found");

				return Task.FromResult(0);
			});
		}

		[Test]
		public async Task ExportStreamG92HandlingTest()
		{
			var startGCode = "G28\\nM109 S[Temperature]\\nG1 Y5 X5 Z0.8 F1800\\nG92 E0\\nG1 X100 Z0.3 E25 F900\\nG92 E0\\nG1 E-2 F2400\\nG92 E0\\nG1 E1 F900";

			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.WaitForFirstDraw();

				testRunner.CloneAndSelectPrinter("No Retraction after Purge.printer");

				var printer = testRunner.FirstPrinter();
				printer.Settings.SetValue(SettingsKey.start_gcode, startGCode);

				//Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Print Queue Row Item Collection");
				testRunner.InvokeLibraryAddDialog();

				//Get parts to add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");
				testRunner.Delay(1)
					.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"))
					.Delay(1)
					.Type("{Enter}");

				//Get test results 
				testRunner.ClickByName("Row Item Batman.stl")
					.ClickByName("Print Library Overflow Menu")
					.ClickByName("Export Menu Item")
					.Delay(2)
					.WaitForName("Export Item Window");

				testRunner.ClickByName("Machine File (G-Code) Button")
					.ClickByName("Export Button")
					.Delay(2);

				string gcodeOutputPath = MatterControlUtilities.PathToExportGcodeFolder;

				Directory.CreateDirectory(gcodeOutputPath);

				string fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Batman");
				testRunner.Type(fullPathToGcodeFile);
				testRunner.Type("{Enter}");

				var filename = fullPathToGcodeFile + ".gcode";
				testRunner.WaitFor(() => File.Exists(filename), 10)
					.Delay(2);

				var gcode = File.ReadAllLines(filename);

				// make sure the file has the expected header

				return Task.FromResult(0);
			});
		}
	}
}
