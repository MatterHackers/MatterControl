﻿/*
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
using System.Text;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class ToolChangeStream : GCodeStreamProxy
	{
		private readonly string compleatedBeforeGCodeString = "; COMPLEATED_BEFORE_GCODE";
		private int activeTool;
		private int extruderCount = 0;
		private Vector3[] extruderOffsets = new Vector3[4];
		private PrinterMove lastDestination = PrinterMove.Unknown;
		private string postSwitchLine;
		private double preSwitchFeedRate;
		private Vector3 preSwitchPosition;
		private QueuedCommandsStream queuedCommandsStream;
		private int requestedTool;
		enum SendStates { Normal, WaitingForMove, SendingBefore }
		private SendStates SendState = SendStates.Normal;

		public ToolChangeStream(PrinterConfig printer, GCodeStream internalStream, QueuedCommandsStream queuedCommandsStream)
			: base(printer, internalStream)
		{
			this.queuedCommandsStream = queuedCommandsStream;
			extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			activeTool = printer.Connection.ActiveExtruderIndex;
			printer.Settings.SettingChanged += Settings_SettingChanged;
			ReadExtruderOffsets();
		}

		public override string DebugInfo
		{
			get
			{
				return $"Last Destination = {lastDestination}";
			}
		}

		public override void Dispose()
		{
			printer.Settings.SettingChanged -= Settings_SettingChanged;

			base.Dispose();
		}

		public override string ReadLine()
		{
			string lineToSend = base.ReadLine();

			if(lineToSend == null)
			{
				return null;
			}

			if (lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			// check if any of the heaters we will be switching to need to start heating
			ManageReHeating(lineToSend);

			if (lineToSend == compleatedBeforeGCodeString)
			{
				activeTool = requestedTool;
				SendState = SendStates.Normal;
				QueueAfterGCode();
			}

			// track the tool state
			if (lineToSend.StartsWith("T"))
			{
				GCodeFile.GetFirstNumberAfter("T", lineToSend, ref requestedTool);
				if (SendState == SendStates.Normal)
				{
					SendState = SendStates.WaitingForMove;
					// don't queue the tool change until after the before gcode has been sent
					return $"; waiting for move on T{requestedTool}";
				}
			}

			if (QueueBeforeIfNeedToSwitchExtruders(lineToSend))
			{
				return "";
			}

			if (LineIsMovement(lineToSend))
			{
				PrinterMove currentMove = GetPosition(lineToSend, lastDestination);

				PrinterMove moveToSend = currentMove;
				if (activeTool < 4)
				{
					moveToSend.position -= extruderOffsets[activeTool];
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

		public override void SetPrinterPosition(PrinterMove position)
		{
			this.lastDestination.CopyKnowSettings(position);
			if (activeTool < 4)
			{
				lastDestination.position += extruderOffsets[activeTool];
			}
			internalStream.SetPrinterPosition(lastDestination);
		}

		private void ManageCoolDownAndOffTemps()
		{
			// get the time to the next tool switch
			var timeToNextToolChange = printer.Connection.NextToolChange().time;
			var timeToReheat = printer.Settings.GetValue<double>(SettingsKey.seconds_to_reheat);

			// if we do not switch again
			if (timeToNextToolChange == double.PositiveInfinity)
			{
				// we do not switch tools again, turn off any that are not currently printing
				for (int i = 0; i < extruderCount; i++)
				{
					if (i != requestedTool)
					{
						printer.Connection.QueueLine($"M104 T{i} S0");
					}
				}
			}
			else // there are more tool changes in the future
			{
				// get the next time we will use the current tool
				var nextTimeThisTool = printer.Connection.NextToolChange(activeTool).time;

				// if we do not use this tool again
				if (nextTimeThisTool == double.PositiveInfinity)
				{
					// turn off its heat
					printer.Connection.QueueLine($"M104 T{activeTool} S0");
				}
				// If there is enough time before we will use this tool again, lower the temp by the inactive_cool_down
				else if (nextTimeThisTool > timeToReheat)
				{
					var targetTemp = printer.Settings.Helpers.ExtruderTargetTemperature(activeTool);
					targetTemp = Math.Max(0, targetTemp - printer.Settings.GetValue<double>(SettingsKey.inactive_cool_down));
					printer.Connection.QueueLine($"M104 T{activeTool} S{targetTemp}");
				}
			}
		}

		private void ManageReHeating(string line)
		{
			var timeToReheat = printer.Settings.GetValue<double>(SettingsKey.seconds_to_reheat);

			bool setATemp = false;
			// check if we need to turn on extruders while printing
			// check if any extruders need to start heating back up
			for (int i = 0; i < extruderCount; i++)
			{
				var nextToolChange = printer.Connection.NextToolChange(i);
				var targetTemp = printer.Settings.Helpers.ExtruderTargetTemperature(i);
				if (nextToolChange.toolIndex >= 0
					&& nextToolChange.time < timeToReheat
					&& printer.Connection.GetTargetHotendTemperature(i) != targetTemp)
				{
					printer.Connection.QueueLine($"M104 T{i} S0");
					setATemp = true;
				}
			}

			// for smoothie, make sure we have the right extruder active
			if (setATemp)
			{
				printer.Connection.QueueLine($"T{activeTool}");
			}
		}

		private void QueueAfterGCode()
		{
			ManageCoolDownAndOffTemps();

			string afterGcodeToQueue = "";
			switch (requestedTool)
			{
				case 0:
					afterGcodeToQueue = printer.Settings.GetValue(SettingsKey.toolchange_gcode).Replace("\\n", "\n");
					break;

				case 1:
					afterGcodeToQueue = printer.Settings.GetValue(SettingsKey.toolchange_gcode_1).Replace("\\n", "\n");
					break;
			}

			PrinterMove newToolMove = GetPosition(postSwitchLine, PrinterMove.Unknown);
			var newToolPosition = newToolMove.position;
			var lineNoComment = postSwitchLine.Split(';')[0];

			// if there is no extrusion we can move directly the desired position after the extruder switch.
			// Otherwise we need to go to the last position to start the extrusion.
			if (!lineNoComment.Contains("E"))
			{
				newToolPosition.X = newToolPosition.X == double.PositiveInfinity ? preSwitchPosition.X : newToolPosition.X;
				newToolPosition.Y = newToolPosition.Y == double.PositiveInfinity ? preSwitchPosition.Y : newToolPosition.Y;
				newToolPosition.Z = newToolPosition.Y == double.PositiveInfinity ? preSwitchPosition.Z : newToolPosition.Z;
			}

			// put together the output we want to send
			var gcode = new StringBuilder();

			// ensure our next tool is at temp (the one we are switching to)
			var nextToolTargetTemp = printer.Connection.GetTargetHotendTemperature(requestedTool);
			if (printer.Connection.GetActualHotendTemperature(requestedTool) < nextToolTargetTemp - 3)
			{
				gcode.AppendLine($"M109 T{requestedTool} S{nextToolTargetTemp}");
				gcode.AppendLine($"T{requestedTool}");
			}

			if (afterGcodeToQueue.Trim().Length > 0)
			{
				gcode.Append(printer.ReplaceMacroValues(afterGcodeToQueue));
			}

			// move to selected tool to the last tool position at the travel speed
			if (newToolPosition.X != double.PositiveInfinity
				&& newToolPosition.Y != double.PositiveInfinity)
			{
				gcode.AppendLine($"\n G1 X{newToolPosition.X}Y{newToolPosition.Y}F{printer.Settings.XSpeed()}");
			}

			// move to the z position
			if (newToolPosition.Z != double.PositiveInfinity)
			{
				gcode.AppendLine($"G1 Z{newToolPosition.Z}F{printer.Settings.ZSpeed()}");
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

		private bool QueueBeforeIfNeedToSwitchExtruders(string lineIn)
		{
			var lineNoComment = lineIn.Split(';')[0];

			// check if there is a travel
			if ((lineNoComment.StartsWith("G0 ") || lineNoComment.StartsWith("G1 ")) // is a G1 or G0
				&& (lineNoComment.Contains("X") || lineNoComment.Contains("Y") || lineNoComment.Contains("Z")) // hase a move axis in it
				&& activeTool != requestedTool // is different than the last extruder set
				&& SendState == SendStates.WaitingForMove)
			{
				postSwitchLine = lineIn;

				string beforeGcodeToQueue = "";
				switch (requestedTool)
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
					gcode.Append(printer.ReplaceMacroValues(beforeGcodeToQueue));
				}

				// send the actual tool change
				gcode.AppendLine("\n" + $"T{requestedTool}");
				// send the marker to let us know we have sent the before gcode
				gcode.AppendLine(compleatedBeforeGCodeString);

				queuedCommandsStream.Add(gcode.ToString());

				SendState = SendStates.SendingBefore;

				return true;
			}

			return false;
		}

		private void ReadExtruderOffsets()
		{
			for (int i = 0; i < 4; i++)
			{
				extruderOffsets[i] = printer.Settings.Helpers.ExtruderOffset(i);
			}
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
	}
}