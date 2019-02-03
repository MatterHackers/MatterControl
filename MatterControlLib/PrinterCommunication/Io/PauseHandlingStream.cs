﻿/*
Copyright (c) 2015, Lars Brubaker
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
using System.Diagnostics;
using System.Linq;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class PauseHandlingStream : GCodeStreamProxy
	{
		internal class PositionSensorData
		{
			public double LastSensorDistance { get; internal set; }
			public double LastStepperDistance { get; internal set; }
			public int ExtrusionDiscrepency { get; internal set; }
		}

		protected PrinterMove lastDestination = new PrinterMove();
		private List<string> commandQueue = new List<string>();
		private object locker = new object();
		private PrinterMove moveLocationAtEndOfPauseCode;
		private Stopwatch timeSinceLastEndstopRead = new Stopwatch();
		bool readOutOfFilament = false;

		PositionSensorData positionSensorData = new PositionSensorData();

		public PauseHandlingStream(PrinterConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
			// if we have a runout sensor, register to listen for lines to check it
			if (printer.Settings.GetValue<bool>(SettingsKey.filament_runout_sensor))
			{
				printer.Connection.LineReceived += (s, line) =>
				{
					if (line != null)
					{
						if (line.Contains("ros_"))
						{
							if (line.Contains("TRIGGERED"))
							{
								readOutOfFilament = true;
							}
						}

						if (line.Contains("pos_"))
						{
							double sensorDistance = 0;
							double stepperDistance = 0;
							if (GCodeFile.GetFirstNumberAfter("SENSOR:", line, ref sensorDistance))
							{
								if (sensorDistance < -1 || sensorDistance > 1)
								{
									printer.Connection.FilamentPositionSensorDetected = true;
								}

								if (printer.Connection.FilamentPositionSensorDetected)
								{
									GCodeFile.GetFirstNumberAfter("STEPPER:", line, ref stepperDistance);

									var stepperDelta = Math.Abs(stepperDistance - positionSensorData.LastStepperDistance);

									// if we think we should have move the filament by more than 1mm
									if (stepperDelta > .2)
									{
										var sensorDelta = Math.Abs(sensorDistance - positionSensorData.LastSensorDistance);
										// check if the sensor data is within a tolerance of the stepper data

										var deltaRatio = sensorDelta / stepperDelta;
										if (deltaRatio < .9 || deltaRatio > 1.1)
										{
											// we have a repartable discrepency set a runout state
											positionSensorData.ExtrusionDiscrepency++;
											if (positionSensorData.ExtrusionDiscrepency > 2)
											{
												readOutOfFilament = true;
												positionSensorData.ExtrusionDiscrepency = 0;
											}
										}
										else
										{
											positionSensorData.ExtrusionDiscrepency = 0;
										}

										// and recard this position
										positionSensorData.LastSensorDistance = sensorDistance;
										positionSensorData.LastStepperDistance = stepperDistance;
									}
								}
							}
						}
					}
				};
			}
		}

		public enum PauseReason { UserRequested, PauseLayerReached, GCodeRequest, FilamentRunout }

		public PrinterMove LastDestination { get { return lastDestination; } }

		public void Add(string line)
		{
			// lock queue
			lock (locker)
			{
				commandQueue.Add(line);
			}
		}

		private long lastSendTimeMs;

		public void DoPause(PauseReason pauseReason, int layerNumber = -1)
		{
			switch (pauseReason)
			{
				case PauseReason.UserRequested:
					// do nothing special
					break;

				case PauseReason.PauseLayerReached:
				case PauseReason.GCodeRequest:
					printer.Connection.OnPauseOnLayer(new PrintPauseEventArgs(printer.Bed.EditContext?.SourceItem?.Name ?? "Unknown", false, layerNumber));
					break;

				case PauseReason.FilamentRunout:
					printer.Connection.OnFilamentRunout(new PrintPauseEventArgs(printer.Bed.EditContext?.SourceItem?.Name ?? "Unknown", true, layerNumber));
					break;
			}

			// Add the pause_gcode to the loadedGCode.GCodeCommandQueue
			string pauseGCode = printer.Settings.GetValue(SettingsKey.pause_gcode);

			// put in the gcode for pausing (if any)
			InjectPauseGCode(pauseGCode);

			// inject a marker to tell when we are done with the inserted pause code
			InjectPauseGCode("M114");

			InjectPauseGCode("MH_PAUSE");
		}

		public override string ReadLine()
		{
			string lineToSend = null;
			// lock queue
			lock (locker)
			{
				if (commandQueue.Count > 0)
				{
					lineToSend = commandQueue[0];
					commandQueue.RemoveAt(0);
				}
			}

			if (lineToSend == null)
			{
				if (!printer.Connection.PrinterIsPaused)
				{
					lineToSend = base.ReadLine();
					if (lineToSend == null)
					{
						return lineToSend;
					}
					if (lineToSend.EndsWith("; NO_PROCESSING"))
					{
						return lineToSend;
					}

					// We got a line from the gcode we are sending check if we should queue a request for filament runout
					if (printer.Settings.GetValue<bool>(SettingsKey.filament_runout_sensor))
					{
						// request to read the endstop state
						if (!timeSinceLastEndstopRead.IsRunning || timeSinceLastEndstopRead.ElapsedMilliseconds > 5000)
						{
							printer.Connection.QueueLine("M119");
							timeSinceLastEndstopRead.Restart();
						}
					}
					lastSendTimeMs = UiThread.CurrentTimerMs;
				}
				else
				{
					lineToSend = "";
					// If more than 10 seconds have passed send a movement command so the motors will stay locked
					if(UiThread.CurrentTimerMs - lastSendTimeMs > 10000)
					{
						printer.Connection.MoveRelative(PrinterConnection.Axis.X, .1, printer.Settings.Helpers.ManualMovementSpeeds().X);
						printer.Connection.MoveRelative(PrinterConnection.Axis.X, -.1, printer.Settings.Helpers.ManualMovementSpeeds().X);
						lastSendTimeMs = UiThread.CurrentTimerMs;
					}
				}
			}

			if (GCodeFile.IsLayerChange(lineToSend))
			{
				string layerNumber = lineToSend.Split(':')[1];
				if (PauseOnLayer(layerNumber))
				{
					// make the string 1 based (the internal code is 0 based)
					int layerIndex;
					int.TryParse(layerNumber, out layerIndex);
					DoPause(PauseReason.PauseLayerReached, layerIndex + 1);
				}
			}
			else if (lineToSend.StartsWith("M226")
				|| lineToSend.StartsWith("@pause"))
			{
				DoPause(PauseReason.GCodeRequest);
				lineToSend = "";
			}
			else if (lineToSend == "MH_PAUSE")
			{
				moveLocationAtEndOfPauseCode = LastDestination;

				if (printer.Connection.PrinterIsPrinting)
				{
					// remember where we were after we ran the pause gcode
					printer.Connection.CommunicationState = CommunicationStates.Paused;
				}

				lineToSend = "";
			}
			else if (readOutOfFilament)
			{
				readOutOfFilament = false;
				DoPause(PauseReason.FilamentRunout);
				lineToSend = "";
			}

			// keep track of the position
			if (lineToSend != null
				&& LineIsMovement(lineToSend))
			{
				lastDestination = GetPosition(lineToSend, lastDestination);
			}

			return lineToSend;
		}

		public void Resume()
		{
			// first go back to where we were after executing the pause code
			Vector3 positionBeforeActualPause = moveLocationAtEndOfPauseCode.position;
			InjectPauseGCode("G92 E{0:0.###}".FormatWith(moveLocationAtEndOfPauseCode.extrusion));
			Vector3 ensureAllAxisAreSent = positionBeforeActualPause + new Vector3(.01, .01, .01);
			var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();
			InjectPauseGCode("G1 X{0:0.###} Y{1:0.###} Z{2:0.###} F{3}".FormatWith(ensureAllAxisAreSent.X, ensureAllAxisAreSent.Y, ensureAllAxisAreSent.Z, feedRates.X + 1));
			InjectPauseGCode("G1 X{0:0.###} Y{1:0.###} Z{2:0.###} F{3}".FormatWith(positionBeforeActualPause.X, positionBeforeActualPause.Y, positionBeforeActualPause.Z, feedRates));

			string resumeGCode = printer.Settings.GetValue(SettingsKey.resume_gcode);
			InjectPauseGCode(resumeGCode);
			InjectPauseGCode("M114"); // make sure we know where we are after this resume code

			// make sure we are moving at a reasonable speed
			var outerPerimeterSpeed = printer.Settings.GetValue<double>(SettingsKey.perimeter_speed) * 60;
			InjectPauseGCode("G91"); // move relative
			InjectPauseGCode($"G1 X.1 F{outerPerimeterSpeed}"); // ensure good extrusion speed
			InjectPauseGCode($"G1 -X.1 F{outerPerimeterSpeed}"); // move back
			InjectPauseGCode("G90"); // move absolute
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			this.lastDestination.CopyKnowSettings(position);
			internalStream.SetPrinterPosition(lastDestination);
		}

		private void InjectPauseGCode(string codeToInject)
		{
			codeToInject = printer.ReplaceMacroValues(codeToInject);

			codeToInject = codeToInject.Replace("\\n", "\n");
			string[] lines = codeToInject.Split('\n');

			for (int i = 0; i < lines.Length; i++)
			{
				string[] splitOnSemicolon = lines[i].Split(';');
				string trimedLine = splitOnSemicolon[0].Trim().ToUpper();
				if (trimedLine != "")
				{
					this.Add(trimedLine);
				}
			}
		}

		private bool PauseOnLayer(string layer)
		{
			int layerNumber;
			var printerRecoveryStream = internalStream as PrintRecoveryStream;

			if (int.TryParse(layer, out layerNumber)
				&& printer.Settings.Helpers.LayerToPauseOn().Contains(layerNumber)
				&& (printerRecoveryStream == null
					|| printerRecoveryStream.RecoveryState == RecoveryState.PrintingToEnd))
			{
				return true;
			}
			return false;
		}
	}
}