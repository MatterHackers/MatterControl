using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ExportGcodeFromExportWindow
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task ExportAsGcode()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				string firstItemName = "Queue Item Batman";
				//Navigate to Downloads Library Provider
				testRunner.ClickByName("Queue Tab");
				testRunner.ClickByName("Queue Add Button", 2);

				//Get parts to add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");

				//Add STL part items to Downloads and then type paths into file dialog
				testRunner.Wait(1);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				//Get test results 
				Assert.IsTrue(testRunner.WaitForName(firstItemName, 2) == true);

				testRunner.ClickByName("Queue Export Button");
				testRunner.Wait(2);

				testRunner.WaitForName("Export Item Window", 2);
				testRunner.ClickByName("Export as GCode Button", 2);
				testRunner.Wait(2);

				string gcodeOutputPath = MatterControlUtilities.PathToExportGcodeFolder;

				Directory.CreateDirectory(gcodeOutputPath);

				string fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Batman");
				testRunner.Type(fullPathToGcodeFile);
				testRunner.Type("{Enter}");
				testRunner.Wait(2);

				Console.WriteLine(gcodeOutputPath);

				Assert.IsTrue(File.Exists(fullPathToGcodeFile + ".gcode") == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}
