using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.DataStorage;
using System.IO;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class MatterSliceInfo : SliceEngineInfo
	{
		public MatterSliceInfo()
			: base(MatterSliceInfo.DisplayName)
		{
		}

		public static string DisplayName = "MatterSlice";

		public override SlicingEngineTypes GetSliceEngineType()
		{
			return SlicingEngineTypes.MatterSlice;
		}

		public override bool Exists()
		{
			if (OsInformation.OperatingSystem == OSType.Android || OsInformation.OperatingSystem == OSType.Mac || SlicingQueue.runInProcess)
			{
				return true;
			}
			else
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
		}

		protected override string getWindowsPath()
		{
			string matterSliceRelativePath = Path.Combine(".", "MatterSlice.exe");
			return Path.GetFullPath(matterSliceRelativePath);
		}

		protected override string getMacPath()
		{
			string applicationPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationPath, "MatterSlice");
			return applicationPath;
		}

		protected override string getLinuxPath()
		{
			string matterSliceRelativePath = Path.Combine(".", "MatterSlice.exe");
			return Path.GetFullPath(matterSliceRelativePath);
		}
	}
}