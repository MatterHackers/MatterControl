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

using MatterHackers.Agg;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class SoftwareEndstopsStream : GCodeStreamProxy
	{
		private PrinterMove lastDestination = PrinterMove.Unknown;

		AxisAlignedBoundingBox[] extruderBounds = new AxisAlignedBoundingBox[4];

		public SoftwareEndstopsStream(PrinterConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
			CalculateBounds();

			printer.Settings.SettingChanged += Settings_SettingChanged;

			// Register to listen for position after home and update Bounds based on the axis homed and position info.
		}

		private void Settings_SettingChanged(object sender, StringEventArgs stringEvent)
		{
			if (stringEvent.Data == SettingsKey.bed_size
				|| stringEvent.Data == SettingsKey.print_center
				|| stringEvent.Data == SettingsKey.build_height
				|| stringEvent.Data == SettingsKey.bed_shape)
			{
				this.CalculateBounds();
			}
		}

		public override void Dispose()
		{
			printer.Settings.SettingChanged -= Settings_SettingChanged;

			base.Dispose();
		}

		private void CalculateBounds()
		{
			var bedSize = printer.Settings.GetValue<Vector2>(SettingsKey.bed_size);
			var printCenter = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);
			var buildHeight = printer.Settings.GetValue<double>(SettingsKey.build_height);
			if (buildHeight == 0)
			{
				buildHeight = double.PositiveInfinity;
			}

			// first set all the extruders to the bounds defined by the settings file
			for (int i = 0; i < extruderBounds.Length; i++)
			{
				extruderBounds[i] = new AxisAlignedBoundingBox(
					printCenter.X - bedSize.X / 2, // min x
					printCenter.Y - bedSize.Y / 2, // min y
					0, // min z
					printCenter.X + bedSize.X / 2, // max x
					printCenter.Y + bedSize.Y / 2, // max y
					buildHeight); // max z

			}

			// if we have more constrained info for extruders, add that it

			// If we know something about the homing positions, add them in.
			// Specifically never go above a z homes max endstop.
			// We may also want to add in all other know (measured) endstop postions.

		}

		public override string ReadLine()
		{
			string lineToSend = base.ReadLine();

			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			if (lineToSend != null
				&& LineIsMovement(lineToSend))
			{
				PrinterMove currentMove = GetPosition(lineToSend, lastDestination);

				PrinterMove moveToSend = currentMove;

				if (moveToSend.HaveAnyPosition)
				{
					ClampToPrinter(ref moveToSend);
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
			internalStream.SetPrinterPosition(lastDestination);
		}

		private void ClampToPrinter(ref PrinterMove moveToSend)
		{
			var bounds = extruderBounds[printer.Connection.ActiveExtruderIndex];
			if (moveToSend.position.X < bounds.MinXYZ.X)
			{
				moveToSend.position.X = bounds.MinXYZ.X;
				// If we clamp, than do not do any extrusion at all
				moveToSend.extrusion = 0;
			}
		}
	}
}