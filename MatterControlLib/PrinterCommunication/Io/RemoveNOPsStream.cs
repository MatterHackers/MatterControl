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


using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class RemoveNOPsStream : GCodeStreamProxy
	{
		private bool filterM300;

		public override string DebugInfo => "";

		public RemoveNOPsStream(PrinterConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
			filterM300 = !printer.Settings.GetValue<bool>(SettingsKey.enable_firmware_sounds);
		}

		public override string ReadLine()
		{
			var baseLine = base.ReadLine();

			if (baseLine == null)
			{
				return null;
			}

			if (baseLine.EndsWith("; NO_PROCESSING"))
			{
				return baseLine;
			}

			var trimmedLine = baseLine.Trim();

			// check if the send is a sound and if needed filter it
			if (filterM300
				&& trimmedLine.StartsWith("M300"))
			{
				return "";
			}

			// if the line contains no actual movement information, don't' send it
			if (trimmedLine == "G1"
				|| trimmedLine == "G0")
			{
				return "";
			}

			return baseLine;
		}
	}
}