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
using System;
using MatterHackers.MatterControl.SlicerConfiguration;
using System.Linq;
using MatterHackers.GCodeVisualizer;
using System.Diagnostics;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class PauseHandlingStream : GCodeStreamProxy
	{
		object locker = new object();
		private List<string> commandQueue = new List<string>();
		protected PrinterMove lastDestination = new PrinterMove();
		public PrinterMove LastDestination { get { return lastDestination; } }
		PrinterMove moveLocationAtEndOfPauseCode;

		public override void SetPrinterPosition(PrinterMove position)
		{
			lastDestination = position;
			internalStream.SetPrinterPosition(lastDestination);
		}

		public PauseHandlingStream(GCodeStream internalStream)
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

		public enum PauseReason { UserRequested, PauseLayerReached, GCodeRequest, FillamentRunout }
		public void DoPause(PauseReason pauseReason)
		{
			var pcc = PrinterConnectionAndCommunication.Instance;
			switch (pauseReason)
			{
				case PauseReason.UserRequested:
					// do nothing special
					break;

				case PauseReason.PauseLayerReached:
				case PauseReason.GCodeRequest:
					pcc.PauseOnLayer.CallEvents(pcc, new PrintItemWrapperEventArgs(pcc.ActivePrintItem));
					break;

				case PauseReason.FillamentRunout:
					pcc.FillamentRunout.CallEvents(pcc, new PrintItemWrapperEventArgs(pcc.ActivePrintItem));
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

		Stopwatch timeSinceLastEndstopRead = new Stopwatch();

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
				if (!PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
				{
					lineToSend = base.ReadLine();
					if (lineToSend == null)
					{
						return lineToSend;
					}

					// We got a line from the gcode we are sending check if we should queue a request for fillament runout
					if(ActiveSliceSettings.Instance.GetValue(SettingsKey.fillament_runout_endstop) != "None")
					{
						// request to read the endstop state
						if (!timeSinceLastEndstopRead.IsRunning || timeSinceLastEndstopRead.ElapsedMilliseconds > 5000)
						{
							PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("M119");
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
					DoPause(PauseReason.PauseLayerReached);
				}
			}
			else if (lineToSend.StartsWith("M226") 
				|| lineToSend.StartsWith("@pause"))
			{
				DoPause(PauseReason.GCodeRequest);
				lineToSend = "";
			}
			else if (OutOfFillament())
			{
				DoPause(PauseReason.FillamentRunout);
				lineToSend = "";
			}
			else if (lineToSend == "MH_PAUSE")
			{
				moveLocationAtEndOfPauseCode = LastDestination;

				if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
				{
					// remember where we were after we ran the pause gcode
					PrinterConnectionAndCommunication.Instance.CommunicationState = PrinterConnectionAndCommunication.CommunicationStates.Paused;
				}

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

		private bool OutOfFillament()
		{
			if (ActiveSliceSettings.Instance.GetValue(SettingsKey.fillament_runout_endstop) != "None")
			{

			}

			return false;
		}
	}
}