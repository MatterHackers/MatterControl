/*
Copyright (c) 2019, Lars Brubaker
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

namespace MatterControl.Printing.Pipelines
{
	public class SoftwareEndstopsStream : GCodeStreamProxy
	{
		private PrinterMove lastDestination = PrinterMove.Unknown;

		AxisAlignedBoundingBox[] extruderBounds = new AxisAlignedBoundingBox[4];

		public SoftwareEndstopsStream(PrintHostConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
			CalculateBounds();

			printer.Settings.SettingChanged += Settings_SettingChanged;
			printer.Connection.HomingPositionChanged += Connection_HomingPositionChanged;

			// Register to listen for position after home and update Bounds based on the axis homed and position info.
		}

		public override string DebugInfo
		{
			get
			{
				return $"Last Destination = {lastDestination}";
			}
		}

		private void Connection_HomingPositionChanged(object sender, System.EventArgs e)
		{
			this.CalculateBounds();
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
			printer.Connection.HomingPositionChanged -= Connection_HomingPositionChanged;

			base.Dispose();
		}

		private void CalculateBounds()
		{
			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			AxisAlignedBoundingBox aabb = printer.Settings.BedAABB();

			// if the printer has no height set than allow it to go up any amount
			if(aabb.ZSize < 10)
			{
				aabb.MaxXYZ.Z = 200;
			}

			// if the printer has leveling enabled
			if(printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
			{
				// set to a big value to make sure we can get to any leveling position described (below the bed)
				aabb.MinXYZ.Z = -100; 
			}
			else // leave a little bit of room for baby stepping
			{
				aabb.MinXYZ.Z = -3;
			}

			// let the z endstop set the max z bounds
			var homingPosition = printer.Connection.HomingPosition;
			// If we know the homing endstop positions, add them in.
			if (homingPosition.Z != double.NegativeInfinity)
			{
				// Figure out which side of center it is on and modify the bounds
				// If the z is less than 20 we assume the printer is bottom homing
				if (homingPosition.Z < aabb.Center.Z)
				{
					aabb.MinXYZ.Z = homingPosition.Z;
				}
				else
				{
					aabb.MaxXYZ.Z = homingPosition.Z;
				}
			}

			// set all the extruders to the bounds defined by the aabb
			for (int i = 0; i < extruderBounds.Length; i++)
			{
				extruderBounds[i] = aabb;
			}

			// If we have more constrained info for extruders, add that in
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

				if (currentMove.HaveAnyPosition)
				{
					ClampToPrinter(ref currentMove);
					lineToSend = CreateMovementLine(currentMove, lastDestination);
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
			// clamp to each axis
			for (int i = 0; i < 3; i++)
			{
				if (moveToSend.position[i] < bounds.MinXYZ[i])
				{
					moveToSend.position[i] = bounds.MinXYZ[i];
					// If we clamp, than do not do any extrusion at all
					moveToSend.extrusion = lastDestination.extrusion;
				}
				else if (moveToSend.position[i] > bounds.MaxXYZ[i])
				{
					moveToSend.position[i] = bounds.MaxXYZ[i];
					// If we clamp, than do not do any extrusion at all
					moveToSend.extrusion = lastDestination.extrusion;
				}
			}
		}
	}
}