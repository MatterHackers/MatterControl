/*
Copyright (c) 2014, Lars Brubaker
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
using System;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class OffsetStream : GCodeStreamProxy
	{
		private int extruderIndex = 0;
		PrinterMove lastDestination = PrinterMove.Unknown;

		Vector3[] extruderOffsets = new Vector3[4];

		public OffsetStream(GCodeStream internalStream, PrinterConfig printer, Vector3 offset)
			: base(printer, internalStream)
		{
			this.Offset = offset;

			printer.Settings.SettingChanged += Settings_SettingChanged;

			extruderIndex = printer.Connection.ActiveExtruderIndex;

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
			lastDestination.position -= Offset;
			if (extruderIndex < 4)
			{
				lastDestination.position += extruderOffsets[extruderIndex];
			}
			internalStream.SetPrinterPosition(lastDestination);
		}

		public Vector3 Offset { get; set; }

		public override string ReadLine()
		{
			string lineToSend = base.ReadLine();

			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			if (lineToSend != null
				&& lineToSend.StartsWith("T"))
			{
				int extruder = 0;
				if (GCodeFile.GetFirstNumberAfter("T", lineToSend, ref extruder))
				{
					extruderIndex = extruder;
				}
			}

			if (lineToSend != null
				&& LineIsMovement(lineToSend))
			{
				PrinterMove currentMove = GetPosition(lineToSend, lastDestination);

				PrinterMove moveToSend = currentMove;
				moveToSend.position += Offset;
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
	}
}