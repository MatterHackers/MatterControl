﻿/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using System.Text.RegularExpressions;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class GCodeMacro
	{
		public string Name { get; set; }
		public string GCode { get; set; }
		public bool ActionGroup { get; set; }
		public DateTime LastModified { get; set; }

		public static string FixMacroName(string input)
		{
			int lengthLimit = 24;

			string result = Regex.Replace(input, @"\r\n?|\n", " ");

			if (result.Length > lengthLimit)
			{
				result = result.Substring(0, lengthLimit) + "...";
			}

			return result;
		}

		public void Run()
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected)
			{
				PrinterConnectionAndCommunication.Instance.MacroStart();
				SendCommandToPrinter(GCode);
				if (GCode.Contains(QueuedCommandsStream.MacroPrefix))
				{
					SendCommandToPrinter("\n" + QueuedCommandsStream.MacroPrefix + "close()");
				}
			}
		}

		protected void SendCommandToPrinter(string command)
		{
			PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow(command);
		}
	}
}