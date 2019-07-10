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
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterControl.Printing.Pipelines
{
	public class ToolChangeStream : GCodeStreamProxy
	{
		private readonly string completedBeforeGCodeString = "; COMPLETED_BEFORE_GCODE";
		private int activeTool;
		private readonly int extruderCount = 0;
		private PrinterMove lastDestination = PrinterMove.Unknown;
		private string postSwitchLine;
		private double preSwitchFeedRate;
		private Vector3 preSwitchPosition;
		private readonly IGCodeLineReader gcodeLineReader;
		private readonly GCodeMemoryFile gCodeMemoryFile;
		private readonly QueuedCommandsStream queuedCommandsStream;

		public int RequestedTool { get; set; }

		private enum SendStates
		{
			Normal,
			WaitingForMove,
			SendingBefore
		}

		private SendStates sendState = SendStates.Normal;
		private readonly double[] targetTemps = new double[4];
		private readonly Queue<string> queuedCommands = new Queue<string>();

		public ToolChangeStream(PrintHostConfig printer, GCodeStream internalStream, QueuedCommandsStream queuedCommandsStream, IGCodeLineReader gcodeLineReader)
			: base(printer, internalStream)
		{
			this.gcodeLineReader = gcodeLineReader;
			if (gcodeLineReader != null)
			{
				this.gCodeMemoryFile = gcodeLineReader.GCodeFile as GCodeMemoryFile;
			}

			this.queuedCommandsStream = queuedCommandsStream;
			extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			activeTool = printer.Connection.ActiveExtruderIndex;
		}

		public override string DebugInfo => $"Last Destination = {lastDestination}";

		public override string ReadLine()
		{
			if (queuedCommands.Count > 0)
			{
				return queuedCommands.Dequeue();
			}

			string lineToSend = base.ReadLine();

			if (lineToSend == null)
			{
				return null;
			}

			if (lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			var requestedToolForTempChange = -1;
			// if we see a temp command remember what heat we are setting
			if (lineToSend.StartsWith("M109") || lineToSend.StartsWith("M104"))
			{
				int toolTemp = 0;
				// get the temp we are setting
				GCodeFile.GetFirstNumberAfter("S", lineToSend, ref toolTemp);
				// set it to the tool we will be changing to
				requestedToolForTempChange = RequestedTool;
				// check if this command contains a tool specification
				GCodeFile.GetFirstNumberAfter("T", lineToSend, ref requestedToolForTempChange);

				if (!lineToSend.Contains("; INACTIVE_COOL_DOWN"))
				{
					if (targetTemps[requestedToolForTempChange] != toolTemp)
					{
						targetTemps[requestedToolForTempChange] = toolTemp;
					}
				}
			}

			// check if any of the heaters we will be switching to need to start heating
			ManageReHeating(lineToSend);

			if (lineToSend == completedBeforeGCodeString
				&& sendState != SendStates.Normal)
			{
				activeTool = RequestedTool;
				sendState = SendStates.Normal;
				QueueAfterGCode();
			}

			var lineNoComment = lineToSend.Split(';')[0];

			if (lineNoComment == "G28"
				|| lineNoComment == "G28 Z0")
			{
				sendState = SendStates.Normal;
				RequestedTool = activeTool = 0;
			}

			// if this command is a temperature change request
			if (requestedToolForTempChange != -1)
			{
				if (requestedToolForTempChange != activeTool)
				{
					// For smoothie, switch back to the extrude we were using before the temp change (smoothie switches to the specified extruder, marlin repetier do not)
					queuedCommands.Enqueue($"T{activeTool}");
					var temp = GetNextToolTemp(requestedToolForTempChange);
					if (temp > 0)
					{
						return $"{lineToSend.Substring(0, 4)} T{requestedToolForTempChange} S{temp}";
					}
					else // send the temp as requested
					{
						return lineToSend;
					}
				}

				// if we are waiting to switch to the next tool
				else if (activeTool != RequestedTool)
				{
					// if this command does not include the extruder to switch to, than we need to switch before sending it
					if (!lineNoComment.Contains("T"))
					{
						queuedCommands.Enqueue($"T{RequestedTool}");
					}

					// For smoothie, switch back to the extrude we were using before the temp change (smoothie switches to the specified extruder, marlin repetier do not)
					queuedCommands.Enqueue($"T{activeTool}");
					// then send the heat command
					return lineToSend;
				}
			}

			// if this is a tool change request
			else if (lineToSend.StartsWith("T"))
			{
				int changeCommandTool = -1;
				if (GCodeFile.GetFirstNumberAfter("T", lineToSend, ref changeCommandTool))
				{
					if (changeCommandTool == activeTool)
					{
						if (sendState == SendStates.WaitingForMove)
						{
							// we have to switch back to our starting tool without a move
							// change back to normal processing and don't change tools
							sendState = SendStates.Normal;
							var lastRequestedTool = RequestedTool;
							// set the requested tool
							RequestedTool = changeCommandTool;
							// don't send the change are we are on the right tool now
							return $"; switch back without move from T{lastRequestedTool} to T{activeTool}";
						}
					}
					else // we are switching tools
					{
						if (sendState == SendStates.Normal)
						{
							sendState = SendStates.WaitingForMove;
							// set the requested tool
							RequestedTool = changeCommandTool;
							// don't queue the tool change until after the before gcode has been sent
							return $"; waiting for move on T{RequestedTool}";
						}
					}
				}
			}

			// if it is only an extrusion move
			if (sendState == SendStates.WaitingForMove
				&& activeTool != RequestedTool // is different than the last extruder set
				&& (lineNoComment.StartsWith("G0 ") || lineNoComment.StartsWith("G1 ")) // is a G1 or G0
				&& lineNoComment.Contains("E") // it is an extrusion move
											   // and have no other position information
				&& !lineNoComment.Contains("X")
				&& !lineNoComment.Contains("Y")
				&& !lineNoComment.Contains("Z"))
			{
				double ePosition = 0;

				if (GCodeFile.GetFirstNumberAfter("E", lineNoComment, ref ePosition))
				{
					// switch extruders
					queuedCommands.Enqueue($"T{RequestedTool}");

					// if we know the current E position before the switch
					// set the E value to the previous E value.
					if (lastDestination.extrusion != double.PositiveInfinity)
					{
						// On Marlin E position is share between extruders and this code has no utility
						// On Smoothie E is stored per extruder and this makes it behave the same as Marlin
						queuedCommands.Enqueue($"G92 E{lastDestination.extrusion}");
					}

					// send the extrusion
					queuedCommands.Enqueue(lineNoComment + " ; NO_PROCESSING");
					// switch back
					queuedCommands.Enqueue($"T{activeTool}");
					lastDestination.extrusion = ePosition;
					queuedCommands.Enqueue($"G92 E{lastDestination.extrusion}");
					return "";
				}
			}

			if (QueueBeforeIfNeedToSwitchExtruders(lineToSend, lineNoComment))
			{
				return "";
			}

			if (LineIsMovement(lineToSend))
			{
				lastDestination = GetPosition(lineToSend, lastDestination);
			}

			return lineToSend;
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			this.lastDestination.CopyKnowSettings(position);
			internalStream.SetPrinterPosition(lastDestination);
		}

		/// <summary>
		/// Seconds until the next tool change while printing.
		/// </summary>
		/// <returns>The time tool index we are switching to and the time until it will switch.</returns>
		private (int toolIndex, double time) NextToolChange(int toolToLookFor = -1)
		{
			if (gCodeMemoryFile != null)
			{
				var timeToTool = gCodeMemoryFile.NextToolChange(gcodeLineReader.LineIndex, -1, toolToLookFor);
				return timeToTool;
			}

			return (-1, 0);
		}

		private void ManageCoolDownAndOffTemps(StringBuilder gcode)
		{
			// get the time to the next tool switch
			var timeToNextToolChange = NextToolChange().time;

			// if we do not switch again
			if (timeToNextToolChange == double.PositiveInfinity)
			{
				// we do not switch tools again, turn off any that are not currently printing
				for (int i = 0; i < extruderCount; i++)
				{
					if (i != RequestedTool)
					{
						gcode.AppendLine($"M104 T{i} S0");
					}
				}
			}
			else // there are more tool changes in the future
			{
				var targetTemp = GetNextToolTemp(activeTool);

				// if we do not use this tool again
				if (targetTemp != printer.Connection.GetTargetHotendTemperature(activeTool))
				{
					gcode.AppendLine($"M104 T{activeTool} S{targetTemp} ; INACTIVE_COOL_DOWN");
				}
			}
		}

		private double GetNextToolTemp(int toolIndex)
		{
			var timeToReheat = printer.Settings.GetValue<double>(SettingsKey.seconds_to_reheat);

			// get the next time we will use the current tool
			var nextTimeThisTool = NextToolChange(toolIndex).time;

			// if we do not use this tool again
			if (nextTimeThisTool == double.PositiveInfinity)
			{
				// turn off its heat
				return 0;
			}

			// If there is enough time before we will use this tool again, lower the temp by the inactive_cool_down
			else if (nextTimeThisTool > timeToReheat)
			{
				var targetTemp = targetTemps[toolIndex];
				targetTemp = Math.Max(0, targetTemp - printer.Settings.GetValue<double>(SettingsKey.inactive_cool_down));
				if (targetTemp != targetTemps[toolIndex])
				{
					return targetTemp;
				}
			}

			return targetTemps[toolIndex];
		}

		private void ManageReHeating(string line)
		{
			var timeToReheat = printer.Settings.GetValue<double>(SettingsKey.seconds_to_reheat);

			// check if we need to turn on extruders while printing
			// check if any extruders need to start heating back up
			for (int i = 0; i < extruderCount; i++)
			{
				var (toolIndex, time) = NextToolChange(i);
				var targetTemp = targetTemps[i];
				var setTempLine = $"M104 T{i} S{targetTemp}";
				if (toolIndex >= 0
					&& time < timeToReheat
					&& printer.Connection.GetTargetHotendTemperature(i) != targetTemp
					&& line != setTempLine)
				{
					printer.Connection.SetTargetHotendTemperature(i, targetTemp);
					// queuedCommands.Enqueue(setTempLine);
				}
			}
		}

		private void QueueAfterGCode()
		{
			string afterGcodeToQueue = "";
			switch (RequestedTool)
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

			// if there is no extrusion we can move directly to the desired position after the extruder switch.
			// Otherwise we need to go to the last position to start the extrusion.
			if (!lineNoComment.Contains("E"))
			{
				newToolPosition.X = newToolPosition.X == double.PositiveInfinity ? preSwitchPosition.X : newToolPosition.X;
				newToolPosition.Y = newToolPosition.Y == double.PositiveInfinity ? preSwitchPosition.Y : newToolPosition.Y;
			}

			// no matter what happens with the x and y we want to set our z if we have one before
			newToolPosition.Z = newToolPosition.Z == double.PositiveInfinity ? preSwitchPosition.Z : newToolPosition.Z;

			// put together the output we want to send
			var gcode = new StringBuilder();

			// If the printer is heating, make sure we are at temp before switching extruders
			var nextToolTargetTemp = targetTemps[RequestedTool];
			var currentPrinterTargeTemp = printer.Connection.GetTargetHotendTemperature(RequestedTool);
			if (currentPrinterTargeTemp > 0
				&& printer.Connection.GetActualHotendTemperature(RequestedTool) < nextToolTargetTemp - 3)
			{
				// ensure our next tool is at temp (the one we are switching to)
				gcode.AppendLine($"M109 T{RequestedTool} S{nextToolTargetTemp}");
			}

			if (afterGcodeToQueue.Trim().Length > 0)
			{
				gcode.Append(printer.Settings.ReplaceMacroValues(afterGcodeToQueue));
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

		private bool QueueBeforeIfNeedToSwitchExtruders(string lineIn, string lineNoComment)
		{
			// check if there is a travel
			if (sendState == SendStates.WaitingForMove
				&& activeTool != RequestedTool // is different than the last extruder set
				&& (lineNoComment.StartsWith("G0 ") || lineNoComment.StartsWith("G1 ")) // is a G1 or G0
				&& (lineNoComment.Contains("X") || lineNoComment.Contains("Y") || lineNoComment.Contains("Z"))) // has a move axis in it
			{
				postSwitchLine = lineIn;

				string beforeGcodeToQueue = "";
				switch (RequestedTool)
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
					gcode.Append(printer.Settings.ReplaceMacroValues(beforeGcodeToQueue));
				}

				gcode.Append("\n");

				ManageCoolDownAndOffTemps(gcode);

				// send the actual tool change
				gcode.AppendLine($"T{RequestedTool}");

				// send the marker to let us know we have sent the before gcode
				gcode.AppendLine(completedBeforeGCodeString);

				queuedCommandsStream.Add(gcode.ToString());

				sendState = SendStates.SendingBefore;

				return true;
			}

			return false;
		}
	}
}