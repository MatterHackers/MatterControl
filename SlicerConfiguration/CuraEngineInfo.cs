using MatterHackers.MatterControl.DataStorage;
using System.IO;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class CuraEngineInfo : SliceEngineInfo
	{
		public CuraEngineInfo()
			: base("CuraEngine")
		{
		}

		public override SlicingEngineTypes GetSliceEngineType()
		{
			return SlicingEngineTypes.CuraEngine;
		}

		protected override string getWindowsPath()
		{
			string curaEngineRelativePath = Path.Combine("..", "CuraEngine.exe");
			if (!File.Exists(curaEngineRelativePath))
			{
				curaEngineRelativePath = Path.Combine(".", "CuraEngine.exe");
			}
			return Path.GetFullPath(curaEngineRelativePath);
		}

		protected override string getMacPath()
		{
			string applicationPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationPath, "CuraEngine");
			return applicationPath;
		}

		protected override string getLinuxPath()
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