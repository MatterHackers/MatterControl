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
	public class ExportItemsFromDownloads
	{
		[Test, Apartment(ApartmentState.STA), Category("FixNeeded" /* Not Finished */)]
		public async Task ExportAsGcode()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				string firstItemName = "Row Item Batman";
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

				testRunner.ClickByName("Queue Edit Button");
				testRunner.ClickByName(firstItemName);
				testRunner.ClickByName("Queue Export Button");
				testRunner.Wait(2);

				testRunner.WaitForName("Export Item Window", 2);
				testRunner.ClickByName("Export as GCode Button", 2);
				testRunner.Wait(2);

				string gcodeExportPath = MatterControlUtilities.PathToExportGcodeFolder;
				testRunner.Type(gcodeExportPath);
				testRunner.Type("{Enter}");
				testRunner.Wait(2);

				Console.WriteLine(gcodeExportPath);

				Assert.IsTrue(File.Exists(gcodeExportPath) == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}
