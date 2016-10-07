using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Category("FixNeeded" /* Not Finished */)]
	public class ExportItemsFromDownloads
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void ExportAsGcode()
		{
			// Run a copy of MatterControl
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

					string firstItemName = "Row Item Batman";
					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Queue Tab");
					testRunner.ClickByName("Queue Add Button", 2);

					//Get parts to add
					string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");

					//Add STL part items to Downloads and then type paths into file dialogue
					testRunner.Wait(1);
					testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					//Get test results 
					testRunner.AddTestResult(testRunner.WaitForName(firstItemName, 2) == true);

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

					testRunner.AddTestResult(File.Exists(gcodeExportPath) == true);
					Debugger.Break();

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);

			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}
}
