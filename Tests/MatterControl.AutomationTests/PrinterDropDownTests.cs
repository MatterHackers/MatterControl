using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PrinterNameChangeTests
	{
		[Test]
		public async Task NameChangeOnlyEffectsOnePrinter()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForFirstDraw();

				// Add Guest printers
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				var printer1 = testRunner.FirstPrinter();

				testRunner.AddAndSelectPrinter("BCN3D", "Sigma");

				var printer2 = ApplicationController.Instance.ActivePrinters.Last();

				string newName0 = "Updated name 0";
				string newName1 = "Updated name 1";
				var printerTab0 = testRunner.GetWidgetByName("3D View Tab 0", out _) as ChromeTab;
				var printerTab1 = testRunner.GetWidgetByName("3D View Tab 1", out _) as ChromeTab;

				// switch back to airwolf tab
				testRunner.ClickByName("3D View Tab 0")
					.SwitchToPrinterSettings()
					.InlineTitleEdit("Printer Name", newName0);

				Assert.AreEqual(newName0, printerTab0.Title);
				Assert.AreEqual("BCN3D Sigma", printerTab1.Title);

				// switch back to BCN tab
				testRunner.ClickByName("3D View Tab 1")
					.SwitchToPrinterSettings()
					.InlineTitleEdit("Printer Name", newName1);

				Assert.AreEqual(newName1, printerTab1.Title);
				Assert.AreEqual(newName0, printerTab0.Title, "Name did not change");

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		[Test]
		public async Task NameChangePersists()
		{
			// Ensures that printer model changes are applied correctly and observed by the view
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForFirstDraw();

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should exist after add");

				testRunner.SwitchToPrinterSettings();

				// Change the printer name
				string newName = "Updated name";
				testRunner.InlineTitleEdit("Printer Name", newName);

				var printer = testRunner.FirstPrinter();
				string printerID = printer.Settings.ID;

				// Wait for change
				testRunner.WaitFor(() => newName == ProfileManager.Instance[printerID].Name);

				// Validate that the model reflects the new name
				Assert.AreEqual(newName, ProfileManager.Instance[printerID].Name, "ActiveProfile has updated name");

				// Validate that the treeview reflects the new name
				testRunner.SwitchToHardwareTab();
				Assert.IsTrue(testRunner.WaitForName(newName + " Node"), "Widget with updated printer name exists");

				// Validate that the tab reflects the new name
				var printerTab = testRunner.GetWidgetByName("3D View Tab 0", out _) as ChromeTab;
				Assert.AreEqual(newName, printerTab.Title);

				// Validate that the settings layer reflects the new name
				Assert.AreEqual(newName, printer.Settings.GetValue(SettingsKey.printer_name));

				return Task.CompletedTask;
			});
		}
	}
}
