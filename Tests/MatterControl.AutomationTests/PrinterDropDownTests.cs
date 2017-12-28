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
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PrinterNameChangePersists
	{
		[Test]
		public async Task PrinterNameStaysChanged()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				testRunner.SwitchToPrinterSettings();

				testRunner.ClickByName("Printer Name Field");

				var textWidget = testRunner.GetWidgetByName("Printer Name Field", out _);
				string newName = "Updated name";
				textWidget.Text = newName;

				// Force loose focus
				testRunner.ClickByName("Printer Tab");
				testRunner.Delay(1);

				//Check to make sure the Printer dropdown gets the name change 
				testRunner.OpenPrintersDropdown();
				Assert.IsTrue(testRunner.WaitForName(newName + " Menu Item"), "Widget with updated printer name exists");

				//Make sure the Active profile name changes as well
				Assert.AreEqual(newName, ProfileManager.Instance.ActiveProfile.Name, "ActiveProfile has updated name");

				return Task.CompletedTask;
			});
		}
	}
}
