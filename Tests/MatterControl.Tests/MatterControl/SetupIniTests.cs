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
	public class SetupIniTests
	{

		[Test, Category("SetupIni")]
		public void SetUpIniTests()
		{

			DirectoryInfo currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
			var allConfigFile = currentDirectory.Parent.Parent.Parent.Parent.FullName;
			string pathToPrinterSettings = @"StaticData\PrinterSettings";
			var fullPathToPrinterSettings = Path.Combine(allConfigFile, pathToPrinterSettings);

			DirectoryInfo test = new DirectoryInfo(fullPathToPrinterSettings);

			IEnumerable<FileInfo> fileList = test.GetFiles(".", System.IO.SearchOption.AllDirectories);

			var allPrinterConfigs = fileList.Where(file => file.Name == "setup.ini");

			foreach(FileInfo file in allPrinterConfigs)
			{
				Debug.WriteLine(file.FullName);

				foreach(string line in File.ReadLines(file.FullName))
				{
					string needsPrintLeveling = "default_material_presets";
					string printLevelingType = "default_quality_preset";

					if(!line.Contains(needsPrintLeveling) || line.Contains(printLevelingType))
					{
						Debug.WriteLine(line);
					}
				}
			}
		}

	}
}
