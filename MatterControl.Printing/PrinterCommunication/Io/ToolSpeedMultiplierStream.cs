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

using MatterHackers.Agg;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterControl.Printing.Pipelines
{
	public class ToolSpeedMultiplierStream : GCodeStreamProxy
	{
		private PrinterMove lastDestination = PrinterMove.Unknown;
		private double t0Multiplier;

		public ToolSpeedMultiplierStream(PrintHostConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
			t0Multiplier = printer.Settings.GetValue<double>(SettingsKey.t1_extrusion_move_speed_multiplier);
			printer.Settings.SettingChanged += Settings_SettingChanged;
		}

		private void Settings_SettingChanged(object sender, StringEventArgs stringEvent)
		{
			// we don't change the setting while printing
			if (stringEvent.Data == SettingsKey.t1_extrusion_move_speed_multiplier)
			{
				t0Multiplier = printer.Settings.GetValue<double>(SettingsKey.t1_extrusion_move_speed_multiplier);
			}
		}

		public override void Dispose()
		{
			base.Dispose();
		}

		public override string DebugInfo => $"Last Destination = {lastDestination}";

		public override void SetPrinterPosition(PrinterMove position)
		{
			this.lastDestination.CopyKnowSettings(position);
			internalStream.SetPrinterPosition(this.lastDestination);
		}

		public override string ReadLine()
		{
			string lineToSend = internalStream.ReadLine();

			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			if (lineToSend != null
				&& LineIsMovement(lineToSend))
			{
				PrinterMove currentMove = GetPosition(lineToSend, this.lastDestination);

				PrinterMove moveToSend = currentMove;
				// If we are on T1
				if (printer.Connection.ActiveExtruderIndex == 1)
				{
					bool extrusionDelta = currentMove.extrusion != this.lastDestination.extrusion;
					bool xyPositionDelta = currentMove.position.X != this.lastDestination.position.X || currentMove.position.Y != this.lastDestination.position.Y;
					// and there is both extrusion and position delta
					if (extrusionDelta && xyPositionDelta)
					{
						// modify the speed by the T1 multiplier
						moveToSend.feedRate *= t0Multiplier;
					}
				}

				if (moveToSend.HaveAnyPosition)
				{
					lineToSend = CreateMovementLine(moveToSend, this.lastDestination);
				}

				this.lastDestination = currentMove;
				return lineToSend;
			}

			return lineToSend;
		}
	}
}