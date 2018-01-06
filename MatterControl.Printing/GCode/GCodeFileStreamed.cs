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
#define MULTI_THREAD

using MatterHackers.Agg;
using MatterHackers.VectorMath;
using System;
using System.IO;

namespace MatterControl.Printing
{
	public class GCodeFileStreamed : GCodeFile
	{
		private StreamReader openGcodeStream;
		object locker = new object();

		private bool readLastLineOfFile = false;
		private int readLineCount = 0;
		private const int MaxLinesToBuffer = 128;
		private PrinterMachineInstruction[] readLinesRingBuffer = new PrinterMachineInstruction[MaxLinesToBuffer];

		public GCodeFileStreamed(string fileName)
		{
			var inStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			openGcodeStream = new StreamReader(inStream);
		}

		~GCodeFileStreamed()
		{
			CloseStream();
		}

		private void CloseStream()
		{
			if (openGcodeStream != null)
			{
				openGcodeStream.Close();
				openGcodeStream = null;
			}
		}

		public override int LineCount
		{
			get
			{
				if (openGcodeStream != null
					&& !readLastLineOfFile)
				{
					return Math.Max(readLineCount + 1, (int)(openGcodeStream.BaseStream.Length / 14));
				}

				return readLineCount;
			}
		}

		public long ByteCount
		{
			get
			{
				if (openGcodeStream != null
					&& !readLastLineOfFile)
				{
					return openGcodeStream.BaseStream.Length;
				}

				return 0;
			}
		}

		public long BytePosition
		{
			get
			{
				if (openGcodeStream != null
					&& !readLastLineOfFile)
				{
					return openGcodeStream.BaseStream.Position;
				}

				return 0;
			}
		}

		public override double TotalSecondsInPrint
		{
			get
			{
				// We don't know, so we always return -1 used to identify as streaming.
				return -1;
			}
		}

		public override void Clear()
		{
			CloseStream();

			readLastLineOfFile = false;
			readLineCount = 0;
		}

		public override Vector2 GetWeightedCenter()
		{
			throw new NotImplementedException("A streamed GCode file should not need to do this. Please validate the code that is calling this.");
		}

		public override RectangleDouble GetBounds()
		{
			throw new NotImplementedException("A streamed GCode file should not need to do this. Please validate the code that is calling this.");
		}

		public override double GetFilamentCubicMm(double filamentDiameter)
		{
			throw new NotImplementedException("A streamed GCode file should not need to do this. Please validate the code that is calling this.");
		}

		public override bool IsExtruding(int instructionIndexToCheck)
		{
			throw new NotImplementedException();
		}

		public override double GetLayerHeight()
		{
			throw new NotImplementedException();
		}

		public override double GetFirstLayerHeight()
		{
			throw new NotImplementedException();
		}

		public override double GetFilamentUsedMm(double filamentDiameter)
		{
			throw new NotImplementedException();
		}

		public override double PercentComplete(int instructionIndex)
		{
			lock(locker)
			{
				if (openGcodeStream != null
					&& openGcodeStream.BaseStream.Length > 0)
				{
					return (double)openGcodeStream.BaseStream.Position / (double)openGcodeStream.BaseStream.Length * 100.0;
				}
			}

			return 100;
		}

		public override int GetInstructionIndexAtLayer(int layerIndex)
		{
			return 0;
		}

		public override double GetFilamentDiameter()
		{
			return 0;
		}

		public override double GetFilamentWeightGrams(double filamentDiameterMm, double density)
		{
			return 0;
		}

		public override int GetLayerIndex(int instructionIndex)
		{
			return 0;
		}

		public override int LayerCount
		{
			get
			{
				return 0;
			}
		}

		private double feedRateMmPerMin = 0;
		private Vector3 lastPrinterPosition = new Vector3();
		private double lastEPosition = 0;

		public override PrinterMachineInstruction Instruction(int index)
		{
			lock(locker)
			{
				if (index < readLineCount - MaxLinesToBuffer)
				{
					throw new Exception("You are asking for a line we no longer have buffered");
				}

				while (index >= readLineCount)
				{
					string line = openGcodeStream.ReadLine();
					if (line == null)
					{
						readLastLineOfFile = true;
						line = "";
					}

					int ringBufferIndex = readLineCount % MaxLinesToBuffer;
					readLinesRingBuffer[ringBufferIndex] = new PrinterMachineInstruction(line);

					PrinterMachineInstruction instruction = readLinesRingBuffer[ringBufferIndex];
					Vector3 deltaPositionThisLine = new Vector3();
					double deltaEPositionThisLine = 0;
					string lineToParse = line.ToUpper().Trim();
					if (lineToParse.StartsWith("G0") || lineToParse.StartsWith("G1"))
					{
						double newFeedRateMmPerMin = 0;
						if (GetFirstNumberAfter("F", lineToParse, ref newFeedRateMmPerMin))
						{
							feedRateMmPerMin = newFeedRateMmPerMin;
						}

						Vector3 attemptedDestination = lastPrinterPosition;
						GetFirstNumberAfter("X", lineToParse, ref attemptedDestination.X);
						GetFirstNumberAfter("Y", lineToParse, ref attemptedDestination.Y);
						GetFirstNumberAfter("Z", lineToParse, ref attemptedDestination.Z);

						double ePosition = lastEPosition;
						GetFirstNumberAfter("E", lineToParse, ref ePosition);

						deltaPositionThisLine = attemptedDestination - lastPrinterPosition;
						deltaEPositionThisLine = Math.Abs(ePosition - lastEPosition);

						lastPrinterPosition = attemptedDestination;
						lastEPosition = ePosition;
					}
					else if (lineToParse.StartsWith("G92"))
					{
						double ePosition = 0;
						if (GetFirstNumberAfter("E", lineToParse, ref ePosition))
						{
							lastEPosition = ePosition;
						}
					}

					if (feedRateMmPerMin > 0)
					{
						instruction.secondsThisLine = (float)GetSecondsThisLine(deltaPositionThisLine, deltaEPositionThisLine, feedRateMmPerMin);
					}

					readLineCount++;
				}
			}

			return readLinesRingBuffer[index % MaxLinesToBuffer];
		}

		public override double Ratio0to1IntoContainedLayer(int instructionIndex)
		{
			if (ByteCount != 0)
			{
				return BytePosition / (double)ByteCount;
			}

			return 1;
		}
	}
}
