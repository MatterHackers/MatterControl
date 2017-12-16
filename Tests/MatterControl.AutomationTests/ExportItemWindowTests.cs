using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ExportGcodeFromExportWindow
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task ExportAsGcode()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				//Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Print Queue Row Item Collection");
				testRunner.ClickByName("Library Add Button");

				//Get parts to add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");

				//Add STL part items to Downloads and then type paths into file dialog
				testRunner.Delay(1);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				//Get test results 
				testRunner.ClickByName("Row Item Batman.stl");

				testRunner.ClickByName("Print Library Overflow Menu");
				testRunner.ClickByName("Export Menu Item");
				testRunner.Delay(2);

				testRunner.WaitForName("Export Item Window");
				testRunner.ClickByName("Machine File (G-Code) Button");
				testRunner.ClickByName("Export Button");
				testRunner.Delay(2);

				string gcodeOutputPath = MatterControlUtilities.PathToExportGcodeFolder;

				Directory.CreateDirectory(gcodeOutputPath);

				string fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Batman");
				testRunner.Type(fullPathToGcodeFile);
				testRunner.Type("{Enter}");

				testRunner.WaitFor(() => File.Exists(fullPathToGcodeFile + ".gcode"), 10);

				Assert.IsTrue(File.Exists(fullPathToGcodeFile + ".gcode") == true);

				return Task.FromResult(0);
			});
		}
	}
}
