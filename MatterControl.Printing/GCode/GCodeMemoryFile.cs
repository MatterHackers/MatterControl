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
#define DUMP_SLOW_TIMES

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.GCodeVisualizer
{
	public class GCodeMemoryFile : GCodeFile
	{
		private double amountOfAccumulatedEWhileParsing = 0;

		private List<int> indexOfChangeInZ = new List<int>();
		private Vector2 center = Vector2.Zero;
		private double parsingLastZ;
		private bool gcodeHasExplicitLayerChangeInfo = false;
		private double firstLayerThickness;
		private double layerThickness;

        private double filamentUsedMmCache = 0;
        private double diameterOfFilamentUsedMmCache = 0;

		private List<PrinterMachineInstruction> GCodeCommandQueue = new List<PrinterMachineInstruction>();

		public GCodeMemoryFile(bool gcodeHasExplicitLayerChangeInfo = false)
		{
			this.gcodeHasExplicitLayerChangeInfo = gcodeHasExplicitLayerChangeInfo;
		}

		public GCodeMemoryFile(string pathAndFileName, CancellationToken cancellationToken, bool gcodeHasExplicitLayerChangeInfo = false)
		{
			this.gcodeHasExplicitLayerChangeInfo = gcodeHasExplicitLayerChangeInfo;

			var loadedFile = GCodeMemoryFile.Load(pathAndFileName, cancellationToken, null);
			if (loadedFile != null)
			{
				this.indexOfChangeInZ = loadedFile.indexOfChangeInZ;
				this.center = loadedFile.center;
				this.parsingLastZ = loadedFile.parsingLastZ;
				this.GCodeCommandQueue = loadedFile.GCodeCommandQueue;
			}
		}

		public override PrinterMachineInstruction Instruction(int index)
		{
			return GCodeCommandQueue[index];
		}

		public override int LineCount => GCodeCommandQueue.Count;

		public override void Clear()
		{
			indexOfChangeInZ.Clear();
			GCodeCommandQueue.Clear();
		}

		public override double TotalSecondsInPrint => Instruction(0).secondsToEndFromHere;

		public void Add(PrinterMachineInstruction printerMachineInstruction)
		{
			Insert(LineCount, printerMachineInstruction);
		}

		public void Insert(int insertIndex, PrinterMachineInstruction printerMachineInstruction)
		{
			for (int i = 0; i < indexOfChangeInZ.Count; i++)
			{
				if (insertIndex < indexOfChangeInZ[i])
				{
					indexOfChangeInZ[i]++;
				}
			}

			GCodeCommandQueue.Insert(insertIndex, printerMachineInstruction);
		}

		public static GCodeFile ParseGCodeString(string gcodeContents, CancellationToken cancellationToken)
		{
			return ParseFileContents(gcodeContents, cancellationToken, null);
		}

		public static GCodeMemoryFile Load(Stream fileStream, CancellationToken cancellationToken, ReportProgressRatio<(double ratio, string state)> progressReporter = null)
		{
			try
			{
				using (var reader = new StreamReader(fileStream))
				{
					return ParseFileContents(reader.ReadToEnd(), cancellationToken, progressReporter);
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
			}

			return null;
		}

		public static GCodeMemoryFile Load(string filePath, CancellationToken cancellationToken, ReportProgressRatio<(double ratio, string state)> progressReporter)
		{
			if (Path.GetExtension(filePath).ToUpper() == ".GCODE")
			{
				try
				{
					using (var stream = File.OpenRead(filePath))
					{
						return Load(stream, cancellationToken, progressReporter);
					}
				}
				catch (Exception e)
				{
					Debug.Print(e.Message);
				}
			}

			return null;
		}

		private static IEnumerable<string> CustomSplit(string newtext, char splitChar)
		{
			int endOfLastFind = 0;
			int positionOfSplitChar = newtext.IndexOf(splitChar);
			while (positionOfSplitChar != -1)
			{
				string text = newtext.Substring(endOfLastFind, positionOfSplitChar - endOfLastFind).Trim();
				yield return text;
				endOfLastFind = positionOfSplitChar + 1;
				positionOfSplitChar = newtext.IndexOf(splitChar, endOfLastFind);
			}

			string lastText = newtext.Substring(endOfLastFind);
			yield return lastText;
		}

		private static int CountNumLines(string gCodeString)
		{
			int crCount = 0;
			foreach (char testCharacter in gCodeString)
			{
				if (testCharacter == '\n')
				{
					crCount++;
				}
			}

			return crCount + 1;
		}

		public static GCodeMemoryFile ParseFileContents(string gCodeString, CancellationToken cancellationToken, ReportProgressRatio<(double ratio, string state)> progressReporter)
		{
			if (gCodeString == null)
			{
				return null;
			}

			Stopwatch loadTime = Stopwatch.StartNew();

			Stopwatch maxProgressReport = new Stopwatch();
			maxProgressReport.Start();
			PrinterMachineInstruction machineInstructionForLine = new PrinterMachineInstruction("None");

			bool gcodeHasExplicitLayerChangeInfo = false;
			if (gCodeString.Contains("LAYER:"))
			{
				gcodeHasExplicitLayerChangeInfo = true;
			}

			GCodeMemoryFile loadedGCodeFile = new GCodeMemoryFile(gcodeHasExplicitLayerChangeInfo);

			int crCount = CountNumLines(gCodeString);
			int lineIndex = 0;
			foreach (string outputString in CustomSplit(gCodeString, '\n'))
			{
				string lineString = outputString.Trim();
				machineInstructionForLine = new PrinterMachineInstruction(lineString, machineInstructionForLine, false);

				if (lineString.Length > 0)
				{
					switch (lineString[0])
					{
						case 'G':
							loadedGCodeFile.ParseGLine(lineString, machineInstructionForLine);
							break;

						case 'M':
							loadedGCodeFile.ParseMLine(lineString, machineInstructionForLine);
							break;

						case 'T':
							double extruderIndex = 0;
							if (GetFirstNumberAfter("T", lineString, ref extruderIndex))
							{
								machineInstructionForLine.ExtruderIndex = (int)extruderIndex;
							}
							break;

						case ';':
							if (gcodeHasExplicitLayerChangeInfo && IsLayerChange(lineString))
							{
								loadedGCodeFile.IndexOfChangeInZ.Add(loadedGCodeFile.GCodeCommandQueue.Count);
							}
							if (lineString.StartsWith("; layerThickness"))
							{
								loadedGCodeFile.layerThickness = double.Parse(lineString.Split('=')[1]);
							}
							else if (lineString.StartsWith("; firstLayerThickness") && loadedGCodeFile.firstLayerThickness == 0)
							{
								loadedGCodeFile.firstLayerThickness = double.Parse(lineString.Split('=')[1]);
							}
							break;

						case '@':
							break;

						default:
#if DEBUG
							throw new NotImplementedException();
#else
							break;
#endif
					}
				}

				loadedGCodeFile.GCodeCommandQueue.Add(machineInstructionForLine);

				if (progressReporter != null && maxProgressReport.ElapsedMilliseconds > 200)
				{
					progressReporter(((double)lineIndex / crCount / 2, ""));

					if (cancellationToken.IsCancellationRequested)
					{
						return null;
					}

					maxProgressReport.Restart();
				}

				lineIndex++;
			}

			loadedGCodeFile.AnalyzeGCodeLines(cancellationToken, progressReporter);

			loadTime.Stop();
			Console.WriteLine("Time To Load Seconds: {0:0.00}".FormatWith(loadTime.Elapsed.TotalSeconds));

			return loadedGCodeFile;
		}

		private void AnalyzeGCodeLines(CancellationToken cancellationToken, ReportProgressRatio<(double ratio, string state)> progressReporter)
		{
			double feedRateMmPerMin = 0;
			Vector3 lastPrinterPosition = new Vector3();
			double lastEPosition = 0;

			Stopwatch maxProgressReport = new Stopwatch();
			maxProgressReport.Start();

			for (int lineIndex = 0; lineIndex < GCodeCommandQueue.Count; lineIndex++)
			{
				PrinterMachineInstruction instruction = GCodeCommandQueue[lineIndex];
				string line = instruction.Line;
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
					GetFirstNumberAfter("X", lineToParse, ref attemptedDestination.x);
					GetFirstNumberAfter("Y", lineToParse, ref attemptedDestination.y);
					GetFirstNumberAfter("Z", lineToParse, ref attemptedDestination.z);

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

				if (progressReporter != null && maxProgressReport.ElapsedMilliseconds > 200)
				{
					progressReporter((((double) lineIndex / GCodeCommandQueue.Count / 2) + .5, ""));
					if (cancellationToken.IsCancellationRequested)
					{
						return;
					}

					maxProgressReport.Restart();
				}
			}

			double accumulatedTime = 0;
			for (int i = GCodeCommandQueue.Count - 1; i >= 0; i--)
			{
				PrinterMachineInstruction line = GCodeCommandQueue[i];
				accumulatedTime += line.secondsThisLine;
				line.secondsToEndFromHere = (float)accumulatedTime;
			}
		}

		public Vector2 Center
		{
			get { return center; }
		}

		public override double PercentComplete(int instructionIndex)
		{
			if (GCodeCommandQueue.Count > 0)
			{
				return Math.Min(99.9, (double)instructionIndex / (double)GCodeCommandQueue.Count * 100);
			}

			return 100;
		}

		public override int GetInstructionIndexAtLayer(int layerIndex)
		{
			return IndexOfChangeInZ[layerIndex];
		}

		private List<int> IndexOfChangeInZ
		{
			get { return indexOfChangeInZ; }
		}

		public override int LayerCount
		{
			get { return indexOfChangeInZ.Count; }
		}

		private void ParseMLine(string lineString, PrinterMachineInstruction processingMachineState)
		{
			// take off any comments before we check its length
			int commentIndex = lineString.IndexOf(';');
			if (commentIndex != -1)
			{
				lineString = lineString.Substring(0, commentIndex);
			}

			string[] splitOnSpace = lineString.Split(' ');
			switch (splitOnSpace[0].Substring(1).Trim())
			{
				case "01":
					// show a message?
					break;

				case "6":
					// wait for tool to heat up (wait for condition?)
					break;

				case "101":
					// extrude on, forward
					break;

				case "18":
					// turn off stepers
					break;

				case "42":
					// Stop on material exhausted / Switch I/O pin
					break;

                case "72":
                    // makerbot, Play tone or song
                    break;

                case "73":
					// makerbot, Manually set build percentage
					break;

				case "82":
					// set extruder to absolute mode
					break;

                case "83":
                    //Set extruder to relative mode
                    break;

				case "84":
					// lineString = "M84     ; disable motors\r"
					break;

				case "92":
					// set steps per mm
					break;

				case "102":
					// extrude on reverse
					break;

				case "103":
					// extrude off
					break;

				case "104":
					// set extruder tempreature
					break;

				case "105":
					// M105 Custom code for temperature reading. (Not used)
					break;

				case "106":
					// turn fan on
					break;

				case "107":
					// turn fan off
					break;

				case "108":
					// set extruder speed
					break;

				case "109":
					// set heated platform temperature
					break;

				case "114":
					break;

				case "117":
					// in Marlin: Display Message
					break;

				case "126":
					// enable fan (makerbot)
					break;

				case "127":
					// disable fan (makerbot)
					break;

				case "132":
					// recall stored home offsets for axis xyzab
					break;

                case "133":
                    // MakerBot wait for toolhead to heat
                    break;

                case "134":
                    // MakerBot wait for platform to reach target temp
                    break;

                case "135":
                    // MakerBot change toolhead
                    break;

                case "140":
					// set bed temperature
					break;

				case "190":
					// wait for bed temperature to be reached
					break;

				case "200":
					// M200 sets the filament diameter.
					break;

				case "201":
					// set axis acceleration
					break;

				case "204": // - Set default acceleration
					break;

				case "207": // M207: calibrate z axis by detecting z max length
					break;

				case "208": // M208: set axis max travel
					break;

				case "209": // M209: enable automatic retract
					break;

				case "210": // Set homing rate
					break;

				case "226": // user request pause
					break;

				case "227": // Enable Automatic Reverse and Prime
					break;

				case "301":
					break;

				case "400": // Wait for current moves to finish
					break;

				case "565": // M565: Set Z probe offset
					break;
                case "1200"://M1200 Makerbot Fake gCode command for start build notification
                    break;
                case "1201"://M1201 Makerbot Fake gCode command for end build notification
                    break;
                case "1202"://M1202 Makerbot Fake gCode command for reset board
                    break;

                default:
                    break;
			}
		}

		private void ParseGLine(string lineString, PrinterMachineInstruction processingMachineState)
		{
			// take off any comments before we check its length
			int commentIndex = lineString.IndexOf(';');
			if (commentIndex != -1)
			{
				lineString = lineString.Substring(0, commentIndex);
			}

			string[] splitOnSpace = lineString.Split(' ');
			string onlyNumber = splitOnSpace[0].Substring(1).Trim();
			switch (onlyNumber)
			{
				case "0":
					goto case "1";

				case "4":
				case "04":
					// wait a given number of miliseconds
					break;

				case "1":
					// get the x y z to move to
					{
						double valueX = 0;
						if (GCodeFile.GetFirstNumberAfter("X", lineString, ref valueX))
						{
							processingMachineState.X = valueX;
						}
						double valueY = 0;
						if (GCodeFile.GetFirstNumberAfter("Y", lineString, ref valueY))
						{
							processingMachineState.Y = valueY;
						}
						double valueZ = 0;
						if (GCodeFile.GetFirstNumberAfter("Z", lineString, ref valueZ))
						{
							processingMachineState.Z = valueZ;
						}
						double valueE = 0;
						if (GCodeFile.GetFirstNumberAfter("E", lineString, ref valueE))
						{
							if (processingMachineState.movementType == PrinterMachineInstruction.MovementTypes.Absolute)
							{
								processingMachineState.EPosition = valueE + amountOfAccumulatedEWhileParsing;
							}
							else
							{
								processingMachineState.EPosition += valueE;
							}
						}
						double valueF = 0;
						if (GCodeFile.GetFirstNumberAfter("F", lineString, ref valueF))
						{
							processingMachineState.FeedRate = valueF;
						}
					}

					if (!gcodeHasExplicitLayerChangeInfo)
					{
						if (processingMachineState.Z != parsingLastZ || indexOfChangeInZ.Count == 0)
						{
							// if we changed z or there is a movement and we have never started a layer index
							indexOfChangeInZ.Add(GCodeCommandQueue.Count);
						}
					}
					parsingLastZ = processingMachineState.Position.z;
					break;

				case "10": // firmware retract
					break;

				case "11": // firmware unretract
					break;

				case "21":
					// set to metric
					break;

				case "28":
					// G28 	Return to home position (machine zero, aka machine reference point)
					break;

				case "29":
					// G29 Probe the z-bed in 3 places
					break;

				case "30":
					// G30 Probe z in current position
					break;

				case "90": // G90 is Absolute Distance Mode
					processingMachineState.movementType = PrinterMachineInstruction.MovementTypes.Absolute;
					break;

				case "91": // G91 is Incremental Distance Mode
					processingMachineState.movementType = PrinterMachineInstruction.MovementTypes.Relative;
					break;

				case "92":
					// set current head position values (used to reset origin)
					double ePosition = 0;
					if (GetFirstNumberAfter("E", lineString, ref ePosition))
					{
						// remember how much e position we just gave up
						amountOfAccumulatedEWhileParsing = (processingMachineState.EPosition - ePosition);
					}
					break;

                case "130":
                    //Set Digital Potentiometer value
                    break;

				case "161":
					// home x,y axis minimum
					break;

				case "162":
					// home z axis maximum
					break;

                default:
                    break;
			}
		}

		public override Vector2 GetWeightedCenter()
		{
			Vector2 total = new Vector2();
#if !MULTI_THREAD
            foreach (PrinterMachineInstruction state in GCodeCommandQueue)
            {
                total += new Vector2(state.Position.x, state.Position.y);
            }
#else
			Parallel.For<Vector2>(
				0,
				GCodeCommandQueue.Count,
				() => new Vector2(),
				(int index, ParallelLoopState loop, Vector2 subtotal) =>
				{
					PrinterMachineInstruction state = GCodeCommandQueue[index];
					subtotal += new Vector2(state.Position.x, state.Position.y);
					return subtotal;
				},
					(x) =>
					{
						total += new Vector2(x.x, x.y);
					}
			);
#endif

			return total / GCodeCommandQueue.Count;
		}

		public override RectangleDouble GetBounds()
		{
			RectangleDouble bounds = new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
#if !MULTI_THREAD
            foreach (PrinterMachineInstruction state in GCodeCommandQueue)
            {
                bounds.Left = Math.Min(state.Position.x, bounds.Left);
                bounds.Right = Math.Max(state.Position.x, bounds.Right);
                bounds.Bottom = Math.Min(state.Position.y, bounds.Bottom);
                bounds.Top = Math.Max(state.Position.y, bounds.Top);
            }
#else
			Parallel.For<RectangleDouble>(
				0,
				GCodeCommandQueue.Count,
				() => new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue),
				(int index, ParallelLoopState loop, RectangleDouble subtotal) =>
				{
					PrinterMachineInstruction state = GCodeCommandQueue[index];
					subtotal.Left = Math.Min(state.Position.x, subtotal.Left);
					subtotal.Right = Math.Max(state.Position.x, subtotal.Right);
					subtotal.Bottom = Math.Min(state.Position.y, subtotal.Bottom);
					subtotal.Top = Math.Max(state.Position.y, subtotal.Top);

					return subtotal;
				},
					(x) =>
					{
						bounds.Left = Math.Min(x.Left, bounds.Left);
						bounds.Right = Math.Max(x.Right, bounds.Right);
						bounds.Bottom = Math.Min(x.Bottom, bounds.Bottom);
						bounds.Top = Math.Max(x.Top, bounds.Top);
					}
			);
#endif
			return bounds;
		}

		public override bool IsExtruding(int instructionIndexToCheck)
		{
			if (instructionIndexToCheck > 1 && instructionIndexToCheck < GCodeCommandQueue.Count)
			{
				double extrusionLengeth = GCodeCommandQueue[instructionIndexToCheck].EPosition - GCodeCommandQueue[instructionIndexToCheck - 1].EPosition;
				if (extrusionLengeth > 0)
				{
					return true;
				}
			}

			return false;
		}

		public override double GetFilamentUsedMm(double filamentDiameter)
		{
			if (filamentUsedMmCache == 0 || filamentDiameter != diameterOfFilamentUsedMmCache)
			{
				double lastEPosition = 0;
				double filamentMm = 0;
				for (int i = 0; i < GCodeCommandQueue.Count; i++)
				{
					PrinterMachineInstruction instruction = GCodeCommandQueue[i];
					//filamentMm += instruction.EPosition;

					string lineToParse = instruction.Line;
					if (lineToParse.StartsWith("G0") || lineToParse.StartsWith("G1"))
					{
						double ePosition = lastEPosition;
						if (GetFirstNumberAfter("E", lineToParse, ref ePosition))
						{
							if (instruction.movementType == PrinterMachineInstruction.MovementTypes.Absolute)
							{
								double deltaEPosition = ePosition - lastEPosition;
								filamentMm += deltaEPosition;
							}
							else
							{
								filamentMm += ePosition;
							}

							lastEPosition = ePosition;
						}
					}
					else if (lineToParse.StartsWith("G92"))
					{
						double ePosition = 0;
						if (GetFirstNumberAfter("E", lineToParse, ref ePosition))
						{
							lastEPosition = ePosition;
						}
					}
				}

				filamentUsedMmCache = filamentMm;
                diameterOfFilamentUsedMmCache = filamentDiameter;
            }

			return filamentUsedMmCache;
		}

		public override double GetFilamentCubicMm(double filamentDiameterMm)
		{
			double filamentUsedMm = GetFilamentUsedMm(filamentDiameterMm);
			double filamentRadius = filamentDiameterMm / 2;
			double areaSquareMm = (filamentRadius * filamentRadius) * Math.PI;

			return areaSquareMm * filamentUsedMm;
		}

		public override double GetFilamentWeightGrams(double filamentDiameterMm, double densityGramsPerCubicCm)
		{
			double cubicMmPerCubicCm = 1000;
			double gramsPerCubicMm = densityGramsPerCubicCm / cubicMmPerCubicCm;
			double cubicMms = GetFilamentCubicMm(filamentDiameterMm);
			return cubicMms * gramsPerCubicMm;
		}

		public void Save(string dest)
		{
			using (StreamWriter file = new StreamWriter(dest))
			{
				foreach (PrinterMachineInstruction instruction in GCodeCommandQueue)
				{
					file.WriteLine(instruction.Line);
				}
			}
		}

		double filamentDiameterCache = 0;
		public override double GetFilamentDiameter()
		{
			if (filamentDiameterCache == 0)
			{
				for (int i = 0; i < Math.Min(20, GCodeCommandQueue.Count); i++)
				{
					if (GetFirstNumberAfter("filamentDiameter =", GCodeCommandQueue[i].Line, ref filamentDiameterCache))
					{
						break;
					}
				}

				if (filamentDiameterCache == 0)
				{
					// didn't find it, so look at the end of the file for filament_diameter =
					string lookFor = "; filament_diameter =";// 2.85
					for (int i = GCodeCommandQueue.Count - 1; i > Math.Max(0, GCodeCommandQueue.Count - 100); i--)
					{
						if (GetFirstNumberAfter(lookFor, GCodeCommandQueue[i].Line, ref filamentDiameterCache))
						{
						}
					}
				}

				if(filamentDiameterCache == 0)
				{
					// it is still 0 so set it to something so we render
					filamentDiameterCache = 1.75;
				}
			}

			return filamentDiameterCache;
		}

		public override double GetLayerHeight()
		{
			if (layerThickness > 0)
			{
				return layerThickness;
			}

			if (indexOfChangeInZ.Count > 2)
			{
				return GCodeCommandQueue[IndexOfChangeInZ[2]].Z - GCodeCommandQueue[IndexOfChangeInZ[1]].Z;
			}

			return .5;
		}

		public override double GetFirstLayerHeight()
		{
			if (firstLayerThickness > 0)
			{
				return firstLayerThickness;
			}

			if (indexOfChangeInZ.Count > 1)
			{
				return GCodeCommandQueue[IndexOfChangeInZ[1]].Z - GCodeCommandQueue[IndexOfChangeInZ[0]].Z;
			}

			return .5;
		}

		public override int GetLayerIndex(int instructionIndex)
		{
			if (instructionIndex >= 0
				&& instructionIndex < LineCount)
			{
				for (int zIndex = 0; zIndex < LayerCount; zIndex++)
				{
					if (instructionIndex < IndexOfChangeInZ[zIndex])
					{
						return zIndex;
					}
				}

				return LayerCount - 1;
			}

			return -1;
		}

		public override double Ratio0to1IntoContainedLayer(int instructionIndex)
		{
			int currentLayer = GetLayerIndex(instructionIndex);

			if (currentLayer > -1)
			{
				int startIndex = 0;
				if (currentLayer > 0)
				{
					startIndex = IndexOfChangeInZ[currentLayer - 1];
				}
				int endIndex = LineCount - 1;
				if (currentLayer < LayerCount - 1)
				{
					endIndex = IndexOfChangeInZ[currentLayer];
				}

				int deltaFromStart = Math.Max(0, instructionIndex - startIndex);
				return deltaFromStart / (double)(endIndex - startIndex);
			}

			return 0;
		}
	}
}