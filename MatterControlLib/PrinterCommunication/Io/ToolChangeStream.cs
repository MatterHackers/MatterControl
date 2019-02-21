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
using MatterControl.Printing;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class ToolChangeStream : GCodeStreamProxy
	{
		private Queue<string> commandQueue = new Queue<string>();
		private object locker = new object();
		private int requestedExtruder;
		private int activeExtruderIndex;
		PrinterMove lastDestination = PrinterMove.Unknown;
		int extruderCount = 0;

		public ToolChangeStream(PrinterConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
			extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			activeExtruderIndex = printer.Connection.ActiveExtruderIndex;
		}

		private bool CheckIfNeedToSwitchExtruders(string lineIn)
		{
			bool queuedSwitch = false;
			if (lineIn == null)
			{
				return queuedSwitch;
			}

			var lineNoComment = lineIn.Split(';')[0];

			TrackExtruderState(lineNoComment);

			if ((lineNoComment.StartsWith("G0 ") || lineNoComment.StartsWith("G1 ")) // is a G1 or G0
				&& (lineNoComment.Contains("X") || lineNoComment.Contains("Y") || lineNoComment.Contains("Z")) // hase a move axis in it
				&& activeExtruderIndex != requestedExtruder) // is different than the last extruder set
			{
				string gcodeToQueue = "";
				switch (requestedExtruder)
				{
					case 0:
						gcodeToQueue = printer.Settings.GetValue(SettingsKey.before_toolchange_gcode).Replace("\\n", "\n");
						break;
					case 1:
						gcodeToQueue = printer.Settings.GetValue(SettingsKey.before_toolchange_gcode_1).Replace("\\n", "\n");
						break;
				}

				if (gcodeToQueue.Trim().Length > 0)
				{
					var feedRate = lastDestination.feedRate;
					if (gcodeToQueue.Contains("\n"))
					{
						string[] linesToWrite = gcodeToQueue.Split(new string[] { "\n" }, StringSplitOptions.None);
						for (int i = 0; i < linesToWrite.Length; i++)
						{
							string gcodeLine = linesToWrite[i].Trim();
							if (gcodeLine.Length > 0)
							{
								commandQueue.Enqueue(gcodeLine);
							}
						}
					}
					else
					{
						commandQueue.Enqueue(gcodeToQueue);
					}

					commandQueue.Enqueue($"G1 F{feedRate}");
					commandQueue.Enqueue(lineIn);
					queuedSwitch = true;
				}

				activeExtruderIndex = requestedExtruder;
			}

			return queuedSwitch;
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			this.lastDestination.CopyKnowSettings(position);
			internalStream.SetPrinterPosition(lastDestination);
		}

		public override string ReadLine()
		{
			string lineToSend = null;

			// lock queue
			lock (locker)
			{
				if (commandQueue.Count > 0)
				{
					lineToSend = commandQueue.Dequeue();
					lineToSend = printer.ReplaceMacroValues(lineToSend);
				}
			}

			if (lineToSend == null)
			{
				lineToSend = base.ReadLine();
			}

			if(CheckIfNeedToSwitchExtruders(lineToSend))
			{
				return "";
			}

			TrackExtruderState(lineToSend);

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
				activeExtruderIndex = 0;
				requestedExtruder = 0;
			}

			if (line.StartsWith("T"))
			{
				GCodeFile.GetFirstNumberAfter("T", line, ref requestedExtruder);
			}
		}
	}
}