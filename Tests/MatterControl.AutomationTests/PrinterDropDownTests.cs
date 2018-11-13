using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PrinterNameChangePersists
	{
		[Test]
		public async Task PrinterNameChangeTest()
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

				var printer = ApplicationController.Instance.ActivePrinters.First();
				string printerID = printer.Settings.ID;

				// Wait for change
				testRunner.WaitFor(() => newName == ProfileManager.Instance[printerID].Name);

				// Validate that the model reflects the new name
				Assert.AreEqual(newName, ProfileManager.Instance[printerID].Name, "ActiveProfile has updated name");

				// Validate that the treeview reflects the new name
				testRunner.SwitchToHardwareTab();
				Assert.IsTrue(testRunner.WaitForName(newName + " Node"), "Widget with updated printer name exists");

				// Validate that the tab reflects the new name
				var printerTab = testRunner.GetWidgetByName("3D View Tab", out _) as ChromeTab;
				Assert.AreEqual(newName, printerTab.Title);

				// Validate that the settings layer reflects the new name
				Assert.AreEqual(newName, printer.Settings.GetValue(SettingsKey.printer_name));


				return Task.CompletedTask;
			});
		}
	}
}
