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
using MatterHackers.Agg;
using MatterHackers.GCodeVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class TrackPrinterPosition : GCodeFileProxy
	{
        public RootedObjectEventHandler DestinationChanged = new RootedObjectEventHandler();

        private Vector3 currentDestination;

        private double currentExtruderDestination;

        public Vector3 CurrentDestination { get { return currentDestination; } }

        public TrackPrinterPosition(GCodeFile source)
            : base(source)
		{
		}

        private string KeepTrackOfPostionAndDestination(string lineBeingSent)
        {
            if (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 "))
            {
                Vector3 newDestination = currentDestination;
                if (PrinterConnectionAndCommunication.Instance.MovementMode == PrinterMachineInstruction.MovementTypes.Relative)
                {
                    newDestination = Vector3.Zero;
                }

                GCodeFile.GetFirstNumberAfter("X", lineBeingSent, out newDestination.x);
                GCodeFile.GetFirstNumberAfter("Y", lineBeingSent, out newDestination.y);
                GCodeFile.GetFirstNumberAfter("Z", lineBeingSent, out newDestination.z);

                GCodeFile.GetFirstNumberAfter("E", lineBeingSent, out currentExtruderDestination);
                GCodeFile.GetFirstNumberAfter("F", lineBeingSent, out currentFeedRate);

                if (PrinterConnectionAndCommunication.Instance.MovementMode == PrinterMachineInstruction.MovementTypes.Relative)
                {
                    newDestination += currentDestination;
                }

                if (currentDestination != newDestination)
                {
                    currentDestination = newDestination;
                    DestinationChanged.CallEvents(this, null);
                }
            }

            return lineBeingSent;
        }
    }
}