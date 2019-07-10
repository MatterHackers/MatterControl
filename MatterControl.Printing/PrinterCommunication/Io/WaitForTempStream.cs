/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using System.Diagnostics;
using System.Threading;

namespace MatterControl.Printing.Pipelines
{
	public class WaitForTempStream : GCodeStreamProxy
	{
		/// <summary>
		/// The number of seconds to wait after reaching the target temp before continuing. Analogous to 
		/// firmware dwell time for temperature stabilization
		/// </summary>
		public static double WaitAfterReachTempTime { get; set; } = 3;

		private double extruderIndex;
		private double ignoreRequestIfBelowTemp = 20;
		private double sameTempRangeBed = 3;
		private double sameTempRangeHotend = 1;
		private State state = State.Passthrough;
		private double targetTemp = 0;
		private Stopwatch timeHaveBeenAtTemp = new Stopwatch();

		private bool waitWhenCooling = false;

		public WaitForTempStream(PrintHostConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
			state = State.Passthrough;
		}

		private enum State
		{
			Passthrough,
			WaitingForBedTemp,
			WaitingForT0Temp,
			WaitingForT1Temp
		};

		public bool HeatingBed { get { return state == State.WaitingForBedTemp; } }

		public bool HeatingT0 { get { return state == State.WaitingForT0Temp; } }

		public bool HeatingT1 { get { return state == State.WaitingForT1Temp; } }

		public override string DebugInfo => "";

		public void Cancel()
		{
			state = State.Passthrough;
		}

		public override string ReadLine()
		{
			switch (state)
			{
				case State.Passthrough:
					{
						string lineToSend = base.ReadLine();

						if(lineToSend == null)
						{
							return null;
						}

						if (lineToSend.EndsWith("; NO_PROCESSING"))
						{
							return lineToSend;
						}

						if (lineToSend.StartsWith("M"))
						{
							// initial test is just to see if it is an M109
							if (lineToSend.StartsWith("M109")) // extruder set and wait temp
							{
								var lineNoComment = lineToSend.Split(';')[0];

								if (lineNoComment.Contains("F") // If it has a control character F (auto temp)
									|| !lineNoComment.Contains("S")) // if it is a reset (has no S temperature)
								{
									// don't replace it
									return lineToSend;
								}

								// send an M104 instead
								waitWhenCooling = false;
								lineToSend = "M104" + lineToSend.Substring(4);
								GCodeFile.GetFirstNumberAfter("S", lineToSend, ref targetTemp);
								extruderIndex = printer.Connection.ActiveExtruderIndex;
								GCodeFile.GetFirstNumberAfter("T", lineToSend, ref extruderIndex);
								if (targetTemp > ignoreRequestIfBelowTemp)
								{
									if (extruderIndex == 1)
									{
										state = State.WaitingForT1Temp;
									}
									else
									{
										state = State.WaitingForT0Temp;
									}
									timeHaveBeenAtTemp.Reset();
								}
								else
								{
									Thread.Sleep(100); // sleep .1 second while waiting for temp
									return ""; // return nothing until we reach temp
								}
							}
							else if (lineToSend.StartsWith("M190")) // bed set and wait temp
							{
								// send an M140 instead
								bool gotR = GCodeFile.GetFirstNumberAfter("R", lineToSend, ref targetTemp);
								bool gotS = GCodeFile.GetFirstNumberAfter("S", lineToSend, ref targetTemp);
								if (gotR || gotS)
								{
									if (targetTemp > ignoreRequestIfBelowTemp)
									{
										waitWhenCooling = gotR;
										lineToSend = "M140 S" + targetTemp.ToString();
										state = State.WaitingForBedTemp;
										timeHaveBeenAtTemp.Reset();
									}
									else
									{
										Thread.Sleep(100); // sleep .1 second while waiting for temp
										return ""; // return nothing until we reach temp
									}
								}
								else
								{
									Thread.Sleep(100); // sleep .1 second while waiting for temp
									return ""; // return nothing until we reach temp
								}
							}
						}

						return lineToSend;
					}

				case State.WaitingForT0Temp:
				case State.WaitingForT1Temp:
					{
						double extruderTemp = printer.Connection.GetActualHotendTemperature((int)extruderIndex);
						bool tempWithinRange = extruderTemp >= targetTemp - sameTempRangeHotend 
							&& extruderTemp <= targetTemp + sameTempRangeHotend;
						if (tempWithinRange && !timeHaveBeenAtTemp.IsRunning)
						{
							timeHaveBeenAtTemp.Start();
						}

						if (timeHaveBeenAtTemp.Elapsed.TotalSeconds > WaitAfterReachTempTime
							|| printer.Connection.PrintWasCanceled)
						{
							// switch to pass through and continue
							state = State.Passthrough;
							return "";
						}
						else
						{
							// send a wait command
							Thread.Sleep(100); // sleep .1 second while waiting for temp
							return ""; // return nothing until we reach temp
						}
					}

				case State.WaitingForBedTemp:
					{
						double bedTemp = printer.Connection.ActualBedTemperature;
						bool tempWithinRange;
						if (waitWhenCooling)
						{
							tempWithinRange = bedTemp >= targetTemp - sameTempRangeBed 
								&& bedTemp <= targetTemp + sameTempRangeBed;
						}
						else
						{
							tempWithinRange = bedTemp >= targetTemp - sameTempRangeBed;
						}

						// Added R code for M190
						if (tempWithinRange && !timeHaveBeenAtTemp.IsRunning)
						{
							timeHaveBeenAtTemp.Start();
						}

						if (timeHaveBeenAtTemp.Elapsed.TotalSeconds > WaitAfterReachTempTime
							|| printer.Connection.PrintWasCanceled)
						{
							// switch to pass through and continue
							state = State.Passthrough;
							return "";
						}
						else
						{
							// send a wait command
							Thread.Sleep(100); // sleep .1 second while waiting for temp
							return ""; // return nothing until we reach temp
						}
					}
			}

			return null;
		}
	}
}