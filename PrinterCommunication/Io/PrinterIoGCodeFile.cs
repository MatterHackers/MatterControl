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

using MatterHackers.GCodeVisualizer;
using System;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class PrinterIoGCodeFile : PrinterIoBase
	{
		private GCodeFile loadedGCode;
		private int printerCommandQueueLineIndex;

		public PrinterIoGCodeFile(GCodeFile loadedGCode)
		{
			this.loadedGCode = loadedGCode;
		}

		public override double TotalSecondsInPrint
		{
			get
			{
				if (loadedGCode.LineCount > 0)
				{
					return loadedGCode.Instruction(0).secondsToEndFromHere;
				}

				return 0;
			}
		}

		private int backupAmount = 16;

		public int CurrentlyPrintingLayer
		{
			get
			{
				int currentIndex = printerCommandQueueLineIndex - backupAmount;
				if (currentIndex >= 0
					&& currentIndex < loadedGCode.LineCount)
				{
					for (int zIndex = 0; zIndex < loadedGCode.NumChangesInZ; zIndex++)
					{
						if (currentIndex < loadedGCode.GetInstructionIndexAtLayer(zIndex))
						{
							return zIndex - 1;
						}
					}

					return loadedGCode.NumChangesInZ - 1;
				}

				return -1;
			}
		}

		public int TotalLayersInPrint
		{
			get
			{
				try
				{
					int layerCount = loadedGCode.NumChangesInZ;
					return layerCount;
				}
				catch
				{
					return -1;
				}
			}
		}

		public double RatioIntoCurrentLayer
		{
			get
			{
				int currentLineIndex = printerCommandQueueLineIndex - backupAmount;
				if (currentLineIndex >= 0
					&& currentLineIndex < loadedGCode.LineCount)
				{
					int currentLayer = CurrentlyPrintingLayer;
					int startIndex = loadedGCode.GetInstructionIndexAtLayer(currentLayer);
					int endIndex = loadedGCode.LineCount - 1;
					if (currentLayer < loadedGCode.NumChangesInZ - 2)
					{
						endIndex = loadedGCode.GetInstructionIndexAtLayer(currentLayer + 1) - 1;
					}

					int deltaFromStart = Math.Max(0, currentLineIndex - startIndex);
					return deltaFromStart / (double)(endIndex - startIndex);
				}

				return 0;
			}
		}

		public override double SecondsRemaining
		{
			get
			{
				if (NumberOfInstruction > 0)
				{
					if (printerCommandQueueLineIndex >= 0
						&& printerCommandQueueLineIndex < loadedGCode.LineCount
						&& loadedGCode.Instruction(printerCommandQueueLineIndex).secondsToEndFromHere != 0)
					{
						return loadedGCode.Instruction(printerCommandQueueLineIndex).secondsToEndFromHere;
					}
				}

				return 0;
			}
		}
	}
}