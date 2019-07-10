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

namespace MatterControl.Printing.Pipelines
{
	public class QueuedCommandsStream : GCodeStreamProxy
	{
		private List<string> commandQueue = new List<string>();
		private object locker = new object();

		public QueuedCommandsStream(PrintHostConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
		}

		public int Count => commandQueue.Count;

		public override string DebugInfo
		{
			get
			{
				return "";
			}
		}

		public string Peek()
		{
			// lock queue
			lock (locker)
			{
				if (commandQueue.Count > 0)
				{
					return commandQueue[0];
				}
			}

			return null;
		}

		public string LastAdd()
		{
			// lock queue
			lock (locker)
			{
				if (commandQueue.Count > 0)
				{
					return commandQueue[commandQueue.Count - 1];
				}
			}

			return null;
		}

		public void Add(string lineIn, bool forceTopOfQueue = false)
		{
			// lock queue
			lock (locker)
			{
				if (lineIn.Contains("\\n"))
				{
					lineIn = lineIn.Replace("\\n", "\n");
				}

				// Check line for line breaks, split and process separate if necessary
				if (lineIn.Contains("\n"))
				{
					string[] linesToWrite = lineIn.Split(new string[] { "\n" }, StringSplitOptions.None);
					for (int i = 0; i < linesToWrite.Length; i++)
					{
						string line = linesToWrite[i].Trim();
						if (line.Length > 0)
						{
							this.Add(line);
						}
					}

					return;
				}

				if (forceTopOfQueue)
				{
					commandQueue.Insert(0, lineIn);
				}
				else
				{
					commandQueue.Add(lineIn);
				}
			}
		}

		public void Cancel()
		{
			Reset();
		}

		public override string ReadLine()
		{
			string lineToSend = null;

			// lock queue
			lock (locker)
			{
				if (commandQueue.Count > 0)
				{
					lineToSend = commandQueue[0];
					lineToSend = printer.Settings.ReplaceMacroValues(lineToSend);
					commandQueue.RemoveAt(0);
				}
			}

			if (lineToSend == null)
			{
				lineToSend = base.ReadLine();
			}

			return lineToSend;
		}

		public void Reset()
		{
			lock (locker)
			{
				commandQueue.Clear();
			}
		}
	}
}