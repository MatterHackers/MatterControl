using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.UI;
using NUnit.Framework;
using MatterHackers.GuiAutomation;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.Tests.Automation
{

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class PrinterNameChangePersists
	{
		[Test, RequiresSTA, RunInApplicationDomain]
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
					testRunner.Wait(2);
					testRunner.ClickByName("SettingsAndControls");
					testRunner.ClickByName("Settings Tab");

					resultsHarness.AddTestResult(testRunner.ClickByName("User Level Dropdown", 1));
					resultsHarness.AddTestResult(testRunner.ClickByName("Advanced Menu Item", 1));
					testRunner.Wait(.5);
					resultsHarness.AddTestResult(testRunner.ClickByName("Printer Tab", 1));

					string widgetName = "Printer Name Edit";
					testRunner.ClickByName(widgetName);

					SystemWindow window;
					var textWidget = testRunner.GetWidgetByName(widgetName, out window);
					string newName = "Updated name";

					textWidget.Text = newName;
					testRunner.ClickByName("Printer Tab", 1);

					testRunner.Wait(5);
					
					resultsHarness.AddTestResult(ProfileManager.Instance.ActiveProfile.Name == newName);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 4); // make sure we ran all our tests
		}
	}

}
