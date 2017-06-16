using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.DataStorage;
using System.IO;
using System;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class MatterSliceInfo
	{
		public static string DisplayName { get; } = "MatterSlice";

		public static string GetEnginePath()
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

		private static string getWindowsPath()
		{
			string matterSliceRelativePath = Path.Combine(".", "MatterSlice.exe");
			return Path.GetFullPath(matterSliceRelativePath);
		}

		private static string getMacPath()
		{
			string applicationPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationPath, "MatterSlice");
			return applicationPath;
		}

		private static string getLinuxPath()
		{
			string matterSliceRelativePath = Path.Combine(".", "MatterSlice.exe");
			return Path.GetFullPath(matterSliceRelativePath);
		}
	}
}