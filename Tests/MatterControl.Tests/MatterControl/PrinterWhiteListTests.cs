using System.IO;
using System.Linq;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	class PrinterWhiteListTests
	{
		[Test, Category("PrinterWhiteListTests")]
		public void DesktopCalibrationPartsInSettings()
		{
			string settingsJsonPath = TestContext.CurrentContext.ResolveProjectPath(4, "StaticData", "OEMSettings", "Settings.json");

			if (File.Exists(settingsJsonPath))
			{
				string[] lines = File.ReadAllLines(settingsJsonPath);
				bool hasCoin = lines.Where(l => l.Contains("\"MatterControl - Coin.stl\",")).Any();
				bool hasTabletStand = lines.Where(l => l.Contains("\"MatterControl - Stand.stl\",")).Any();

				Assert.IsTrue(hasCoin, "Expected coin file not found");
				Assert.IsTrue(hasTabletStand, "Expected stand file not found");
			}
		}

		[Test, Category("SamplePartsTests")]
		public void DesktopCalibrationPartsExist()
		{
			string samplePartsPath = TestContext.CurrentContext.ResolveProjectPath(4, "StaticData", "OEMSettings", "SampleParts");
			string[] files = Directory.GetFiles(samplePartsPath);
			bool hasTabletStand = files.Where(l => l.Contains("MatterControl - Stand.stl")).Any();
			bool hasCoin = files.Where(l => l.Contains("MatterControl - Coin.stl")).Any();
			Assert.IsTrue(hasCoin, "Expected coin file not found");
			Assert.IsTrue(hasTabletStand, "Expected stand file not found");
		}
	}
}
