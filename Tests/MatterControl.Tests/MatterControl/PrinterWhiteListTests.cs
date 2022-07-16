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
			string settingsJsonPath = Path.Combine(MatterControlUtilities.StaticDataPath, "OEMSettings", "Settings.json");

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
			string samplePartsPath = Path.Combine(MatterControlUtilities.StaticDataPath, "OEMSettings", "SampleParts");
			string[] files = Directory.GetFiles(samplePartsPath);
			bool hasPhil = files.Where(l => l.Contains("Phil A Ment.stl")).Any();
			Assert.IsTrue(hasPhil, "Expected Phil file not found");
		}
	}
}
