using System;
using System.Threading;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.SlicerConfiguration;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class PrinterNameChangePersists
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void PrinterNameStaysChanged()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);

				// Now do the actions specific to this test. (replace this for new tests)
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

					MatterControlUtilities.SwitchToAdvancedSettings(testRunner, resultsHarness);

					resultsHarness.AddTestResult(testRunner.ClickByName("Printer Tab", 1));

					string widgetName = "Printer Name Edit";
					testRunner.ClickByName(widgetName);

					SystemWindow window;
					var textWidget = testRunner.GetWidgetByName(widgetName, out window);
					string newName = "Updated name";
					textWidget.Text = newName;
					testRunner.ClickByName("Printer Tab", 1);
					testRunner.Wait(4);

					//Check to make sure the Printer dropdown gets the name change 
					testRunner.ClickByName("Printers... Menu", 2);
					testRunner.Wait(1);
					resultsHarness.AddTestResult(testRunner.NameExists(newName + " Menu Item"));
					//Make sure the Active profile name changes as well
					resultsHarness.AddTestResult(ProfileManager.Instance.ActiveProfile.Name == newName);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(7));
		}
	}
}
