using System;
using System.Threading;
using System.Threading.Tasks;
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
		[Test, Apartment(ApartmentState.STA)]
		public async Task PrinterNameStaysChanged()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				Assert.IsTrue(testRunner.ClickByName("Printer Tab", 1), "Click Printer Tab");

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
				Assert.IsTrue(testRunner.NameExists(newName + " Menu Item"), "Widget with updated printer name exists");

				//Make sure the Active profile name changes as well
				Assert.IsTrue(ProfileManager.Instance.ActiveProfile.Name == newName, "ActiveProfile has updated name");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}
