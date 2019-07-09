using System.Threading;
using System.Threading.Tasks;
using MatterControl.Printing;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PrinterConnectionTests
	{
		[Test]
		public async Task PrinterDisconnectedOnTabClose()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Create and connect to Airwolf via emulator port
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					var printer = testRunner.FirstPrinter();

					// Ensure connected
					Assert.AreEqual(CommunicationStates.Connected, printer.Connection.CommunicationState, "Printer should be Connected after LaunchAndConnectToPrinterEmulator");

					// Close Printer
					testRunner.CloseFirstPrinterTab();

					// Ensure disconnected
					testRunner.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.Disconnected);
					Assert.AreEqual(CommunicationStates.Disconnected, printer.Connection.CommunicationState, "Printer should be Disconnected after closing printer tab");

					Assert.AreEqual(0, PrinterConnection.ReadThread.NumRunning, "No ReadThread instances should be running when only printer Disconnected");
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}
	}
}
