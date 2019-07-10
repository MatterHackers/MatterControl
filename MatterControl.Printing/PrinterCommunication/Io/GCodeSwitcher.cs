/*
Copyright (c) 2019, Lars Brubaker
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterControl.Printing.Pipelines
{
	public class GCodeSwitcher : GCodeStream, IGCodeLineReader
	{
		private GCodeMemoryFile switchToGCode = null;
		private readonly object locker = new object();
		private readonly List<string> commandQueue = new List<string>();

		private string lastLine = "";

		public GCodeSwitcher(Stream gcodeStream, PrintHostConfig printer, int startLine = 0)
			: base(printer)
		{
			var settings = this.printer.Settings;
			var maxAcceleration = settings.GetValue<double>(SettingsKey.max_acceleration);
			var maxVelocity = settings.GetValue<double>(SettingsKey.max_velocity);
			var jerkVelocity = settings.GetValue<double>(SettingsKey.jerk_velocity);
			var multiplier = settings.GetValue<double>(SettingsKey.print_time_estimate_multiplier) / 100.0;

			var fileStreaming = GCodeFile.Load(gcodeStream,
				new Vector4(maxAcceleration, maxAcceleration, maxAcceleration, maxAcceleration),
				new Vector4(maxVelocity, maxVelocity, maxVelocity, maxVelocity),
				new Vector4(jerkVelocity, jerkVelocity, jerkVelocity, jerkVelocity),
				new Vector4(multiplier, multiplier, multiplier, multiplier),
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

					lastLine = lineToSend;

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
							// find the first layer in the new code that is greater than or equal to our current height
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

								if (change)
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

				lastLine = GCodeFile.Instruction(LineIndex++).Line;

				return lastLine;
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
					var settings = this.printer.Settings;
					var maxAcceleration = settings.GetValue<double>(SettingsKey.max_acceleration);
					var maxVelocity = settings.GetValue<double>(SettingsKey.max_velocity);
					var jerkVelocity = settings.GetValue<double>(SettingsKey.jerk_velocity);
					var multiplier = settings.GetValue<double>(SettingsKey.print_time_estimate_multiplier) / 100.0;

					var switchToGCode = GCodeFile.Load(new StreamReader(gcodeFilename).BaseStream,
						new Vector4(maxAcceleration, maxAcceleration, maxAcceleration, maxAcceleration),
						new Vector4(maxVelocity, maxVelocity, maxVelocity, maxVelocity),
						new Vector4(jerkVelocity, jerkVelocity, jerkVelocity, jerkVelocity),
						new Vector4(multiplier, multiplier, multiplier, multiplier),
						CancellationToken.None);

					if (switchToGCode is GCodeMemoryFile memoryFile)
					{
						this.switchToGCode = memoryFile;
					}
				});
			}
		}

		public override GCodeStream InternalStream => null;

		public override string DebugInfo => lastLine;
	}
}