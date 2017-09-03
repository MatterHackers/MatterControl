/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	// this class is so that it is not passed by value
	public class ProbePosition
	{
		public Vector3 position;
	}

	public class LevelWizardBase : SystemWindow
	{
		private LevelingStrings levelingStrings = new LevelingStrings();

		public enum RuningState { InitialStartupCalibration, UserRequestedCalibration }

		protected WizardControl printLevelWizard;

		protected int totalSteps { get; private set; }

		public LevelWizardBase(int width, int height, int totalSteps)
			: base(width, height)
		{
			AlwaysOnTopOfMain = true;
			this.totalSteps = totalSteps;
		}

		public static List<Vector2> GetManualPositions(string settingsValue, int requiredCount)
		{
			// can look like "0,1:100,2:50,101"
			if (!string.IsNullOrEmpty(settingsValue))
			{
				var coordinates = settingsValue.Split(':');
				if(coordinates.Length == requiredCount)
				{
					var result = new List<Vector2>();
					foreach(var coordinate in coordinates)
					{
						var xyData = coordinate.Split(',');
						if(xyData.Length != 2)
						{
							// bad data
							return null;
						}

						Vector2 probePosition = new Vector2();
						if (!double.TryParse(xyData[0], out probePosition.x))
						{
							// error
							return null;
						}
						if (!double.TryParse(xyData[1], out probePosition.y))
						{
							// error
							return null;
						}
						result.Add(probePosition);
					}
					if (result.Count == requiredCount)
					{
						return result;
					}
				}
			}
			return null;
		}

		public static Vector2 GetPrintLevelPositionToSample(int index)
		{
			var manualPositions = GetManualPositions(ActiveSliceSettings.Instance.GetValue(SettingsKey.leveling_manual_positions), 3);
			if(manualPositions != null)
			{
				return manualPositions[index];
			}

			Vector2 bedSize = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size);
			Vector2 printCenter = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);

			switch (ActiveSliceSettings.Instance.GetValue<BedShape>(SettingsKey.bed_shape))
			{
				case BedShape.Circular:
					Vector2 firstPosition = new Vector2(printCenter.x, printCenter.y + (bedSize.y / 2) * .5);
					switch (index)
					{
						case 0:
							return firstPosition;

						case 1:
							return Vector2.Rotate(firstPosition, MathHelper.Tau / 3);

						case 2:
							return Vector2.Rotate(firstPosition, MathHelper.Tau * 2 / 3);

						default:
							throw new IndexOutOfRangeException();
					}

				case BedShape.Rectangular:
				default:
					switch (index)
					{
						case 0:
							return new Vector2(printCenter.x, printCenter.y + (bedSize.y / 2) * .8);

						case 1:
							return new Vector2(printCenter.x - (bedSize.x / 2) * .8, printCenter.y - (bedSize.y / 2) * .8);

						case 2:
							return new Vector2(printCenter.x + (bedSize.x / 2) * .8, printCenter.y - (bedSize.y / 2) * .8);

						default:
							throw new IndexOutOfRangeException();
					}
			}
		}

		private static SystemWindow printLevelWizardWindow;

		public static void ShowPrintLevelWizard(PrinterConnection printerConnection)
		{
			LevelWizardBase.RuningState runningState = LevelWizardBase.RuningState.UserRequestedCalibration;

			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
			{
				// run in the first run state
				runningState = LevelWizardBase.RuningState.InitialStartupCalibration;
			}

			ShowPrintLevelWizard(printerConnection, runningState);
		}

		public static void ShowPrintLevelWizard(PrinterConnection printerConnection, LevelWizardBase.RuningState runningState)
		{
			if (printLevelWizardWindow == null)
			{
				printLevelWizardWindow = LevelWizardBase.CreateAndShowWizard(printerConnection, runningState);
				printLevelWizardWindow.Closed += (sender, e) =>
				{
					printLevelWizardWindow = null;

					// make sure we raise the probe on close 
					if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_z_probe)
						&& ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.use_z_probe)
						&& ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_z_servo))
					{
						// make sure the servo is retracted
						var servoRetract = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.z_servo_retracted_angle);
						printerConnection.SendLineToPrinterNow($"M280 P0 S{servoRetract}");
					}
				};
			}
			else
			{
				printLevelWizardWindow.BringToFront();
			}
		}

		private static LevelWizardBase CreateAndShowWizard(PrinterConnection printerConnection, LevelWizardBase.RuningState runningState)
		{
			// turn off print leveling
			ActiveSliceSettings.Instance.Helpers.DoPrintLeveling(false);
			// clear any data that we are going to be acquiring (sampled positions, after z home offset)
			PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
			levelingData.SampledPositions.Clear();
			ActiveSliceSettings.Instance.SetValue(SettingsKey.baby_step_z_offset, "0");

			ApplicationController.Instance.ReloadAdvancedControlsPanel();

			LevelWizardBase printLevelWizardWindow;
			switch (levelingData.CurrentPrinterLevelingSystem)
			{
				case PrintLevelingData.LevelingSystem.Probe3Points:
					printLevelWizardWindow = new LevelWizard3Point(printerConnection, runningState);
					break;

				case PrintLevelingData.LevelingSystem.Probe7PointRadial:
					printLevelWizardWindow = new LevelWizard7PointRadial(printerConnection, runningState);
					break;

				case PrintLevelingData.LevelingSystem.Probe13PointRadial:
					printLevelWizardWindow = new LevelWizard13PointRadial(printerConnection, runningState);
					break;

				case PrintLevelingData.LevelingSystem.Probe3x3Mesh:
					printLevelWizardWindow = new LevelWizard3x3Mesh(printerConnection, runningState);
					break;

				default:
					throw new NotImplementedException();
			}

			printLevelWizardWindow.ShowAsSystemWindow();
			return printLevelWizardWindow;
		}
	}

	public class PrintLevelingInfo
	{
	}
}