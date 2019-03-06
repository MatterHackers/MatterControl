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
using MatterControl.Printing;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class HotendTemperatureStream : GCodeStreamProxy
	{
		private int currentToolIndex;
		private QueuedCommandsStream queuedCommandsStream;
		int toolCount = 0;

		public HotendTemperatureStream(PrinterConfig printer, GCodeStream internalStream, QueuedCommandsStream queuedCommandsStream)
			: base(printer, internalStream)
		{
			this.queuedCommandsStream = queuedCommandsStream;
			toolCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			currentToolIndex = printer.Connection.ActiveExtruderIndex;
		}

		public override string DebugInfo
		{
			get
			{
				return $"";
			}
		}

		public override string ReadLine()
		{
			string lineToSend = base.ReadLine();

			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			TrackToolState(lineToSend);

			return lineToSend;
		}

		private void TrackToolState(string line)
		{
			if (line == null)
			{
				return;
			}

			var timeToReheat = printer.Settings.GetValue<double>(SettingsKey.seconds_to_reheat);

			// check if we need to turn on extruders while printing
			if (printer.Connection.Printing)
			{
				// check if any extruders need to start heating back up
				for (int i = 0; i < toolCount; i++)
				{
					var timeUntilUsed = printer.Connection.NextToolChange(i).time;
					var targetTemp = printer.Settings.Helpers.ExtruderTargetTemperature(i);
					if (timeUntilUsed < timeToReheat
						&& printer.Connection.GetTargetHotendTemperature(i) != targetTemp)
					{
						printer.Connection.SetTargetHotendTemperature(i, targetTemp);
					}
				}
			}

			if (line.StartsWith("T"))
			{
				var nextToolIndex = currentToolIndex;
				GCodeFile.GetFirstNumberAfter("T", line, ref nextToolIndex);
				if(printer.Connection.Printing
					&& nextToolIndex != currentToolIndex)
				{
					// get the time to the next tool switch
					var timeToNextToolChange = printer.Connection.NextToolChange().time;

					// if we do not switch again
					if (timeToNextToolChange == double.PositiveInfinity)
					{
						// we do not switch tools again, turn off any that are not currently printing
						for (int i = 0; i < toolCount; i++)
						{
							if (i != nextToolIndex)
							{
								printer.Connection.SetTargetHotendTemperature(i, 0, true);
							}
						}
					}
					else // there are more tool changes in the future
					{
						// get the next time we will use the current tool
						var nextTimeThisTool = printer.Connection.NextToolChange(currentToolIndex).time;

						// if we do not use this tool again
						if (nextTimeThisTool == double.PositiveInfinity)
						{
							// turn off its heat
							printer.Connection.SetTargetHotendTemperature(currentToolIndex, 0, true);
						}
						// If there is enough time before we will use this tool again, lower the temp by the inactive_cool_down
						else if (nextTimeThisTool > timeToReheat)
						{
							var targetTemp = printer.Settings.Helpers.ExtruderTargetTemperature(currentToolIndex);
							targetTemp = Math.Max(0, targetTemp - printer.Settings.GetValue<double>(SettingsKey.inactive_cool_down));
							printer.Connection.SetTargetHotendTemperature(currentToolIndex, targetTemp);
						}
					}

					currentToolIndex = nextToolIndex;
				}
			}
		}
	}
}