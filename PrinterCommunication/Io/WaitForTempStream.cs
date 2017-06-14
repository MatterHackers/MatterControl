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

using System;
using System.Diagnostics;
using System.Threading;
using MatterHackers.GCodeVisualizer;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class WaitForTempStream : GCodeStreamProxy
	{
		private double extruderIndex;
		private double ignoreRequestIfBelowTemp = 20;
		private double sameTempRange = 1;
		private State state = State.passthrough;
		private double targetTemp = 0;
		private Stopwatch timeHaveBeenAtTemp = new Stopwatch();
		private double waitAfterReachTempTime = 3;
		private bool waitWhenCooling = false;

		public WaitForTempStream(GCodeStream internalStream)
			: base(internalStream)
		{
			state = State.passthrough;
		}

		private enum State
		{ passthrough, waitingForExtruderTemp, waitingForBedTemp };

		public bool HeatingBed { get { return state == State.waitingForBedTemp; } }
		public bool HeatingExtruder { get { return state == State.waitingForExtruderTemp; } }

		public void Cancel()
		{
			state = State.passthrough;
		}

		public override string ReadLine()
		{
			switch (state)
			{
				case State.passthrough:
					{
						string lineToSend = base.ReadLine();

						if (lineToSend != null
							&& lineToSend.StartsWith("M"))
						{
							// initial test is just to see if it is an M109
							if (lineToSend.StartsWith("M109")) // extruder set and wait temp
							{
								if (lineToSend.Contains("F") // If it has a control character F (auto temp)
									|| !lineToSend.Contains("S")) // if it is a reset (has no S temperature)
								{
									// don't replace it
									return lineToSend;
								}

								// send an M104 instead
								waitWhenCooling = false;
								lineToSend = "M104" + lineToSend.Substring(4);
								GCodeFile.GetFirstNumberAfter("S", lineToSend, ref targetTemp);
								extruderIndex = 0;
								GCodeFile.GetFirstNumberAfter("T", lineToSend, ref extruderIndex);
								if (targetTemp > ignoreRequestIfBelowTemp)
								{
									state = State.waitingForExtruderTemp;
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
										state = State.waitingForBedTemp;
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

				case State.waitingForExtruderTemp:
					{
						double extruderTemp = PrinterConnection.Instance.GetActualExtruderTemperature((int)extruderIndex);
						bool tempWithinRange = extruderTemp >= targetTemp - sameTempRange && extruderTemp <= targetTemp + sameTempRange;
						if (tempWithinRange && !timeHaveBeenAtTemp.IsRunning)
						{
							timeHaveBeenAtTemp.Start();
						}

						if (timeHaveBeenAtTemp.Elapsed.TotalSeconds > waitAfterReachTempTime
							|| PrinterConnection.Instance.PrintWasCanceled)
						{
							// switch to pass through and continue
							state = State.passthrough;
							return "";
						}
						else
						{
							// send a wait command
							Thread.Sleep(100); // sleep .1 second while waiting for temp
							return ""; // return nothing until we reach temp
						}
					}

				case State.waitingForBedTemp:
					{
						double bedTemp = PrinterConnection.Instance.ActualBedTemperature;
						bool tempWithinRange;
						if (waitWhenCooling)
						{
							tempWithinRange = bedTemp >= targetTemp - sameTempRange && bedTemp <= targetTemp + sameTempRange;
						}
						else
						{
							tempWithinRange = bedTemp >= targetTemp - sameTempRange;
						}

						// Added R code for M190
						if (tempWithinRange && !timeHaveBeenAtTemp.IsRunning)
						{
							timeHaveBeenAtTemp.Start();
						}

						if (timeHaveBeenAtTemp.Elapsed.TotalSeconds > waitAfterReachTempTime
							|| PrinterConnection.Instance.PrintWasCanceled)
						{
							// switch to pass through and continue
							state = State.passthrough;
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