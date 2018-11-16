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

using MatterControl.Printing;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class GCodeSwitcher : GCodeStream
	{
		private GCodeMemoryFile switchToGCode = null;
		private object locker = new object();
		private List<string> commandQueue = new List<string>();

		public GCodeSwitcher(string gcodeFilename, PrinterConfig printer, int startLine = 0)
			: base(printer)
		{
			var fileStreaming = GCodeFile.Load(gcodeFilename,
					new Vector4(),
					new Vector4(),
					new Vector4(),
					Vector4.One,
					CancellationToken.None);

			this.GCodeFile = fileStreaming;
			LineIndex = startLine;
		}

		public GCodeFile GCodeFile { get; private set; }
		public int LineIndex { get; private set; } = -1;

		public override void Dispose()
		{
		}

		public override string ReadLine()
		{
			lock (locker)
			{
				if (commandQueue.Count > 0)
				{
					var lineToSend = commandQueue[0];
					commandQueue.RemoveAt(0);
					return lineToSend;
				}
			}

			if (LineIndex < GCodeFile.LineCount)
			{
				if (LineIndex > 1
					&& GCodeFile is GCodeMemoryFile currentMemoryFile
					&& switchToGCode != null)
				{
					var prevlayerIndex = currentMemoryFile.GetLayerIndex(LineIndex - 1);
					var layerIndex = currentMemoryFile.GetLayerIndex(LineIndex);
					// we only try to switch as we are changing layers
					if (prevlayerIndex < layerIndex)
					{
						var currentBottom = currentMemoryFile.GetLayerBottom(layerIndex);
						// see if there is a layer height that is compatible in the new gcode
						for (int i = 0; i < switchToGCode.LayerCount; i++)
						{
							// find the first layer in the new code that is greater than or eaqual to our current height
							var switchBottom = switchToGCode.GetLayerBottom(i);
							if (switchBottom >= currentBottom)
							{
								bool change = false;
								// is the current gcode the same or bigger than the new gcode
								if (currentBottom >= switchBottom)
								{
									change = true;
								}
								else // only switch if we are within one layer height of the new gcode
								{
									if (currentBottom - switchBottom < switchToGCode.GetLayerHeight(layerIndex))
									{
										change = true;
									}
								}

								if(change)
								{
									GCodeFile = switchToGCode;
									LineIndex = switchToGCode.GetFirstLayerInstruction(i);
									var line = $"G92 E{switchToGCode.Instruction(LineIndex).EPosition:0.###}";
									lock (locker)
									{
										commandQueue.Add(line);
									}
									switchToGCode = null;
									// return a dwell to exhaust the command queue on the firmware
									return "G4 P1";
								}

								// we are done evaluating after the first found layer
								break;
							}
						}
					}
				}

				return GCodeFile.Instruction(LineIndex++).Line;
			}

			return null;
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
		}

		public void SwitchTo(string gcodeFilename)
		{
			if (GCodeFile is GCodeMemoryFile)
			{
				Task.Run(() =>
				{
					var switchToGCode = GCodeFile.Load(gcodeFilename,
							new Vector4(),
							new Vector4(),
							new Vector4(),
							Vector4.One,
							CancellationToken.None);

					if (switchToGCode is GCodeMemoryFile memoryFile)
					{
						this.switchToGCode = memoryFile;
					}
				});
			}
		}
	}
}