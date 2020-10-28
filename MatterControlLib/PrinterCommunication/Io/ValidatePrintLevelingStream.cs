/*
Copyright (c) 2018, Lars Brubaker
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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterControl.Printing;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class ValidatePrintLevelingStream : GCodeStreamProxy
	{
		private readonly double[] babySteppingValue = new double[4];
		private readonly Queue<string> queuedCommands = new Queue<string>();
		private readonly List<double> samplesForSinglePosition = new List<double>();
		private int activeProbeIndex;
		private bool gcodeAlreadyLeveled;
		private LevelingPlan levelingPlan;
		private List<Vector2> positionsToSample;
		private Vector3 positionToSample;
		private Vector3 positionToSampleWithProbeOffset;
		private List<PrintLevelingWizard.ProbePosition> sampledPositions;

		private bool validationHasBeenRun;
		private bool validationRunning;
		private bool waitingToCompleteNextSample;
		private bool haveSeenM190;
		private bool haveSeenG28;

		public ValidatePrintLevelingStream(PrinterConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
			printer.Connection.PrintCanceled += Connection_PrintCanceled;
		}

		private void Connection_PrintCanceled(object sender, EventArgs e)
		{
			ShutdownProbing();
		}

		public override string DebugInfo => "";

		public override void Dispose()
		{
			ShutdownProbing();
			printer.Connection.PrintCanceled -= Connection_PrintCanceled;

			base.Dispose();
		}

		private void ShutdownProbing()
		{
			if (validationRunning)
			{
				validationRunning = false;
				validationHasBeenRun = true;
				haveSeenG28 = false;
				haveSeenM190 = false;

				printer.Connection.LineReceived -= GetZProbeHeight;

				if (validationRunning || validationHasBeenRun)
				{
					// If leveling was on when we started, make sure it is on when we are done.
					printer.Connection.AllowLeveling = true;

					// set the baby stepping back to the last known good value
					printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
					{
						printer.Settings.SetValue(key, babySteppingValue[i].ToString());
					});

					// make sure we raise the probe on close
					if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
						&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe)
						&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
					{
						// make sure the servo is retracted
						var servoRetract = printer.Settings.GetValue<double>(SettingsKey.z_servo_retracted_angle);
						queuedCommands.Enqueue($"M280 P0 S{servoRetract}");
					}
				}
			}
		}

		public override string ReadLine()
		{
			if (queuedCommands.Count > 0)
			{
				return queuedCommands.Dequeue();
			}

			if (validationRunning
				&& !validationHasBeenRun)
			{
				SampleProbePoints();
			}

			string lineToSend = base.ReadLine();

			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			if (lineToSend == "; Software Leveling Applied")
			{
				gcodeAlreadyLeveled = true;
			}

			if (lineToSend != null)
			{
				if (lineToSend.Contains("M190"))
				{
					haveSeenM190 = true;
				}

				if (lineToSend.Contains("G28"))
				{
					haveSeenG28 = true;
				}

				if (!validationHasBeenRun
					&& !gcodeAlreadyLeveled
					&& printer.Connection.IsConnected
					&& printer.Connection.Printing
					&& printer.Connection.CurrentlyPrintingLayer <= 0
					&& printer.Connection.ActivePrintTask?.RecoveryCount < 1
					&& printer.Settings.GetValue<bool>(SettingsKey.validate_leveling))
				{
					// we are setting the bed temp
					if (haveSeenG28 && haveSeenM190)
					{
						haveSeenG28 = false;
						haveSeenM190 = false;
						SetupForValidation();
						// still set the bed temp and wait
						return lineToSend;
					}
				}
			}

			return lineToSend;
		}

		private void GetZProbeHeight(object sender, string line)
		{
			if (line != null)
			{
				double sampleRead = double.MinValue;
				if (line.StartsWith("Bed")) // marlin G30 return code (looks like: 'Bed Position X:20 Y:32 Z:.01')
				{
					sampledPositions[activeProbeIndex].Position.X = positionToSample.X;
					sampledPositions[activeProbeIndex].Position.Y = positionToSample.Y;
					GCodeFile.GetFirstNumberAfter("Z:", line, ref sampleRead);
				}
				else if (line.StartsWith("Z:")) // smoothie G30 return code (looks like: 'Z:10.01')
				{
					sampledPositions[activeProbeIndex].Position.X = positionToSample.X;
					sampledPositions[activeProbeIndex].Position.Y = positionToSample.Y;
					// smoothie returns the position relative to the start position
					double reportedProbeZ = 0;

					GCodeFile.GetFirstNumberAfter("Z:", line, ref reportedProbeZ);
					sampleRead = positionToSample.Z - reportedProbeZ;
				}

				if (sampleRead != double.MinValue)
				{
					samplesForSinglePosition.Add(sampleRead);

					int numberOfSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);
					if (samplesForSinglePosition.Count >= numberOfSamples)
					{
						samplesForSinglePosition.Sort();
						if (samplesForSinglePosition.Count > 3)
						{
							// drop the high and low values
							samplesForSinglePosition.RemoveAt(0);
							samplesForSinglePosition.RemoveAt(samplesForSinglePosition.Count - 1);
						}

						sampledPositions[activeProbeIndex].Position.Z = Math.Round(samplesForSinglePosition.Average(), 2);

						// If we are sampling the first point, check if it is unchanged from the last time we ran leveling
						if (activeProbeIndex == 0)
						{
							var levelingData = printer.Settings.Helpers.PrintLevelingData;

							var delta = sampledPositions[activeProbeIndex].Position.Z - levelingData.SampledPositions[activeProbeIndex].Z;
							if (levelingData.SampledPositions.Count == sampledPositions.Count
								&& Math.Abs(delta) < printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter) / 10.0)
							{
								// the last leveling is still good abort this new calibration and start printing
								ShutdownProbing();
								waitingToCompleteNextSample = false;
								validationRunning = false;
								validationHasBeenRun = true;
							}
						}

						// When probe data has been collected, resume our thread to continue collecting
						waitingToCompleteNextSample = false;
						// and go on to the next point
						activeProbeIndex++;
					}
					else
					{
						// add the next request for probe
						queuedCommands.Enqueue("G30");
						// raise the probe after each sample
						var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();
						queuedCommands.Enqueue($"G1 X{positionToSampleWithProbeOffset.X:0.###}Y{positionToSampleWithProbeOffset.Y:0.###}Z{positionToSampleWithProbeOffset.Z:0.###} F{feedRates.X}");
					}
				}
			}
		}

		private void SampleProbePoints()
		{
			if (waitingToCompleteNextSample)
			{
				return;
			}

			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);

			if (activeProbeIndex < positionsToSample.Count)
			{
				var validProbePosition2D = PrintLevelingWizard.EnsureInPrintBounds(printer, positionsToSample[activeProbeIndex]);
				positionToSample = new Vector3(validProbePosition2D, startProbeHeight);

				this.SampleNextPoint();
			}
			else
			{
				SaveSamplePoints();
				ShutdownProbing();
			}
		}

		private void SaveSamplePoints()
		{
			PrintLevelingData levelingData = printer.Settings.Helpers.PrintLevelingData;
			levelingData.SampledPositions.Clear();

			for (int i = 0; i < sampledPositions.Count; i++)
			{
				levelingData.SampledPositions.Add(sampledPositions[i].Position);
			}

			levelingData.LevelingSystem = printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution);
			levelingData.CreationDate = DateTime.Now;
			// record the temp the bed was when we measured it (or 0 if no heated bed)
			levelingData.BedTemperature = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed) ?
				printer.Settings.GetValue<double>(SettingsKey.bed_temperature)
				: 0;
			levelingData.IssuedLevelingTempWarning = false;

			// Invoke setter forcing persistence of leveling data
			printer.Settings.Helpers.PrintLevelingData = levelingData;
			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				printer.Settings.SetValue(key, "0");
			});
			printer.Connection.AllowLeveling = true;
			printer.Settings.Helpers.DoPrintLeveling(true);
		}

		private void SetupForValidation()
		{
			validationRunning = true;
			activeProbeIndex = 0;

			printer.Connection.LineReceived += GetZProbeHeight;

			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				// remember the current baby stepping values
				babySteppingValue[i] = value;
				// clear them while we measure the offsets
				printer.Settings.SetValue(key, "0");
			});

			// turn off print leveling
			printer.Connection.AllowLeveling = false;

			var levelingData = new PrintLevelingData()
			{
				LevelingSystem = printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution)
			};

			switch (levelingData.LevelingSystem)
			{
				case LevelingSystem.Probe3Points:
					levelingPlan = new LevelWizard3Point(printer);
					break;

				case LevelingSystem.Probe7PointRadial:
					levelingPlan = new LevelWizard7PointRadial(printer);
					break;

				case LevelingSystem.Probe13PointRadial:
					levelingPlan = new LevelWizard13PointRadial(printer);
					break;

				case LevelingSystem.Probe100PointRadial:
					levelingPlan = new LevelWizard100PointRadial(printer);
					break;

				case LevelingSystem.Probe3x3Mesh:
					levelingPlan = new LevelWizardMesh(printer, 3, 3);
					break;

				case LevelingSystem.Probe5x5Mesh:
					levelingPlan = new LevelWizardMesh(printer, 5, 5);
					break;

				case LevelingSystem.Probe10x10Mesh:
					levelingPlan = new LevelWizardMesh(printer, 10, 10);
					break;

				case LevelingSystem.ProbeCustom:
					levelingPlan = new LevelWizardCustom(printer);
					break;

				default:
					throw new NotImplementedException();
			}

			sampledPositions = new List<PrintLevelingWizard.ProbePosition>(levelingPlan.ProbeCount);
			for (int j = 0; j < levelingPlan.ProbeCount; j++)
			{
				sampledPositions.Add(new PrintLevelingWizard.ProbePosition());
			}

			positionsToSample = levelingPlan.GetPrintLevelPositionToSample().ToList();
		}

		private void SampleNextPoint()
		{
			waitingToCompleteNextSample = true;

			samplesForSinglePosition.Clear();

			if (printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
			{
				// make sure the servo is deployed
				var servoDeployCommand = printer.Settings.GetValue<double>(SettingsKey.z_servo_depolyed_angle);
				queuedCommands.Enqueue($"M280 P0 S{servoDeployCommand}");
			}

			positionToSampleWithProbeOffset = positionToSample;

			// subtract out the probe offset
			var probeOffset = printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset);
			// we are only interested in the xy position
			probeOffset.Z = 0;
			positionToSampleWithProbeOffset -= probeOffset;

			var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();

			queuedCommands.Enqueue("G90");
			queuedCommands.Enqueue($"G1 Z{positionToSample.Z:0.###} F{feedRates.Z}");
			queuedCommands.Enqueue($"G1 X{positionToSampleWithProbeOffset.X:0.###}Y{positionToSampleWithProbeOffset.Y:0.###}Z{positionToSampleWithProbeOffset.Z:0.###} F{feedRates.X}");

			// probe the current position
			queuedCommands.Enqueue("G30");

			// raise the probe after each sample
			queuedCommands.Enqueue($"G1 X{positionToSampleWithProbeOffset.X:0.###}Y{positionToSampleWithProbeOffset.Y:0.###}Z{positionToSampleWithProbeOffset.Z:0.###} F{feedRates.X}");
		}
	}
}