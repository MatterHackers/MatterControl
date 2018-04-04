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
	[JsonConverter(typeof(StringEnumConverter))]
	public enum LevelingSystem { Probe3Points, Probe7PointRadial, Probe13PointRadial, Probe3x3Mesh, Probe5x5Mesh }

	public class PrintLevelingData
	{
		#region JSON data
		public List<Vector3> SampledPositions = new List<Vector3>();
		public LevelingSystem LevelingSystem;
		public DateTime CreationData;
		public double BedTemperature;
		#endregion

		public PrintLevelingData()
		{
		}

		public bool HasBeenRunAndEnabled(PrinterConfig printer)
		{
			// check if leveling is turned on
			if(!ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled))
			{
				return false;
			}

			// check that there are no duplicate points
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

			// check that the solution last measured is the currently selected solution
			if(printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution) != LevelingSystem)
			{
				return false;
			}

			// check that the bed temperature at probe time was close enough to the current print bed temp
			double reqiredLevlingTemp = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed) ?
				printer.Settings.GetValue<double>(SettingsKey.bed_temperature)
				: 0;

			// check that it is within 10 degrees
			if(Math.Abs(reqiredLevlingTemp - BedTemperature) > 10)
			{
				return false;
			}


			// check that the number of poins sampled is correct for the solution
			switch (LevelingSystem)
			{
				case LevelingSystem.Probe3Points:
					if (SampledPositions.Count != 3) // different criteria for what is not initialized
					{
						return false;
					}
					break;

				case LevelingSystem.Probe7PointRadial:
					if (SampledPositions.Count != 7) // different criteria for what is not initialized
					{
						return false;
					}
					break;

				case LevelingSystem.Probe13PointRadial:
					if (SampledPositions.Count != 13) // different criteria for what is not initialized
					{
						return false;
					}
					break;

				case LevelingSystem.Probe3x3Mesh:
					if (SampledPositions.Count != 9) // different criteria for what is not initialized
					{
						return false;
					}
					break;

				case LevelingSystem.Probe5x5Mesh:
					if (SampledPositions.Count != 25) // different criteria for what is not initialized
					{
						return false;
					}
					break;

				default:
					throw new NotImplementedException();
			}

			return true;
		}

		public bool SamplesAreSame(List<Vector3> sampledPositions)
		{
			if (sampledPositions.Count == SampledPositions.Count)
			{
				for (int i = 0; i < sampledPositions.Count; i++)
				{
					if (sampledPositions[i] != SampledPositions[i])
					{
						return false;
					}
				}

				return true;
			}

			return false;
		}
	}
}