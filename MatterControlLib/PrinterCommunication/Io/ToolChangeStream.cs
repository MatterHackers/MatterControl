/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Text;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class ToolChangeStream : GCodeStreamProxy
	{
		private bool watingForBeforeGCode = false;
		private int requestedExtruder;
		private int extruderIndex;
		PrinterMove lastDestination = PrinterMove.Unknown;
		private QueuedCommandsStream queuedCommandsStream;
		int extruderCount = 0;
		Vector3[] extruderOffsets = new Vector3[4];
		private double preSwitchFeedRate;
		private Vector3 preSwitchPosition;
		private string postSwitchLine;
		private readonly string compleatedBeforeGCodeString = "; COMPLEATED_BEFORE_GCODE";

		public ToolChangeStream(PrinterConfig printer, GCodeStream internalStream, QueuedCommandsStream queuedCommandsStream)
			: base(printer, internalStream)
		{
			this.queuedCommandsStream = queuedCommandsStream;
			extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			extruderIndex = printer.Connection.ActiveExtruderIndex;
			printer.Settings.SettingChanged += Settings_SettingChanged;
			ReadExtruderOffsets();
		}

		private void Settings_SettingChanged(object sender, StringEventArgs stringEvent)
		{
			if (stringEvent != null)
			{
				// if the offsets change update them (unless we are actively printing)
				if (stringEvent.Data == SettingsKey.extruder_offset
					&& !printer.Connection.Printing
					&& !printer.Connection.Paused)
				{
					ReadExtruderOffsets();
				}
			}
		}

		private void ReadExtruderOffsets()
		{
			for (int i = 0; i < 4; i++)
			{
				extruderOffsets[i] = printer.Settings.Helpers.ExtruderOffset(i);
			}
		}

		public override void Dispose()
		{
			printer.Settings.SettingChanged -= Settings_SettingChanged;

			base.Dispose();
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			this.lastDestination.CopyKnowSettings(position);
			if (extruderIndex < 4)
			{
				lastDestination.position += extruderOffsets[extruderIndex];
			}
			internalStream.SetPrinterPosition(lastDestination);
		}

		public override string DebugInfo
		{
			get
			{
				return $"Last Destination = {lastDestination}";
			}
		}

		private bool QueueBeforeIfNeedToSwitchExtruders(string lineIn)
		{
			if (lineIn == null)
			{
				return false;
			}

			postSwitchLine = lineIn;
			var lineNoComment = lineIn.Split(';')[0];

			TrackExtruderState(lineNoComment);

			bool queuedSwitch = false;
			// check if there is a travel
			if ((lineNoComment.StartsWith("G0 ") || lineNoComment.StartsWith("G1 ")) // is a G1 or G0
				&& (lineNoComment.Contains("X") || lineNoComment.Contains("Y") || lineNoComment.Contains("Z")) // hase a move axis in it
				&& extruderIndex != requestedExtruder // is different than the last extruder set
				&& !watingForBeforeGCode)
			{
				string beforeGcodeToQueue = "";
				switch (requestedExtruder)
				{
					case 0:
						beforeGcodeToQueue = printer.Settings.GetValue(SettingsKey.before_toolchange_gcode).Replace("\\n", "\n");
						break;
					case 1:
						beforeGcodeToQueue = printer.Settings.GetValue(SettingsKey.before_toolchange_gcode_1).Replace("\\n", "\n");
						break;
				}

				preSwitchFeedRate = lastDestination.feedRate;
				preSwitchPosition = lastDestination.position;

				// put together the output we want to send
				var gcode = new StringBuilder();
				if (beforeGcodeToQueue.Trim().Length > 0)
				{
					watingForBeforeGCode = true;
					gcode.Append(printer.ReplaceMacroValues(beforeGcodeToQueue));
				}

				gcode.AppendLine("\n" + compleatedBeforeGCodeString);
				queuedCommandsStream.Add(gcode.ToString());

				queuedSwitch = true;
			}

			return queuedSwitch;
		}


		private void QueueAfterGCode()
		{
			string afterGcodeToQueue = "";
			switch (requestedExtruder)
			{
				case 0:
					afterGcodeToQueue = printer.Settings.GetValue(SettingsKey.toolchange_gcode).Replace("\\n", "\n");
					break;
				case 1:
					afterGcodeToQueue = printer.Settings.GetValue(SettingsKey.toolchange_gcode_1).Replace("\\n", "\n");
					break;
			}

			// put together the output we want to send
			var gcode = new StringBuilder();

			if (afterGcodeToQueue.Trim().Length > 0)
			{
				gcode.Append(printer.ReplaceMacroValues(afterGcodeToQueue));
			}

			// move to selected tool to the last tool position at the travel speed
			if (preSwitchPosition.X != double.PositiveInfinity
				&& preSwitchPosition.Y != double.PositiveInfinity)
			{
				gcode.AppendLine($"\n G1 X{preSwitchPosition.X}Y{preSwitchPosition.Y}F{printer.Settings.XSpeed()}");
			}

			// move to the z position
			if (preSwitchPosition.Z != double.PositiveInfinity)
			{
				gcode.AppendLine($"G1 Z{preSwitchPosition.Z}F{printer.Settings.ZSpeed()}");
			}

			// set the feedrate back to what was before we added any code
			if (preSwitchFeedRate != double.PositiveInfinity)
			{
				gcode.AppendLine($"G1 F{preSwitchFeedRate}");
			}

			// and queue the travel
			gcode.AppendLine(postSwitchLine);

			queuedCommandsStream.Add(gcode.ToString());
		}

		public override string ReadLine()
		{
			string lineToSend = base.ReadLine();

			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			if(lineToSend == compleatedBeforeGCodeString)
			{
				extruderIndex = requestedExtruder;
				watingForBeforeGCode = false;
				QueueAfterGCode();
			}

			if (QueueBeforeIfNeedToSwitchExtruders(lineToSend))
			{
				return "";
			}

			TrackExtruderState(lineToSend);

			if (lineToSend != null
				&& LineIsMovement(lineToSend))
			{
				PrinterMove currentMove = GetPosition(lineToSend, lastDestination);

				PrinterMove moveToSend = currentMove;
				if (extruderIndex < 4)
				{
					moveToSend.position -= extruderOffsets[extruderIndex];
				}

				if (moveToSend.HaveAnyPosition)
				{
					lineToSend = CreateMovementLine(moveToSend, lastDestination);
				}
				lastDestination = currentMove;

				return lineToSend;
			}


			return lineToSend;
		}

		private void TrackExtruderState(string line)
		{
			if (line == null)
			{
				return;
			}

			if (line.StartsWith("G28)"))
			{
				extruderIndex = 0;
				requestedExtruder = 0;
			}

			if (line.StartsWith("T"))
			{
				GCodeFile.GetFirstNumberAfter("T", line, ref requestedExtruder);
			}
		}
	}
}