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
using MatterHackers.MatterControl.PrinterCommunication;
using System;
using System.IO;

namespace MatterHackers.MatterControl.EeProm
{
	public class EePromRepetierParameter : EventArgs
	{
		public string description = "";
		public int type;
		public int position;
		public string value { get; private set; } = "";
		private bool changed = false;

		public EePromRepetierParameter(string line)
		{
			update(line);
		}

		public void update(string line)
		{
			if (line.Length > 4)
			{
				string[] lines = line.Substring(4).Split(' ');
				if (lines.Length > 2)
				{
					int.TryParse(lines[0], out type);
					int.TryParse(lines[1], out position);
					value = lines[2];
					int startPos = 7 + lines[0].Length + lines[1].Length + lines[2].Length;
					if (line.Length > startPos)
					{
						description = line.Substring(startPos);
					}
					changed = false;
				}
			}
		}

		public void Save(PrinterConnection printerConnection)
		{
			if (!changed)
			{
				return;
			}

			string cmd = "M206 T" + type + " P" + position + " ";
			if (type == 3) cmd += "X" + value;
			else cmd += "S" + value;
			printerConnection.QueueLine(cmd);
			changed = false;
		}

		public string Description
		{
			get { return description; }
			set { description = value; }
		}

		public string Value
		{
			get { return value; }
			set
			{
				value = value.Replace(',', '.').Trim();
				if (this.value.Equals(value)) return;
				this.value = value;
				MarkChanged();
			}
		}

		internal void MarkChanged()
		{
			changed = true;
		}
	}
}