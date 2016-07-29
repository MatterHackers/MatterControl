/*
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

using System;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Agg;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	internal class ResumePrintingStream : GCodeStream
	{
		enum ResumeState {  RemoveHeating, Raising, Homing, FindingResumeLayer, SkippingGCode, PrimingAndMovingToStart, PrintingSlow, PrintingToEnd }
		private GCodeFileStream internalStream;
		private double percentDone;
		double resumeFeedRate;
		PrinterMove lastDestination;
        QueuedCommandsStream queuedCommands;

		ResumeState resumeState = ResumeState.RemoveHeating;

		public ResumePrintingStream(GCodeFileStream internalStream, double percentDone)
		{
			this.internalStream = internalStream;
			this.percentDone = percentDone;

			resumeFeedRate = ActiveSliceSettings.Instance.GetValue<double>("resume_first_layer_speed");
			if (resumeFeedRate == 0)
			{
				resumeFeedRate = 10;
			}
			resumeFeedRate *= 60;

			queuedCommands = new QueuedCommandsStream(null);
		}

		public override void Dispose()
		{
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			lastDestination = position;
			internalStream.SetPrinterPosition(lastDestination);
		}

		public override string ReadLine() 
		{
			// Send any commands that are queue before moving on to the internal stream.
			string nextCommand = queuedCommands.ReadLine();
			if (nextCommand != null)
			{
				return nextCommand;
			}
			
			switch (resumeState)
			{
				// heat the extrude to remove it from the part
				case ResumeState.RemoveHeating:
					// TODO: make sure we heat up all the extruders that we need to (all that are used)
					queuedCommands.Add("G21; set units to millimeters");
					queuedCommands.Add("M107; fan off");
					queuedCommands.Add("T0; set the active extruder to 0");
					queuedCommands.Add("G90; use absolute coordinates");
					queuedCommands.Add("G92 E0; reset the expected extruder position");
					queuedCommands.Add("M82; use absolute distance for extrusion");
					queuedCommands.Add("M109 S{0}".FormatWith(ActiveSliceSettings.Instance.Helpers.ExtruderTemperature(0)));

					resumeState = ResumeState.Raising;
					return "";

				// remove it from the part
				case ResumeState.Raising:
					queuedCommands.Add("M114 ; get current position");
					queuedCommands.Add("G91 ; move relative");
					queuedCommands.Add("G1 Z10 F{0}".FormatWith(MovementControls.ZSpeed));
					queuedCommands.Add("G90 ; move absolute");
					resumeState = ResumeState.Homing;
					return "";

				// if top homing, home the extruder
				case ResumeState.Homing:
					if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.z_homes_to_max))
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
						Vector2 resumePositionXy = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.resume_position_before_z_home);
						queuedCommands.Add("G1 X{0:0.000}Y{1:0.000}F{2}".FormatWith(resumePositionXy.x, resumePositionXy.y, MovementControls.XSpeed));
						// home z
						queuedCommands.Add("G28 Z0");
					}
					resumeState = ResumeState.FindingResumeLayer;
					return "";
					
				// This is to resume printing if an out a filament occurs. 
				// Help the user move the extruder down to just touching the part
				case ResumeState.FindingResumeLayer:
					if (false) // help the user get the head to the right position
					{
						// move to above the completed print
						// move over a know good part of the model at the current top layer (extrude vertex from gcode)
						// let the user move down until they like the height
						// calculate that position and continue
					}
					else // we are resuming because of disconnect or reset, skip this
					{
						resumeState = ResumeState.SkippingGCode;
						goto case ResumeState.SkippingGCode;
					}

				case ResumeState.SkippingGCode:
					// run through the gcode that the device expected looking for things like temp
					// and skip everything else until we get to the point we left off last time
					while (internalStream.FileStreaming.PercentComplete(internalStream.LineIndex) < percentDone)
					{
						string line = internalStream.ReadLine();

						lastDestination = GetPosition(line, lastDestination);

						// check if the line is something we want to send to the printer (like a temp)
						if (line.StartsWith("M109")
							|| line.StartsWith("M104")
							|| line.StartsWith("T")
							|| line.StartsWith("M106")
							|| line.StartsWith("M107"))
						{
							return line;
						}
					}
					
					resumeState = ResumeState.PrimingAndMovingToStart;
					return "";

				case ResumeState.PrimingAndMovingToStart:
					{
						// let's prime the extruder, move to a good position over the part, then start printing
						queuedCommands.Add("G1 E5");
						queuedCommands.Add("G1 E4");
						if (ActiveSliceSettings.Instance.GetValue("z_homes_to_max") == "0") // we are homed to the bed
						{
							// move to the height we can resume printing from
							Vector2 resumePositionXy = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.resume_position_before_z_home);
							queuedCommands.Add(CreateMovementLine(new PrinterMove(new VectorMath.Vector3(resumePositionXy.x, resumePositionXy.y, lastDestination.position.z + 5), 0, MovementControls.ZSpeed)));
							// move just above the actual print position
							queuedCommands.Add(CreateMovementLine(new PrinterMove(lastDestination.position + new VectorMath.Vector3(0, 0, 5), 0, MovementControls.XSpeed)));
							// move down to part
							queuedCommands.Add(CreateMovementLine(new PrinterMove(lastDestination.position, 0, MovementControls.ZSpeed)));
						}
						else
						{
							// move to the actual print position
							queuedCommands.Add(CreateMovementLine(new PrinterMove(lastDestination.position, 0, MovementControls.ZSpeed)));
						}
						// extrude back to our filament start
						queuedCommands.Add("G1 E5");
						/// reset the printer to know where the filament should be
						queuedCommands.Add("G92 E{0}".FormatWith(lastDestination.extrusion));
						resumeState = ResumeState.PrintingSlow;
					}
					return "";

				case ResumeState.PrintingSlow:
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

								moveToSend.feedRate = resumeFeedRate;

								lineToSend = CreateMovementLine(moveToSend, lastDestination);
								lastDestination = currentMove;
								return lineToSend;
							}

							return lineToSend;
						}
					}

					// we only fall through to here after seeing the next "; Layer:"
					resumeState = ResumeState.PrintingToEnd;
					return "";

				case ResumeState.PrintingToEnd:
					return internalStream.ReadLine();
			}

			return null;
		}
	}
}