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
	public enum LevelingSystem { Probe3Points, Probe7PointRadial, Probe13PointRadial, Probe100PointRadial, Probe3x3Mesh, Probe5x5Mesh, Probe10x10Mesh }

	public class PrintLevelingData
	{
		#region JSON data
		public List<Vector3> SampledPositions = new List<Vector3>();
		public LevelingSystem LevelingSystem;
		public DateTime CreationDate;
		public double BedTemperature;
		#endregion

		public PrintLevelingData()
		{
		}

		public static bool NeedsToBeRun(PrinterConfig printer)
		{
			PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();

			var required = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print);
			if (required && levelingData == null)
			{
				// need but don't have data
				return true;
			}

			var enabled = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled);
			// check if leveling is turned on
			if (required && !enabled)
			{
				// need but not turned on
				return true;
			}

			if(!required && !enabled)
			{
				return false;
			}

			// check that there are no duplicate points
			var positionCounts = from x in levelingData.SampledPositions
				group x by x into g
				let count = g.Count()
				orderby count descending
				select new { Value = g.Key, Count = count };

			foreach (var x in positionCounts)
			{
				if(x.Count > 1)
				{
					return true;
				}
			}

			// check that the solution last measured is the currently selected solution
			if(printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution) != levelingData.LevelingSystem)
			{
				return true;
			}

			// check that the bed temperature at probe time was close enough to the current print bed temp
			double requiredLevelingTemp = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed) ?
				printer.Settings.GetValue<double>(SettingsKey.bed_temperature)
				: 0;

			// check that it is within 10 degrees
			if(Math.Abs(requiredLevelingTemp - levelingData.BedTemperature) > 10)
			{
				return true;
			}

			// check that the number of points sampled is correct for the solution
			switch (levelingData.LevelingSystem)
			{
				case LevelingSystem.Probe3Points:
					if (levelingData.SampledPositions.Count != 3) // different criteria for what is not initialized
					{
						return true;
					}
					break;

				case LevelingSystem.Probe7PointRadial:
					if (levelingData.SampledPositions.Count != 7) // different criteria for what is not initialized
					{
						return true;
					}
					break;

				case LevelingSystem.Probe13PointRadial:
					if (levelingData.SampledPositions.Count != 13) // different criteria for what is not initialized
					{
						return true;
					}
					break;

				case LevelingSystem.Probe100PointRadial:
					if (levelingData.SampledPositions.Count != 100) // different criteria for what is not initialized
					{
						return true;
					}
					break;

				case LevelingSystem.Probe3x3Mesh:
					if (levelingData.SampledPositions.Count != 9) // different criteria for what is not initialized
					{
						return true;
					}
					break;

				case LevelingSystem.Probe5x5Mesh:
					if (levelingData.SampledPositions.Count != 25) // different criteria for what is not initialized
					{
						return true;
					}
					break;

				case LevelingSystem.Probe10x10Mesh:
					if (levelingData.SampledPositions.Count != 100) // different criteria for what is not initialized
					{
						return true;
					}
					break;

				default:
					throw new NotImplementedException();
			}

			// All the above need to pass, as well as all rules defined in ProbeCalibrationWizard - any variance and we need to re-run
			return ProbeCalibrationWizard.NeedsToBeRun(printer);
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