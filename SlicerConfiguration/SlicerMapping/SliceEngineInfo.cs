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
	public abstract class SliceEngineInfo
	{

		private string displayName;
		private string slicePathWindows;
		private string slicePathMac;
		private string slicePathLinux;

		public SliceEngineInfo()
		{

		}

		public SliceEngineInfo(string name, string winPath, string macPath, string linuxPath)
		{
			displayName = name;
			slicePathWindows = winPath;
			slicePathMac = macPath;
			slicePathLinux = linuxPath;
		}


		public string PathOnWindows
		{
			get
			{
				return slicePathWindows;
			}
			set
			{
				slicePathWindows = value;
			
			}
		}

		public string PathOnMac
		{
			get
			{
				return slicePathMac;
			}
			set
			{
				slicePathMac = value;

			}
		}

		public string PathOnLinux
		{
			get
			{
				return slicePathLinux;
			}
			set
			{
				slicePathLinux = value;
			}
		}

		public string Name
		{
			get
			{
				return displayName;
			}
			set
			{
				displayName = value; 
			}
		}
	}
}

