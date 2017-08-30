using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class FileMenuTest
	{
		[Test]
		public async Task FileMenuAddPrinter()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("File Menu");
				testRunner.Delay(1);
				testRunner.ClickByName("Add Printer Menu Item");
				testRunner.Delay(1);
				Assert.IsTrue(testRunner.WaitForName("Select Make"));

				testRunner.ClickByName("Cancel Wizard Button");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}
	}
}
