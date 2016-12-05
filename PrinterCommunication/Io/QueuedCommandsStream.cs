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

using MatterHackers.Agg;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.Threading;
using MatterHackers.MatterControl.PrinterControls;
using System.Text.RegularExpressions;
using MatterHackers.Agg.UI;
using System;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.GCodeVisualizer;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class QueuedCommandsStream : GCodeStreamProxy
	{
		object locker = new object();
		private List<string> commandQueue = new List<string>();
		bool waitingForUserInput = false;

		public const string MacroPrefix = "; Command:";

		public QueuedCommandsStream(GCodeStream internalStream)
			: base(internalStream)
		{
		}

		public void Add(string line)
		{
			// lock queue
			lock (locker)
			{
				commandQueue.Add(line);
			}
		}

		public override string ReadLine()
		{
			string lineToSend = null;

			if (waitingForUserInput)
			{
				Thread.Sleep(100);
				lineToSend = "";
			}
			else
			{
				// lock queue
				lock (locker)
				{
					if (commandQueue.Count > 0)
					{
						lineToSend = commandQueue[0];
						lineToSend = GCodeProcessing.ReplaceMacroValues(lineToSend);
						commandQueue.RemoveAt(0);
					}
				}

				if (lineToSend != null)
				{
					if (lineToSend.StartsWith(MacroPrefix))
					{
						int spaceAfterCommand = lineToSend.IndexOf(' ', MacroPrefix.Length);
						string command;
						if (spaceAfterCommand > 0)
						{
							command = lineToSend.Substring(MacroPrefix.Length, spaceAfterCommand - MacroPrefix.Length);
						}
						else
						{
							command = lineToSend.Substring(MacroPrefix.Length);
						}

						List<string> messages = new List<string>();
						foreach (Match match in Regex.Matches(lineToSend, "\"([^\"]*)\""))
						{
							string matchString = match.ToString();
							messages.Add(matchString.Substring(1, matchString.Length - 2));
						}

						switch (command)
						{
							case "Message":
								if (messages.Count > 0)
								{
									double seconds = 0;
									GCodeFile.GetFirstNumberAfter("ExpectedSeconds:", lineToSend, ref seconds);
									double temperature = 0;
									GCodeFile.GetFirstNumberAfter("ExpectedTemperature:", lineToSend, ref temperature);
									UiThread.RunOnIdle(() => RunningMacroPage.Show(messages[0], expectedSeconds: seconds, ExpectedTemperature: temperature));
								}
								break;

							case "ChooseMaterial":
								waitingForUserInput = true;
								UiThread.RunOnIdle(() => RunningMacroPage.Show(messages.Count > 0 ? messages[0] : "", true, true));
								break;

							case "WaitOK":
								waitingForUserInput = true;
								UiThread.RunOnIdle(() => RunningMacroPage.Show(messages.Count > 0 ? messages[0] : "", true));
								break;

							case "Close":
								UiThread.RunOnIdle(() => WizardWindow.Close("Macro"));
								break;

							default:
								// Don't know the command. Print to terminal log?
								break;
						}

					}
				}
				else
				{
					lineToSend = base.ReadLine();
				}
			}

			return lineToSend;
		}

		public void Reset()
		{
			lock (locker)
			{
				commandQueue.Clear();
			}

			waitingForUserInput = false;
		}

		public void Continue()
		{
			waitingForUserInput = false;
		}
	}
}