/*
Copyright (c) 2016, Lars Brubaker
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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterControl.Printing
{
	public abstract class GCodeFile
	{
		public const string PostProcessedExtension = ".postprocessed.gcode";

#if __ANDROID__
		protected const int Max32BitFileSize = 10000000; // 10 megs
#else
		protected const int Max32BitFileSize = 100000000; // 100 megs
#endif

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		// the number of lines in the file
		public abstract int LineCount { get; }

		public abstract int LayerCount { get; }

		public abstract double TotalSecondsInPrint { get; }

		public abstract void Clear();

		public abstract RectangleDouble GetBounds();

		public abstract double GetFilamentCubicMm(double filamentDiameter);

		public abstract double GetFilamentDiameter();

		public abstract double GetFilamentUsedMm(double filamentDiameter);

		public abstract double GetFilamentWeightGrams(double filamentDiameterMm, double density);

		public abstract int GetFirstLayerInstruction(int layerIndex);

		public abstract double GetLayerHeight(int layerIndex);

		public abstract double GetLayerTop(int layerIndex);

		public abstract int GetLayerIndex(int instructionIndex);

		public abstract Vector2 GetWeightedCenter();

		public abstract PrinterMachineInstruction Instruction(int i);

		public abstract bool IsExtruding(int instructionIndexToCheck);

		public abstract double PercentComplete(int instructionIndex);

		public abstract double Ratio0to1IntoContainedLayerSeconds(int instructionIndex);

		public abstract double Ratio0to1IntoContainedLayerInstruction(int instructionIndex);

		public static int CalculateChecksum(string commandToGetChecksumFor)
		{
			int checksum = 0;
			if (commandToGetChecksumFor.Length > 0)
			{
				checksum = commandToGetChecksumFor[0];
				for (int i = 1; i < commandToGetChecksumFor.Length; i++)
				{
					checksum ^= commandToGetChecksumFor[i];
				}
			}

			return checksum;
		}

		private static readonly Regex FirstDigitsAfterToken = new Regex("\\d+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

		private static readonly string[] LayerLineStartTokens = new[]
		{
			"; LAYER:",
			";LAYER:",
			"; layer ",
		};

		public static bool IsLayerChange(string line)
		{
			return LayerLineStartTokens.Any(l => line.StartsWith(l));
		}

		public static int GetLayerNumber(string line)
		{
			var layerToken = LayerLineStartTokens.FirstOrDefault(t => line.StartsWith(t));

			if (layerToken != null)
			{
				line = line.Substring(layerToken.Length);

				// Find the first digits after the layer start token
				var match = FirstDigitsAfterToken.Match(line);

				if (match.Success
					&& int.TryParse(match.Value, out int layerNumber))
				{
					return layerNumber;
				}
			}

			return 0;
		}

		public static bool FileTooBigToLoad(Stream fileStream)
		{
			if (Is32Bit)
			{
				// Let's make sure we can load a file this big
				if (fileStream.Length > Max32BitFileSize)
				{
					// It is too big to load
					return true;
				}
			}

			return false;
		}

		public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref int readValue, int startIndex = 0, string stopCheckingString = ";")
		{
			return GetFirstNumberAfter(stringToCheckAfter, stringWithNumber, ref readValue, out _, startIndex, stopCheckingString);
		}

		public static string GetLineWithoutChecksum(string inLine)
		{
			if (inLine.StartsWith("N"))
			{
				int lineNumber = 0;
				if (GCodeFile.GetFirstNumberAfter("N", inLine, ref lineNumber, out int numberEnd))
				{
					var outLine = inLine.Substring(numberEnd).Trim();
					int checksumStart = outLine.IndexOf('*');
					if (checksumStart != -1)
					{
						return outLine.Substring(0, checksumStart);
					}
				}
			}

			return inLine;
		}

		public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref int readValue, out int numberEnd, int startIndex = 0, string stopCheckingString = ";")
		{
			double doubleValue = readValue;
			if (GetFirstNumberAfter(stringToCheckAfter, stringWithNumber, ref doubleValue, out numberEnd, startIndex, stopCheckingString))
			{
				readValue = (int)doubleValue;
				return true;
			}

			return false;
		}

		public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref double readValue, int startIndex = 0, string stopCheckingString = ";")
		{
			return GetFirstNumberAfter(stringToCheckAfter, stringWithNumber, ref readValue, out _, startIndex, stopCheckingString);
		}

		public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref double readValue, out int numberEnd, int startIndex = 0, string stopCheckingString = ";")
		{
			int stringPos = stringWithNumber.IndexOf(stringToCheckAfter, Math.Min(stringWithNumber.Length, startIndex));
			int stopPos = stringWithNumber.IndexOf(stopCheckingString);
			if (stringPos != -1
				&& (stopPos == -1 || stringPos < stopPos || string.IsNullOrEmpty(stopCheckingString)))
			{
				stringPos += stringToCheckAfter.Length;
				readValue = agg_basics.ParseDouble(stringWithNumber, ref stringPos, true);
				numberEnd = stringPos;

				return true;
			}

			numberEnd = -1;
			return false;
		}

		public static bool GetFirstStringAfter(string stringToCheckAfter, string fullStringToLookIn, string separatorString, ref string nextString, int startIndex = 0)
		{
			int stringPos = fullStringToLookIn.IndexOf(stringToCheckAfter, startIndex);
			if (stringPos != -1)
			{
				int separatorPos = fullStringToLookIn.IndexOf(separatorString, stringPos);
				if (separatorPos != -1)
				{
					nextString = fullStringToLookIn.Substring(stringPos + stringToCheckAfter.Length, separatorPos - (stringPos + stringToCheckAfter.Length));
					return true;
				}
			}

			return false;
		}

		public static GCodeFile Load(Stream fileStream,
			Vector4 maxAccelerationMmPerS2,
			Vector4 maxVelocityMmPerS,
			Vector4 velocitySameAsStopMmPerS,
			Vector4 speedMultiplier,
			CancellationToken cancellationToken)
		{
			if (FileTooBigToLoad(fileStream))
			{
				return new GCodeFileStreamed(fileStream);
			}
			else
			{
				return GCodeMemoryFile.Load(fileStream,
					maxAccelerationMmPerS2,
					maxVelocityMmPerS,
					velocitySameAsStopMmPerS,
					speedMultiplier,
					cancellationToken,
					null);
			}
		}

		public static string ReplaceNumberAfter(char charToReplaceAfter, string stringWithNumber, double numberToPutIn)
		{
			int charPos = stringWithNumber.IndexOf(charToReplaceAfter);
			if (charPos != -1)
			{
				int spacePos = stringWithNumber.IndexOf(" ", charPos);
				if (spacePos == -1)
				{
					string newString = string.Format("{0}{1:0.#####}", stringWithNumber.Substring(0, charPos + 1), numberToPutIn);
					return newString;
				}
				else
				{
					string newString = string.Format("{0}{1:0.#####}{2}", stringWithNumber.Substring(0, charPos + 1), numberToPutIn, stringWithNumber.Substring(spacePos));
					return newString;
				}
			}

			return stringWithNumber;
		}

		private static readonly bool Is32Bit = IntPtr.Size == 4;
	}
}