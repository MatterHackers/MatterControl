﻿/*
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
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class QueuedCommandsStream : GCodeStreamProxy
	{
		public const string MacroPrefix = "; host.";
		private List<string> commandQueue = new List<string>();
		private List<string> commandsToRepeat = new List<string>();
		private object locker = new object();
		private double maxTimeToWaitForOk = 0;
		private int repeatCommandIndex = 0;
		private bool runningMacro = false;
		private double startingBedTemp = 0;
		private List<double> startingExtruderTemps = new List<double>();
		private Stopwatch timeHaveBeenWaiting = new Stopwatch();
		private bool waitingForUserInput = false;

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

		public ImageBuffer LoadImageAsset(string uri)
		{
			string filePath = Path.Combine("Images", "Macros", uri);
			bool imageOnDisk = false;
			if (uri.IndexOfAny(Path.GetInvalidFileNameChars()) == -1)
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

		public override string ReadLine()
		{
			string lineToSend = null;

			if (waitingForUserInput)
			{
				lineToSend = "";
				Thread.Sleep(100);

				if (timeHaveBeenWaiting.IsRunning
					&& timeHaveBeenWaiting.Elapsed.TotalSeconds > maxTimeToWaitForOk)
				{
					if (commandsToRepeat.Count > 0)
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
					if (lineToSend.StartsWith(MacroPrefix) && lineToSend.TrimEnd().EndsWith(")"))
					{
						if (!runningMacro)
						{
							runningMacro = true;
							int extruderCount = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);
							for (int i = 0; i < extruderCount; i++)
							{
								startingExtruderTemps.Add(PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(i));
							}

							if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_heated_bed))
							{
								startingBedTemp = PrinterConnectionAndCommunication.Instance.TargetBedTemperature;
							}
						}
						int parensAfterCommand = lineToSend.IndexOf('(', MacroPrefix.Length);
						string command = "";
						if (parensAfterCommand > 0)
						{
							command = lineToSend.Substring(MacroPrefix.Length, parensAfterCommand - MacroPrefix.Length);
						}

						RunningMacroPage.MacroCommandData macroData = new RunningMacroPage.MacroCommandData();

						string value = "";
						if (TryGetAfterString(lineToSend, "title", out value))
						{
							macroData.title = value;
						}
						if (TryGetAfterString(lineToSend, "expire", out value))
						{
							double.TryParse(value, out macroData.expireTime);
							maxTimeToWaitForOk = macroData.expireTime;
						}
						if (TryGetAfterString(lineToSend, "count_down", out value))
						{
							double.TryParse(value, out macroData.countDown);
						}
						if (TryGetAfterString(lineToSend, "image", out value))
						{
							macroData.image = LoadImageAsset(value);
						}
						if (TryGetAfterString(lineToSend, "wait_ok", out value))
						{
							macroData.waitOk = value == "true";
						}
						if (TryGetAfterString(lineToSend, "repeat_gcode", out value))
						{
							foreach (string line in value.Split('|'))
							{
								commandsToRepeat.Add(line);
							}
						}

						switch (command)
						{
							case "choose_material":
								waitingForUserInput = true;
								macroData.showMaterialSelector = true;
								macroData.waitOk = true;
								UiThread.RunOnIdle(() => RunningMacroPage.Show(macroData));
								break;

							case "close":
								runningMacro = false;
								UiThread.RunOnIdle(() => WizardWindow.Close("Macro"));
								break;

							case "ding":
								MatterControlApplication.Instance.PlaySound("timer-done.wav");
								break;

							case "show_message":
								waitingForUserInput = macroData.waitOk | macroData.expireTime > 0;
								UiThread.RunOnIdle(() => RunningMacroPage.Show(macroData));
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

			if (runningMacro)
			{
				runningMacro = false;
				for (int i = 0; i < startingExtruderTemps.Count; i++)
				{
					PrinterConnectionAndCommunication.Instance.SetTargetExtruderTemperature(i, startingExtruderTemps[i]);
				}

				if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_heated_bed))
				{
					PrinterConnectionAndCommunication.Instance.TargetBedTemperature = startingBedTemp;
				}
			}
			waitingForUserInput = false;
			timeHaveBeenWaiting.Reset();
			maxTimeToWaitForOk = 0;
			UiThread.RunOnIdle(() => WizardWindow.Close("Macro"));
		}

		private bool TryGetAfterString(string macroLine, string variableName, out string value)
		{
			var macroMatch = Regex.Match(macroLine, variableName + ":\"([^\"]+)");
			value = macroMatch.Success ? macroMatch.Groups[1].Value : null;

			return macroMatch.Success;
		}
	}
}