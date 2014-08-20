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
	public class CuraEngineInfo : SliceEngineInfo
	{
		public CuraEngineInfo()
			:base("CuraEngine", getWindowsPath(), getMacPath(), getLinuxPath())
		{
		}


        public override ActivePrinterProfile.SlicingEngineTypes GetSliceEngineType()
        {
            return ActivePrinterProfile.SlicingEngineTypes.CuraEngine;
        }

		public static string getWindowsPath()
		{
			string curaEngineRelativePath = Path.Combine("..", "CuraEngine.exe");
			if (!File.Exists(curaEngineRelativePath))
			{
				curaEngineRelativePath = Path.Combine(".", "CuraEngine.exe");
			}
			return Path.GetFullPath(curaEngineRelativePath);
		}

		public static string getMacPath()
		{
			string applicationPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationPath, "CuraEngine");
			return applicationPath;
		}

		public static string getLinuxPath()
		{
			string curaEngineRelativePath = Path.Combine("..", "CuraEngine.exe");
			if (!File.Exists(curaEngineRelativePath))
			{
				curaEngineRelativePath = Path.Combine(".", "CuraEngine.exe");
			}
			return Path.GetFullPath(curaEngineRelativePath);
		}
	}
}

