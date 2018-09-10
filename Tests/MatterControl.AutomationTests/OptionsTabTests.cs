using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("Agg.UI.Automation"), Apartment(ApartmentState.STA), RunInApplicationDomain]
	public class ShowTerminalButtonClickedOpensTerminal
	{
		[Test]
		public async Task ClickingShowTerminalButtonOpensTerminal()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				Assert.IsFalse(testRunner.WaitForName("TerminalWidget", 0.5), "Terminal Window should not exist");

				// when we start up a new session the Terminal Sidebar should not be present
				Assert.IsFalse(testRunner.WaitForName("Terminal Sidebar", 0.5), "Terminal Sidebar should not exist");

				testRunner.SwitchToTerminalTab();

				Assert.IsTrue(testRunner.WaitForName("TerminalWidget"), "Terminal Window should exists after Show Terminal button is clicked");

				return Task.CompletedTask;
			});
		}
	}
}
