using MatterHackers.MatterControl;
using NUnit.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Globalization;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	class PrinterWhiteListTests
	{
		[Test, Category("PrinterWhiteListTests")]
		public void DesktopCalibrationPartsInSettings()
		{

			string settingsJsonPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "StaticData", "OEMSettings", "Settings.json"));

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

			string samplePartsPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "StaticData", "OEMSettings", "SampleParts"));
			string[] files = Directory.GetFiles(samplePartsPath);
			bool hasTabletStand = files.Where(l => l.Contains("MatterControl - Stand.stl")).Any();
			bool hasCoin = files.Where(l => l.Contains("MatterControl - Coin.stl")).Any();
			Assert.IsTrue(hasCoin, "Expected coin file not found");
			Assert.IsTrue(hasTabletStand, "Expected stand file not found");

		}


	}
}
