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
	public class MatterSliceInfo : SliceEngineInfo
	{
		public MatterSliceInfo()
			:base("MatterSlice", getWindowsPath(), getMacPath(), getLinuxPath())
		{
		}
			
		public static string getWindowsPath()
		{
			string materSliceRelativePath = Path.Combine(".", "MatterSlice.exe");
			return Path.GetFullPath(materSliceRelativePath);
		}

		public static string getMacPath()
		{
			string applicationPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationPath, "MatterSlice");
			return applicationPath;
		}

		public static string getLinuxPath()
		{
			string materSliceRelativePath = Path.Combine(".", "MatterSlice.exe");
			return Path.GetFullPath(materSliceRelativePath);
		}

	}
}

