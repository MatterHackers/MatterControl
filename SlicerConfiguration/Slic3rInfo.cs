using MatterHackers.MatterControl.DataStorage;
using System.IO;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class Slic3rInfo : SliceEngineInfo
	{
		public Slic3rInfo()
			: base("Slic3r")
		{
		}

		public override SlicingEngineTypes GetSliceEngineType()
		{
			return SlicingEngineTypes.Slic3r;
		}

		protected override string getWindowsPath()
		{
			string slic3rRelativePathWindows = Path.Combine("..", "Slic3r", "slic3r.exe");
			if (!File.Exists(slic3rRelativePathWindows))
			{
				slic3rRelativePathWindows = Path.Combine(".", "Slic3r", "slic3r.exe");
			}
			return Path.GetFullPath(slic3rRelativePathWindows);
		}

		protected override string getMacPath()
		{
			string applicationPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationPath, "Slic3r.app", "Contents", "MacOS", "slic3r");
			return applicationPath;
		}

		protected override string getLinuxPath()
		{
			string slic3rRelativePathWindows = Path.Combine("..", "Slic3r", "bin","slic3r");
			if (!File.Exists(slic3rRelativePathWindows))
			{
				slic3rRelativePathWindows = Path.Combine(".", "Slic3r", "bin","slic3r");
			}
			return Path.GetFullPath(slic3rRelativePathWindows);
		}
	}
}