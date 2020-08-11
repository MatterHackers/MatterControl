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

using System;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class BabyStepsStream : GCodeStreamProxy
	{
		private int extruderIndex = 0;
		private readonly Vector3[] extruderOffsets = new Vector3[4];
		private PrinterMove outputWithBabyStepping = PrinterMove.Unknown;
		private PrinterMove inputNoBabyStepping = PrinterMove.Unknown;
		private double[] _babyStepOffset = new double[4];

		public double SetBabbyStepOffset(int index, double value) => _babyStepOffset[index] = value;

		public double GetBabbyStepOffset(int index) => _babyStepOffset[index];

		public Vector3 BabbyStepOffsetT1 { get; private set; } = Vector3.Zero;

		public BabyStepsStream(PrinterConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
			printer.Settings.SettingChanged += Printer_SettingChanged;

			extruderIndex = printer.Connection.ActiveExtruderIndex;

			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				SetBabbyStepOffset(i, value);
			});

			ReadExtruderOffsets();
		}

		private void ReadExtruderOffsets()
		{
			for (int i = 0; i < 4; i++)
			{
				extruderOffsets[i] = printer.Settings.Helpers.ExtruderOffset(i);
			}
		}

		public override string DebugInfo
		{
			get
			{
				return $"Last Destination = {inputNoBabyStepping}";
			}
		}

		private void Printer_SettingChanged(object s, StringEventArgs e)
		{
			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				if (e?.Data == key)
				{
					SetBabbyStepOffset(i, value);
				}
			});

			if (e?.Data == SettingsKey.extruder_offset
				&& !printer.Connection.Printing
				&& !printer.Connection.Paused)
			{
				// if the offsets change update them (unless we are actively printing)
				ReadExtruderOffsets();
			}
		}

		public override void SetPrinterPosition(PrinterMove outputPosition)
		{
			outputWithBabyStepping.CopyKnowSettings(outputPosition);

			// calculate our offset to pass on to internal streams
			inputNoBabyStepping = outputWithBabyStepping;

			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				if (extruderIndex == i)
				{
					inputNoBabyStepping.position = new Vector3(inputNoBabyStepping.position.X,
						inputNoBabyStepping.position.Y,
						inputNoBabyStepping.position.Z - GetBabbyStepOffset(i));
				}
			});

			inputNoBabyStepping.position += extruderOffsets[Math.Min(extruderIndex, 4)];

			internalStream.SetPrinterPosition(inputNoBabyStepping);
		}

		public override void Dispose()
		{
			printer.Settings.SettingChanged -= Printer_SettingChanged;

			base.Dispose();
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
				inputNoBabyStepping = GetPosition(lineToSend, inputNoBabyStepping);

				// it is a struct so this is making a new copy we con modify
				PrinterMove moveToSend = inputNoBabyStepping;
				printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
				{
					if (extruderIndex == i)
					{
						moveToSend.position = new Vector3(moveToSend.position.X,
							moveToSend.position.Y,
							moveToSend.position.Z + GetBabbyStepOffset(i));
					}
				});

				moveToSend.position -= extruderOffsets[Math.Min(extruderIndex, 4)];

				if (moveToSend.HaveAnyPosition)
				{
					lineToSend = CreateMovementLine(moveToSend, outputWithBabyStepping);
				}

				outputWithBabyStepping = moveToSend;

				return lineToSend;
			}

			return lineToSend;
		}
	}
}