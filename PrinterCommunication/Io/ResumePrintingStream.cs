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

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	internal class ResumePrintingStream : GCodeStream
	{
		enum ResumeState {  RemoveHeating, Raising, Homing, FindingResumeLayer, SkippingGCode, PrimingAndMovingToStart, PrintingSlow, PrintingToEnd }
		private GCodeFileStream gCodeFileStream0;
		private double percentDone;
		PrinterMove lastDestination;
        QueuedCommandsStream raiseCommands;

		ResumeState resumeState = ResumeState.RemoveHeating;

		public ResumePrintingStream(GCodeFileStream gCodeFileStream0, double percentDone)
		{
			this.gCodeFileStream0 = gCodeFileStream0;
			this.percentDone = percentDone;

			raiseCommands = new QueuedCommandsStream(null);
			raiseCommands.Add("G91 ; move relative");
			raiseCommands.Add("G1 Z10 F{0}".FormatWith(MovementControls.ZSpeed));
			raiseCommands.Add("G90 ; move absolute");
		}

		public override void Dispose()
		{
		}

		public override string ReadLine()
		{
			switch(resumeState)
			{
				// heat the extrude to remove it from the part
				case ResumeState.RemoveHeating:
					// TODO: make sure we heat up all the extruders that we need to (all that are used)
					resumeState = ResumeState.Raising;
					return "M109 S{0}".FormatWith(ActiveSliceSettings.Instance.GetMaterialValue("temperature", 1));

				// remove it from the part
				case ResumeState.Raising:
					string nextCommand = raiseCommands.ReadLine();
					if(nextCommand != null)
					{
						return nextCommand;
					}
					resumeState = ResumeState.Homing;
					goto case ResumeState.Homing;

				// if top homing, home the extruder
				case ResumeState.Homing:
					resumeState = ResumeState.FindingResumeLayer;
					return "G28";
					
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
					while (gCodeFileStream0.FileStreaming.PercentComplete(gCodeFileStream0.LineIndex) < percentDone)
					{
						string line = gCodeFileStream0.ReadLine();

						lastDestination = GetPosition(line, lastDestination);

						// check if the line is something we want to send to the printer (like a temp)
						if (line.StartsWith("M109")
							|| line.StartsWith("M104"))
						{
							return line;
						}
					}
					
					resumeState = ResumeState.PrimingAndMovingToStart;
					goto case ResumeState.PrimingAndMovingToStart;

				case ResumeState.PrimingAndMovingToStart:
					resumeState = ResumeState.PrintingSlow;
					goto case ResumeState.PrintingSlow;

				case ResumeState.PrintingSlow:
					string lineToSend = gCodeFileStream0.ReadLine();
					if(!lineToSend.StartsWith("; LAYER:"))
					{
						if (lineToSend != null
							&& LineIsMovement(lineToSend))
						{
							PrinterMove currentMove = GetPosition(lineToSend, lastDestination);
							PrinterMove moveToSend = currentMove;
							double feedRate = ActiveSliceSettings.Instance.GetActiveValueAsDouble("first_layer_speed", 10);
							moveToSend.feedRate = feedRate;

							lineToSend = CreateMovementLine(moveToSend, lastDestination);
							lastDestination = currentMove;
							return lineToSend;
						}

						return lineToSend;
					}
					resumeState = ResumeState.PrintingToEnd;
					goto case ResumeState.PrintingToEnd;

				case ResumeState.PrintingToEnd:
					return gCodeFileStream0.ReadLine();
			}

			return null;
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			gCodeFileStream0.SetPrinterPosition(position);
		}
	}
}