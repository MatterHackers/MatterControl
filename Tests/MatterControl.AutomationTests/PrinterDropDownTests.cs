using System.Threading;
using System.Threading.Tasks;
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

				testRunner.SwitchToPrinterSettings();

				// Change the printer name
				string newName = "Updated name";
				testRunner.InlineTitleEdit("Printer Name", newName);
				testRunner.WaitFor(() => newName == ProfileManager.Instance.ActiveProfile.Name);

				// Validate that the model reflects the new name
				Assert.AreEqual(newName, ProfileManager.Instance.ActiveProfile.Name, "ActiveProfile has updated name");

				// Validate that the treeview reflects the new name
				testRunner.SwitchToHardwareTab();
				Assert.IsTrue(testRunner.WaitForName(newName + " Node"), "Widget with updated printer name exists");

				// Validate that the tab reflects the new name
				var printerTab = testRunner.GetWidgetByName("3D View Tab", out _);
				Assert.AreEqual(newName, printerTab.Text);

				return Task.CompletedTask;
			});
		}
	}
}
