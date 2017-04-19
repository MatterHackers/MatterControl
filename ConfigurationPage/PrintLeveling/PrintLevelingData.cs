using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System;
using System.Linq;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class PrintLevelingData
	{
		private PrinterSettings printerProfile;

		public List<Vector3> SampledPositions = new List<Vector3>()
		{
			new Vector3(),new Vector3(),new Vector3()
		};

		[JsonConverter(typeof(StringEnumConverter))]
		public enum LevelingSystem { Probe3Points, Probe7PointRadial, Probe13PointRadial, Probe3x3Mesh }

		public PrintLevelingData(PrinterSettings printerProfile)
		{
			this.printerProfile = printerProfile;
		}

		public LevelingSystem CurrentPrinterLevelingSystem
		{
			get
			{
				switch (printerProfile.GetValue("print_leveling_solution"))
				{
					case "7 Point Disk":
						return LevelingSystem.Probe7PointRadial;

					case "13 Point Disk":
						return LevelingSystem.Probe13PointRadial;

					case "3 Point Plane":
					default:
						return LevelingSystem.Probe3Points;
				}
			}
		}

		internal static PrintLevelingData Create(PrinterSettings printerProfile, string jsonData, string depricatedPositionsCsv3ByXYZ)
		{
			if (!string.IsNullOrEmpty(jsonData))
			{
				var deserialized = JsonConvert.DeserializeObject<PrintLevelingData>(jsonData);
				deserialized.printerProfile = printerProfile;

				return deserialized;
			}
			else if (!string.IsNullOrEmpty(depricatedPositionsCsv3ByXYZ))
			{
				var item = new PrintLevelingData(ActiveSliceSettings.Instance);
				item.printerProfile = printerProfile;
				item.ParseDepricatedPrintLevelingMeasuredPositions(depricatedPositionsCsv3ByXYZ);

				return item;
			}
			else
			{
				return new PrintLevelingData(ActiveSliceSettings.Instance)
				{
					printerProfile = printerProfile
				};
			}
		}

		/// <summary>
		/// Gets the 9 {3 * (x, y, z)} positions that were probed during the print leveling setup.
		/// </summary>
		/// <returns></returns>
		private void ParseDepricatedPrintLevelingMeasuredPositions(string depricatedPositionsCsv3ByXYZ)
		{
			SampledPositions = new List<Vector3>(3);

			if (depricatedPositionsCsv3ByXYZ != null)
			{
				string[] lines = depricatedPositionsCsv3ByXYZ.Split(',');
				if (lines.Length == 9)
				{
					for (int i = 0; i < 3; i++)
					{
						Vector3 position = new Vector3();

						position.x = double.Parse(lines[0 * 3 + i]);
						position.y = double.Parse(lines[1 * 3 + i]);
						position.z = double.Parse(lines[2 * 3 + i]);

						SampledPositions.Add(position);
					}
				}
			}
		}

		public bool HasBeenRunAndEnabled()
		{
			if(!ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled))
			{
				return false;
			}

			var positionCounts = from x in SampledPositions
				group x by x into g
				let count = g.Count()
				orderby count descending
				select new { Value = g.Key, Count = count };
			
			foreach (var x in positionCounts)
			{
				if(x.Count > 1)
				{
					return false;
				}
			}


			switch (CurrentPrinterLevelingSystem)
			{
				case PrintLevelingData.LevelingSystem.Probe3Points:
					if (SampledPositions.Count != 3) // different criteria for what is not initialized
					{
						return false;
					}
					break;

				case PrintLevelingData.LevelingSystem.Probe7PointRadial:
					if (SampledPositions.Count != 7) // different criteria for what is not initialized
					{
						return false;
					}
					break;

				case PrintLevelingData.LevelingSystem.Probe13PointRadial:
					if (SampledPositions.Count != 13) // different criteria for what is not initialized
					{
						return false;
					}
					break;

				default:
					throw new NotImplementedException();
			}

			return true;
		}
	}
}