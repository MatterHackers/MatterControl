using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PrinterEmulator;
using MatterHackers.VectorMath;
using NUnit.Framework;
using static MatterHackers.MatterControl.PrinterCommunication.PrinterConnection;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation")]
	public class PrinterConnectionTests
	{
		[Test, ChildProcessTest]
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
					testRunner.WaitFor(() => printer.Connection.CommunicationState == PrinterCommunication.CommunicationStates.Disconnected);
					Assert.AreEqual(CommunicationStates.Disconnected, printer.Connection.CommunicationState, "Printer should be Disconnected after closing printer tab");

					Assert.AreEqual(0, ReadThread.NumRunning, "No ReadThread instances should be running when only printer Disconnected");
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}
	}
}
