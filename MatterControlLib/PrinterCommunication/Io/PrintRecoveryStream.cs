﻿/*
Copyright (c) 2016, Lars Brubaker
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

using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public enum RecoveryState { RemoveHeating, Raising, Homing, FindingRecoveryLayer, SkippingGCode, PrimingAndMovingToStart, PrintingSlow, PrintingToEnd }

	public class PrintRecoveryStream : GCodeStream
	{
		private GCodeSwitcher internalStream;
		private double percentDone;
		double recoverFeedRate;
		PrinterMove lastDestination;
		QueuedCommandsStream queuedCommands;
		RectangleDouble boundsOfSkippedLayers = RectangleDouble.ZeroIntersection;
		private string lastLine;

		public RecoveryState RecoveryState { get; private set; } = RecoveryState.RemoveHeating;

		public PrintRecoveryStream(GCodeSwitcher internalStream, PrinterConfig printer, double percentDone)
			: base(printer)
		{
			this.internalStream = internalStream;
			this.percentDone = percentDone;

			recoverFeedRate = printer.Settings.GetValue<double>(SettingsKey.recover_first_layer_speed);
			if (recoverFeedRate == 0)
			{
				recoverFeedRate = 10;
			}
			recoverFeedRate *= 60;

			queuedCommands = new QueuedCommandsStream(printer, null);
		}

		public override void Dispose()
		{
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			this.lastDestination.CopyKnowSettings(position);
			internalStream.SetPrinterPosition(lastDestination);
		}

		public override string ReadLine() 
		{
			// Send any commands that are queue before moving on to the internal stream.
			string nextCommand = queuedCommands.ReadLine();
			if (nextCommand != null)
			{
				lastLine = nextCommand;
				return nextCommand;
			}
			
			switch (RecoveryState)
			{
				// heat the extrude to remove it from the part
				case RecoveryState.RemoveHeating:
					// TODO: make sure we heat up all the extruders that we need to (all that are used)
					queuedCommands.Add("G21; set units to millimeters");
					queuedCommands.Add("M107; fan off");
					queuedCommands.Add("T0; set the active extruder to 0");
					queuedCommands.Add("G90; use absolute coordinates");
					queuedCommands.Add("G92 E0; reset the expected extruder position");
					queuedCommands.Add("M82; use absolute distance for extrusion");
					
					bool hasHeatedBed = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed);
					double bedTemp = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);
					if (hasHeatedBed && bedTemp > 0)
					{
						// start heating the bed
						queuedCommands.Add($"M140 S{bedTemp}");
					}

					// heat up the extruder
					queuedCommands.Add("M109 S{0}".FormatWith(printer.Settings.Helpers.ExtruderTargetTemperature(0)));

					if (hasHeatedBed && bedTemp > 0)
					{
						// finish heating the bed
						queuedCommands.Add($"M190 S{bedTemp}");
					}

					RecoveryState = RecoveryState.Raising;
					lastLine = "";
					return "";

				// remove it from the part
				case RecoveryState.Raising:
					// We don't know where the printer is for sure (it may have been turned off). Disable leveling until we know where it is.
					printer.Connection.AllowLeveling = false;
					queuedCommands.Add("M114 ; get current position");
					queuedCommands.Add("G91 ; move relative");
					queuedCommands.Add("G1 Z10 F{0}".FormatWith(printer.Settings.ZSpeed()));
					queuedCommands.Add("G90 ; move absolute");
					RecoveryState = RecoveryState.Homing;
					lastLine = "";
					return "";

				// if top homing, home the extruder
				case RecoveryState.Homing:
					if (printer.Settings.GetValue<bool>(SettingsKey.z_homes_to_max))
					{
						queuedCommands.Add("G28");
					}
					else
					{
						// home x
						queuedCommands.Add("G28 X0");
						// home y
						queuedCommands.Add("G28 Y0");
						// move to the place we can home z from
						Vector2 recoveryPositionXy = printer.Settings.GetValue<Vector2>(SettingsKey.recover_position_before_z_home);
						queuedCommands.Add("G1 X{0:0.###}Y{1:0.###}F{2}".FormatWith(recoveryPositionXy.X, recoveryPositionXy.Y, printer.Settings.XSpeed()));
						// home z
						queuedCommands.Add("G28 Z0");
					}
					// We now know where the printer is re-enable print leveling
					printer.Connection.AllowLeveling = true;
					RecoveryState = RecoveryState.FindingRecoveryLayer;
					return "";
					
				// This is to recover printing if an out a filament occurs. 
				// Help the user move the extruder down to just touching the part
				case RecoveryState.FindingRecoveryLayer:
					if (false) // help the user get the head to the right position
					{
						// move to above the completed print
						// move over a know good part of the model at the current top layer (extrude vertex from gcode)
						// let the user move down until they like the height
						// calculate that position and continue
					}
					else // we are resuming because of disconnect or reset, skip this
					{
						RecoveryState = RecoveryState.SkippingGCode;
						goto case RecoveryState.SkippingGCode;
					}

				case RecoveryState.SkippingGCode:
					// run through the gcode that the device expected looking for things like temp
					// and skip everything else until we get to the point we left off last time
					int commandCount = 0;
					boundsOfSkippedLayers = RectangleDouble.ZeroIntersection;
					while (internalStream.GCodeFile.PercentComplete(internalStream.LineIndex) < percentDone)
					{
						string line = internalStream.ReadLine();
						if(line == null)
						{
							break;
						}
						commandCount++;

						// make sure we don't parse comments
						if(line.Contains(";"))
						{
							line = line.Split(';')[0];
						}
						lastDestination = GetPosition(line, lastDestination);

						if (commandCount > 100)
						{
							boundsOfSkippedLayers.ExpandToInclude(lastDestination.position.Xy);
						}

						// check if the line is something we want to send to the printer (like a temp)
						if (line.StartsWith("M109") // heat and wait extruder
							|| line.StartsWith("M104") // heat extruder
							|| line.StartsWith("M190") // heat and wait bed
							|| line.StartsWith("M140") // heat bed
							|| line.StartsWith("T") // switch extruder
							|| line.StartsWith("M106") // fan on
							|| line.StartsWith("M107") // fan off
							|| line.StartsWith("G92")) // set position
						{
							lastLine = line;

							return line;
						}
					}
					
					RecoveryState = RecoveryState.PrimingAndMovingToStart;

					// make sure we always- pick up the last movement
					boundsOfSkippedLayers.ExpandToInclude(lastDestination.position.Xy);
					return "";

				case RecoveryState.PrimingAndMovingToStart:
					{

						if (printer.Settings.GetValue("z_homes_to_max") == "0") // we are homed to the bed
						{
							// move to the height we can recover printing from
							Vector2 recoverPositionXy = printer.Settings.GetValue<Vector2>(SettingsKey.recover_position_before_z_home);
							queuedCommands.Add(CreateMovementLine(new PrinterMove(new VectorMath.Vector3(recoverPositionXy.X, recoverPositionXy.Y, lastDestination.position.Z), 0, printer.Settings.ZSpeed())));
						}

						double extruderWidth = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter);
						// move to a position outside the printed bounds
						queuedCommands.Add(CreateMovementLine(new PrinterMove(
							new Vector3(boundsOfSkippedLayers.Left - extruderWidth*2, boundsOfSkippedLayers.Bottom + boundsOfSkippedLayers.Height / 2, lastDestination.position.Z),
							0, printer.Settings.XSpeed())));
						
						// let's prime the extruder
						queuedCommands.Add("G1 E10 F{0}".FormatWith(printer.Settings.EFeedRate(0))); // extrude 10
						queuedCommands.Add("G1 E9"); // and retract a bit

						// move to the actual print position
						queuedCommands.Add(CreateMovementLine(new PrinterMove(lastDestination.position, 0, printer.Settings.XSpeed())));

						/// reset the printer to know where the filament should be
						queuedCommands.Add("G92 E{0}".FormatWith(lastDestination.extrusion));
						RecoveryState = RecoveryState.PrintingSlow;
					}
					return "";

				case RecoveryState.PrintingSlow:
					{
						string lineToSend = internalStream.ReadLine();
						if (lineToSend == null)
						{
							return null;
						}

						if (!GCodeFile.IsLayerChange(lineToSend))
						{
							// have not seen the end of this layer so keep printing slow
							if (LineIsMovement(lineToSend))
							{
								PrinterMove currentMove = GetPosition(lineToSend, lastDestination);
								PrinterMove moveToSend = currentMove;

								moveToSend.feedRate = recoverFeedRate;

								lineToSend = CreateMovementLine(moveToSend, lastDestination);
								lastDestination = currentMove;
								return lineToSend;
							}

							lastLine = lineToSend;

							return lineToSend;
						}
					}

					// we only fall through to here after seeing the next "; Layer:"
					RecoveryState = RecoveryState.PrintingToEnd;
					return "";

				case RecoveryState.PrintingToEnd:
					return internalStream.ReadLine();
			}

			return null;
		}

		public override GCodeStream InternalStream => internalStream;

		public override string DebugInfo => lastLine + $" {this.lastDestination}";
	}
}