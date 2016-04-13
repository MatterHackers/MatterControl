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
using MatterHackers.VectorMath;
using System.Diagnostics;

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

        public WaitForTempStream(GCodeStream internalStream)
            : base(internalStream)
        {
        }

        private enum State
        { passthrough, waitingForExtruderTemp, waitingForBedTemp };

        public bool HeatingBed { get { return state == State.waitingForBedTemp; } }
        public bool HeatingExtruder { get { return state == State.waitingForExtruderTemp; } }

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
                            if (lineToSend.StartsWith("M109")) // extruder set and wait temp
                            {
                                // send an M104 instead
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
                                    return "G4 P1000"; // 1 second
                                }
                            }
                            else if (lineToSend.StartsWith("M190")) // bed set and wait temp
                            {
                                // send an M140 instead
                                GCodeFile.GetFirstNumberAfter("S", lineToSend, ref targetTemp);
                                lineToSend = "M140" + lineToSend.Substring(4);
                                if (targetTemp > ignoreRequestIfBelowTemp)
                                {
                                    state = State.waitingForBedTemp;
                                    timeHaveBeenAtTemp.Reset();
                                }
                                else
                                {
                                    return "G4 P1000"; // 1 second
                                }
                            }
                        }

                        return lineToSend;
                    }

                case State.waitingForExtruderTemp:
                    {
                        double extruderTemp = PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature((int)extruderIndex);
                        bool tempWithinRange = extruderTemp >= targetTemp - sameTempRange && extruderTemp <= targetTemp + sameTempRange;
                        if (tempWithinRange && !timeHaveBeenAtTemp.IsRunning)
                        {
                            timeHaveBeenAtTemp.Start();
                        }

                        if (timeHaveBeenAtTemp.Elapsed.TotalSeconds > waitAfterReachTempTime
                            || PrinterConnectionAndCommunication.Instance.PrintWasCanceled)
                        {
                            // switch to pass through and continue
                            state = State.passthrough;
                            return base.ReadLine();
                        }
                        else
                        {
                            // send a wait command
                            return "G4 P1000"; // 1 second
                        }
                    }

                case State.waitingForBedTemp:
                    {
                        double bedTemp = PrinterConnectionAndCommunication.Instance.ActualBedTemperature;
                        bool tempWithinRange = bedTemp >= targetTemp - sameTempRange;
                        if (tempWithinRange && !timeHaveBeenAtTemp.IsRunning)
                        {
                            timeHaveBeenAtTemp.Start();
                        }

                        if (timeHaveBeenAtTemp.Elapsed.TotalSeconds > waitAfterReachTempTime
                            || PrinterConnectionAndCommunication.Instance.PrintWasCanceled)
                        {
                            // switch to pass through and continue
                            state = State.passthrough;
                            return base.ReadLine();
                        }
                        else
                        {
                            // send a wait command
                            return "G4 P1000"; // 1 second
                        }
                    }
            }

            return null;
        }
    }
}
