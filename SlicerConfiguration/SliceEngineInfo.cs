using MatterHackers.Agg.PlatformAbstract;
using System;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public abstract class SliceEngineInfo
	{
		public string Name { get; set; }

		protected abstract string getWindowsPath();

		protected abstract string getMacPath();

		protected abstract string getLinuxPath();

		public abstract SlicingEngineTypes GetSliceEngineType();

		public SliceEngineInfo(string name)
		{
			this.Name = name;
		}

		public virtual bool Exists()
		{
			if (this.GetEnginePath() == null)
			{
				return false;
			}
			else
			{
				return System.IO.File.Exists(this.GetEnginePath());
			}
		}

		public string GetEnginePath()
		{
			switch (OsInformation.OperatingSystem)
			{
				case OSType.Windows:
					return getWindowsPath();

				case OSType.Mac:
					return getMacPath();

				case OSType.X11:
					return getLinuxPath();

				case OSType.Android:
					return null;

				default:
					throw new NotImplementedException();
			}
		}
	}
}