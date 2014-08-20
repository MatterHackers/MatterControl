using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class Slic3rInfo : SliceEngineInfo
	{
	
		public Slic3rInfo() 
			: base("Slic3r", getWindowsPath(), getMacPath(), getLinuxPath())
		{
		}

		public static string getWindowsPath()
		{
			string slic3rRelativePathWindows = Path.Combine("..", "Slic3r", "slic3r.exe");
			if (!File.Exists(slic3rRelativePathWindows))
			{
				slic3rRelativePathWindows = Path.Combine(".", "Slic3r", "slic3r.exe");
			}
			return Path.GetFullPath(slic3rRelativePathWindows);
		}

		public static string getMacPath()
		{
			string applicationPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationPath, "Slic3r.app", "Contents", "MacOS", "slic3r");
			return applicationPath;
		}

		public static string getLinuxPath()
		{
			string slic3rRelativePathWindows = Path.Combine("..", "Slic3r", "slic3r.exe");
			if (!File.Exists(slic3rRelativePathWindows))
			{
				slic3rRelativePathWindows = Path.Combine(".", "Slic3r", "slic3r.exe");
			}
			return Path.GetFullPath(slic3rRelativePathWindows);
		}
	}
}

