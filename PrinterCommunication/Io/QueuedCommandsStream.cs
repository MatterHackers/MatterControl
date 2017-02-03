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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class QueuedCommandsStream : GCodeStreamProxy
	{
		public const string MacroPrefix = "; Command:";
		private List<string> commandQueue = new List<string>();
		private object locker = new object();
		private bool waitingForUserInput = false;
		private double maxTimeToWaitForOk = 0;
		private int repeatCommandIndex = 0;
		private List<string> commandsToRepeat = new List<string>();
		private Stopwatch timeHaveBeenWaiting = new Stopwatch();

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

		public void Cancel()
		{
			Reset();
		}

		public void Continue()
		{
			waitingForUserInput = false;
			timeHaveBeenWaiting.Reset();
			maxTimeToWaitForOk = 0;
			commandsToRepeat.Clear();
		}

		public override string ReadLine()
		{
			string lineToSend = null;

			if (waitingForUserInput)
			{
				lineToSend = "";
				Thread.Sleep(100);

				if(timeHaveBeenWaiting.IsRunning
					&& timeHaveBeenWaiting.Elapsed.TotalSeconds > maxTimeToWaitForOk)
				{
					if(commandsToRepeat.Count > 0)
					{
						// We timed out without the user responding. Cancel the operation.
						Reset();
					}
					else
					{
						// everything normal continue after time waited
						Continue();
					}
				}

				if (maxTimeToWaitForOk > 0
					&& timeHaveBeenWaiting.Elapsed.TotalSeconds < maxTimeToWaitForOk
					&& commandsToRepeat.Count > 0)
				{
					lineToSend = commandsToRepeat[repeatCommandIndex % commandsToRepeat.Count];
					repeatCommandIndex++;
				}
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

						var macroMatch = Regex.Match(lineToSend, "MacroImage:([^\\s]+)\\s*");
						var macroImage = macroMatch.Success ? LoadImageAsset(macroMatch.Groups[1].Value) : null;

						switch (command)
						{
							case "ChooseMaterial":
								waitingForUserInput = true;
								UiThread.RunOnIdle(() => RunningMacroPage.Show(messages.Count > 0 ? messages[0] : "", true, true));
								break;

							case "Close":
								UiThread.RunOnIdle(() => WizardWindow.Close("Macro"));
								break;

							case "Ding":
								MatterControlApplication.Instance.PlaySound("timer-done.wav");
								break;

							case "Message":
								if (messages.Count > 0)
								{
									double seconds = 0;
									GCodeFile.GetFirstNumberAfter("ExpectedSeconds:", lineToSend, ref seconds);
									double temperature = 0;
									GCodeFile.GetFirstNumberAfter("ExpectedTemperature:", lineToSend, ref temperature);
									UiThread.RunOnIdle(() => RunningMacroPage.Show(messages[0], expectedSeconds: seconds, expectedTemperature: temperature, image: macroImage));
								}
								break;

							case "RepeatUntil": // Repeat a command until the user clicks or or the max time elapses.
								if(messages.Count > 1)
								{
									double seconds = 10;
									GCodeFile.GetFirstNumberAfter("ExpectedSeconds:", lineToSend, ref seconds);
									timeHaveBeenWaiting.Restart();
									maxTimeToWaitForOk = seconds;
									waitingForUserInput = true;
									for (int i = 1; i < messages.Count; i++)
									{
										commandsToRepeat.Add(messages[i]);
									}

									UiThread.RunOnIdle(() => RunningMacroPage.Show(messages[0], true, expectedSeconds: seconds, image: macroImage));
								}
								break;

							case "WaitOK":
								waitingForUserInput = true;
								UiThread.RunOnIdle(() => RunningMacroPage.Show(messages.Count > 0 ? messages[0] : "", true, image: macroImage));
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

		public ImageBuffer LoadImageAsset(string uri)
		{
			string filePath = Path.Combine("Images", "Macros", uri);
			bool imageOnDisk = false;
			if (uri.IndexOfAny(Path.GetInvalidFileNameChars()) == 0)
			{
				try
				{
					imageOnDisk = StaticData.Instance.FileExists(filePath);
				}
				catch
				{
					imageOnDisk = false;
				}
			}
			
			if (imageOnDisk)
			{
				return StaticData.Instance.LoadImage(filePath);
			}
			else
			{
				var imageBuffer = new ImageBuffer(320, 10);

				ApplicationController.Instance.DownloadToImageAsync(imageBuffer, uri, true);

				return imageBuffer;
			}
		}


		public void Reset()
		{
			lock (locker)
			{
				commandQueue.Clear();
			}

			waitingForUserInput = false;
			timeHaveBeenWaiting.Reset();
			maxTimeToWaitForOk = 0;
			UiThread.RunOnIdle(() => WizardWindow.Close("Macro"));
		}
	}
}