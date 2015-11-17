/*
Copyright (c) 2015, Lars Brubaker
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

using MatterHackers.SerialPortCommunication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrinterCommunication
{
	public interface IRepRapCallbacks
	{
		void FoundStart(object sender, EventArgs foundStringEventArgs);
		void PrintingCanContinue(object sender, EventArgs foundStringEventArgs);
		void SuppressEcho(object sender, EventArgs foundStringEventArgs);
		void ReadTemperatures(object sender, EventArgs foundStringEventArgs);
		void ReadSdProgress(object sender, EventArgs foundStringEventArgs);
		void ReadTargetPositions(object sender, EventArgs foundStringEventArgs);
		void PrinterRequestsResend(object sender, EventArgs foundStringEventArgs);
		void PrinterStatesExtensions(object sender, EventArgs foundStringEventArgs);
		void PrinterStatesFirmware(object sender, EventArgs foundStringEventArgs);
		void ExtruderTemperatureWasWritenToPrinter(object sender, EventArgs foundStringEventArgs);
		void BedTemperatureWasWritenToPrinter(object sender, EventArgs foundStringEventArgs);
		void FanSpeedWasWritenToPrinter(object sender, EventArgs foundStringEventArgs);
		void FanOffWasWritenToPrinter(object sender, EventArgs foundStringEventArgs);
		void ExtruderWasSetToAbsoluteMode(object sender, EventArgs foundStringEventArgs);
		void ExtruderWasSetToRelativeMode(object sender, EventArgs foundStringEventArgs);
		void MovementWasSetToAbsoluteMode(object sender, EventArgs foundStringEventArgs);
		void MovementWasSetToRelativeMode(object sender, EventArgs foundStringEventArgs);
		void AtxPowerUpWasWritenToPrinter(object sender, EventArgs foundStringEventArgs);
		void AtxPowerDownWasWritenToPrinter(object sender, EventArgs foundStringEventArgs);
	}

	public static class RepRapReadWriteCallbacks
	{
		public static void SetStandardCallbacks(IRepRapCallbacks instanceToHookTo,
			FoundStringStartsWithCallbacks ReadLineStartCallbacks , FoundStringContainsCallbacks ReadLineContainsCallbacks, 
			FoundStringStartsWithCallbacks WriteLineStartCallbacks, FoundStringContainsCallbacks WriteLineContainsCallbacks)
		{
			ReadLineStartCallbacks.AddCallbackToKey("start", instanceToHookTo.FoundStart);
			ReadLineStartCallbacks.AddCallbackToKey("start", instanceToHookTo.PrintingCanContinue);

			ReadLineStartCallbacks.AddCallbackToKey("ok", instanceToHookTo.SuppressEcho);
			ReadLineStartCallbacks.AddCallbackToKey("wait", instanceToHookTo.SuppressEcho);
			ReadLineStartCallbacks.AddCallbackToKey("T:", instanceToHookTo.SuppressEcho); // repatier

			ReadLineStartCallbacks.AddCallbackToKey("ok", instanceToHookTo.PrintingCanContinue);
			ReadLineStartCallbacks.AddCallbackToKey("Done saving file", instanceToHookTo.PrintingCanContinue);

			ReadLineStartCallbacks.AddCallbackToKey("ok T:", instanceToHookTo.ReadTemperatures); // marlin
			ReadLineStartCallbacks.AddCallbackToKey("ok T0:", instanceToHookTo.ReadTemperatures); // marlin
			ReadLineStartCallbacks.AddCallbackToKey("T:", instanceToHookTo.ReadTemperatures); // repatier
			ReadLineStartCallbacks.AddCallbackToKey("B:", instanceToHookTo.ReadTemperatures); // smoothie

			ReadLineStartCallbacks.AddCallbackToKey("SD printing byte", instanceToHookTo.ReadSdProgress); // repatier

			ReadLineStartCallbacks.AddCallbackToKey("C:", instanceToHookTo.ReadTargetPositions);
			ReadLineStartCallbacks.AddCallbackToKey("ok C:", instanceToHookTo.ReadTargetPositions); // smoothie is reporting the C: with an ok first.
			ReadLineStartCallbacks.AddCallbackToKey("X:", instanceToHookTo.ReadTargetPositions);

			ReadLineContainsCallbacks.AddCallbackToKey("RS:", instanceToHookTo.PrinterRequestsResend);
			ReadLineContainsCallbacks.AddCallbackToKey("Resend:", instanceToHookTo.PrinterRequestsResend);

			ReadLineContainsCallbacks.AddCallbackToKey("FIRMWARE_NAME:", instanceToHookTo.PrinterStatesFirmware);
			ReadLineStartCallbacks.AddCallbackToKey("EXTENSIONS:", instanceToHookTo.PrinterStatesExtensions);

			WriteLineStartCallbacks.AddCallbackToKey("M104", instanceToHookTo.ExtruderTemperatureWasWritenToPrinter);
			WriteLineStartCallbacks.AddCallbackToKey("M109", instanceToHookTo.ExtruderTemperatureWasWritenToPrinter);
			WriteLineStartCallbacks.AddCallbackToKey("M140", instanceToHookTo.BedTemperatureWasWritenToPrinter);
			WriteLineStartCallbacks.AddCallbackToKey("M190", instanceToHookTo.BedTemperatureWasWritenToPrinter);

			WriteLineStartCallbacks.AddCallbackToKey("M106", instanceToHookTo.FanSpeedWasWritenToPrinter);
			WriteLineStartCallbacks.AddCallbackToKey("M107", instanceToHookTo.FanOffWasWritenToPrinter);

			WriteLineStartCallbacks.AddCallbackToKey("M82", instanceToHookTo.ExtruderWasSetToAbsoluteMode);
			WriteLineStartCallbacks.AddCallbackToKey("M83", instanceToHookTo.ExtruderWasSetToRelativeMode);

			WriteLineStartCallbacks.AddCallbackToKey("G90", instanceToHookTo.MovementWasSetToAbsoluteMode);
			WriteLineStartCallbacks.AddCallbackToKey("G91", instanceToHookTo.MovementWasSetToRelativeMode);

			WriteLineStartCallbacks.AddCallbackToKey("M80", instanceToHookTo.AtxPowerUpWasWritenToPrinter);
			WriteLineStartCallbacks.AddCallbackToKey("M81", instanceToHookTo.AtxPowerDownWasWritenToPrinter);
		}
	}
}
