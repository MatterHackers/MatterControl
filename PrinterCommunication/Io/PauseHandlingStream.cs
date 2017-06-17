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
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class PauseHandlingStream : GCodeStreamProxy
	{
		protected PrinterMove lastDestination = new PrinterMove();
		private List<string> commandQueue = new List<string>();
		private object locker = new object();
		private PrinterMove moveLocationAtEndOfPauseCode;
		private Stopwatch timeSinceLastEndstopRead = new Stopwatch();
		bool readOutOfFilament = false;

		private EventHandler unregisterEvents;

		public PauseHandlingStream(GCodeStream internalStream)
			: base(internalStream)
		{
			PrinterConnection.Instance.ReadLine.RegisterEvent((s, e) =>
			{
				StringEventArgs currentEvent = e as StringEventArgs;
				if (currentEvent != null)
				{
					if (currentEvent.Data.Contains("ros_"))
					{
						if(currentEvent.Data.Contains("TRIGGERED"))
						{
							readOutOfFilament = true;
						}
					}
				}
			}, ref unregisterEvents);
		}

		public override void Dispose()
		{
			unregisterEvents?.Invoke(this, null);
			base.Dispose();
		}

		public enum PauseReason { UserRequested, PauseLayerReached, GCodeRequest, FilamentRunout }

		public PrinterMove LastDestination { get { return lastDestination; } }

		public void Add(string line)
		{
			// lock queue
			lock (locker)
			{
				commandQueue.Add(line);
			}
		}

		string pauseCaption = "Printer Paused".Localize();
		string layerPauseMessage = "Your 3D print has been auto-paused.\nPause layer{0} reached.".Localize();
		string filamentPauseMessage = "Out of filament detected\nYour 3D print has been paused.".Localize();

		public void DoPause(PauseReason pauseReason, string layerNumber = "")
		{
			var pcc = PrinterConnection.Instance;
			switch (pauseReason)
			{
				case PauseReason.UserRequested:
					// do nothing special
					break;

				case PauseReason.PauseLayerReached:
				case PauseReason.GCodeRequest:
					pcc.PauseOnLayer.CallEvents(pcc, new PrintItemWrapperEventArgs(pcc.activePrintItem));
					UiThread.RunOnIdle(() => StyledMessageBox.ShowMessageBox(ResumePrint, layerPauseMessage.FormatWith(layerNumber), pauseCaption, StyledMessageBox.MessageType.YES_NO, "Ok".Localize(), "Resume".Localize()));
					break;

				case PauseReason.FilamentRunout:
					pcc.FilamentRunout.CallEvents(pcc, new PrintItemWrapperEventArgs(pcc.activePrintItem));
					UiThread.RunOnIdle(() => StyledMessageBox.ShowMessageBox(ResumePrint, filamentPauseMessage, pauseCaption, StyledMessageBox.MessageType.YES_NO, "Ok".Localize(), "Resume".Localize()));
					break;
			}

			// Add the pause_gcode to the loadedGCode.GCodeCommandQueue
			string pauseGCode = ActiveSliceSettings.Instance.GetValue(SettingsKey.pause_gcode);

			// put in the gcode for pausing (if any)
			InjectPauseGCode(pauseGCode);

			// inject a marker to tell when we are done with the inserted pause code
			InjectPauseGCode("M114");

			InjectPauseGCode("MH_PAUSE");
		}

		private void ResumePrint(bool clickedOk)
		{
			// They clicked either Resume or Ok
			if (!clickedOk && PrinterConnection.Instance.PrinterIsPaused)
			{
				PrinterConnection.Instance.Resume();
			}
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
					commandQueue.RemoveAt(0);
				}
			}

			if (lineToSend == null)
			{
				if (!PrinterConnection.Instance.PrinterIsPaused)
				{
					lineToSend = base.ReadLine();
					if (lineToSend == null)
					{
						return lineToSend;
					}

					// We got a line from the gcode we are sending check if we should queue a request for filament runout
					if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.filament_runout_sensor))
					{
						// request to read the endstop state
						if (!timeSinceLastEndstopRead.IsRunning || timeSinceLastEndstopRead.ElapsedMilliseconds > 5000)
						{
							PrinterConnection.Instance.SendLineToPrinterNow("M119");
							timeSinceLastEndstopRead.Restart();
						}
					}
				}
				else
				{
					lineToSend = "";
				}
			}

			if (GCodeFile.IsLayerChange(lineToSend))
			{
				string layerNumber = lineToSend.Split(':')[1];
				if (PauseOnLayer(layerNumber))
				{
					DoPause(PauseReason.PauseLayerReached, $" {layerNumber}");
				}
			}
			else if (lineToSend.StartsWith("M226")
				|| lineToSend.StartsWith("@pause"))
			{
				DoPause(PauseReason.GCodeRequest);
				lineToSend = "";
			}
			else if (lineToSend == "MH_PAUSE")
			{
				moveLocationAtEndOfPauseCode = LastDestination;

				if (PrinterConnection.Instance.PrinterIsPrinting)
				{
					// remember where we were after we ran the pause gcode
					PrinterConnection.Instance.CommunicationState = CommunicationStates.Paused;
				}

				lineToSend = "";
			}
			else if (readOutOfFilament)
			{
				readOutOfFilament = false;
				DoPause(PauseReason.FilamentRunout);
				lineToSend = "";
			}

			// keep track of the position
			if (lineToSend != null
				&& LineIsMovement(lineToSend))
			{
				lastDestination = GetPosition(lineToSend, lastDestination);
			}

			return lineToSend;
		}

		public void Resume()
		{
			// first go back to where we were after executing the pause code
			Vector3 positionBeforeActualPause = moveLocationAtEndOfPauseCode.position;
			InjectPauseGCode("G92 E{0:0.###}".FormatWith(moveLocationAtEndOfPauseCode.extrusion));
			Vector3 ensureAllAxisAreSent = positionBeforeActualPause + new Vector3(.01, .01, .01);
			var feedRates = ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds();
			InjectPauseGCode("G1 X{0:0.###} Y{1:0.###} Z{2:0.###} F{3}".FormatWith(ensureAllAxisAreSent.x, ensureAllAxisAreSent.y, ensureAllAxisAreSent.z, feedRates.x + 1));
			InjectPauseGCode("G1 X{0:0.###} Y{1:0.###} Z{2:0.###} F{3}".FormatWith(positionBeforeActualPause.x, positionBeforeActualPause.y, positionBeforeActualPause.z, feedRates));

			string resumeGCode = ActiveSliceSettings.Instance.GetValue(SettingsKey.resume_gcode);
			InjectPauseGCode(resumeGCode);
			InjectPauseGCode("M114"); // make sure we know where we are after this resume code
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			lastDestination = position;
			internalStream.SetPrinterPosition(lastDestination);
		}

		private void InjectPauseGCode(string codeToInject)
		{
			codeToInject = GCodeProcessing.ReplaceMacroValues(codeToInject);

			codeToInject = codeToInject.Replace("\\n", "\n");
			string[] lines = codeToInject.Split('\n');

			for (int i = 0; i < lines.Length; i++)
			{
				string[] splitOnSemicolon = lines[i].Split(';');
				string trimedLine = splitOnSemicolon[0].Trim().ToUpper();
				if (trimedLine != "")
				{
					this.Add(trimedLine);
				}
			}
		}

		private bool PauseOnLayer(string layer)
		{
			int layerNumber;

			if (int.TryParse(layer, out layerNumber) && ActiveSliceSettings.Instance.Helpers.LayerToPauseOn().Contains(layerNumber))
			{
				return true;
			}
			return false;
		}
	}
}