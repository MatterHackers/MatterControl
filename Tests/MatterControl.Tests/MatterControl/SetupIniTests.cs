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
			string pathToPrinterSettings = @"StaticData\Profiles";
			var fullPathToPrinterSettings = Path.Combine(allConfigFile, pathToPrinterSettings);

			DirectoryInfo test = new DirectoryInfo(fullPathToPrinterSettings);

			var fileList = test.GetFiles("*.printer", System.IO.SearchOption.AllDirectories);

			foreach(FileInfo file in fileList)
			{
				Debug.WriteLine(file.FullName);

				var lines = File.ReadAllLines(file.FullName);

				if(!lines.Contains("default_material_presets", new LineEqualityComparer())|| lines.Contains("default_quality_presets", new LineEqualityComparer()))
				{
					Debug.WriteLine(file.Name);
				}

			}
		}
		//We are making sure that a line contains a string but we dont want it to be equal so we are changing the check
		class LineEqualityComparer : IEqualityComparer<string>
		{
			public bool Equals(string x, string y)
			{
				return x.Contains(y);
			}

			public int GetHashCode(string obj)
			{
				return obj.GetHashCode();
			}
		}


	}
}
