﻿/*
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
using System.Text;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class OffsetStream : GCodeStreamProxy
	{
		private PrinterMove lastDestination = new PrinterMove();

		public OffsetStream(GCodeStream internalStream, Vector3 offset)
			: base(internalStream)
		{
			this.Offset = offset;
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			lastDestination = position;
			lastDestination.position -= Offset;
			internalStream.SetPrinterPosition(lastDestination);
		}

		public Vector3 Offset { get; set; }

		public override string ReadLine()
		{
			string lineToSend = base.ReadLine();

			if (lineToSend != null
				&& LineIsMovement(lineToSend))
			{
				PrinterMove currentMove = GetPosition(lineToSend, lastDestination);

				PrinterMove moveToSend = currentMove;
				moveToSend.position += Offset;

				lineToSend = CreateMovementLine(moveToSend, lastDestination);
				lastDestination = currentMove;
				return lineToSend;
			}

			return lineToSend;
		}
	}
}