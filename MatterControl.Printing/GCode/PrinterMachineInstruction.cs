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

using MatterHackers.VectorMath;
using System;
using System.Text;

namespace MatterControl.Printing
{
	[Flags]
	public enum PositionSet
	{
		None = 0,
		X = 2,
		Y = 4,
		Z = 8,
		E = 16,
	}

	public class PrinterMachineInstruction
	{
		public byte[] byteLine;

		// Absolute is the RepRap default
		public MovementTypes MovementType = MovementTypes.Absolute;

		public float SecondsThisLine;

		public float SecondsToEndFromHere;

		public PositionSet PositionSet;

		private Vector3Float xyzPosition = new Vector3Float();

		public PrinterMachineInstruction(string Line)
		{
			this.Line = Line;
		}

		public PrinterMachineInstruction(string Line, PrinterMachineInstruction copy, bool clientInsertion = false)
			: this(Line)
		{
			xyzPosition = copy.xyzPosition;
			FeedRate = copy.FeedRate;
			EPosition = copy.EPosition;
			MovementType = copy.MovementType;
			SecondsToEndFromHere = copy.SecondsToEndFromHere;
			ToolIndex = copy.ToolIndex;
		}

		public enum MovementTypes { Absolute, Relative };

		public float EPosition { get; set; }

		public int ToolIndex { get; set; }

		public float FeedRate { get; set; }

		public string Line
		{
			get
			{
				return Encoding.Default.GetString(byteLine);
			}

			set
			{
				byteLine = Encoding.Default.GetBytes(value);
			}
		}

		public Vector3 Position
		{
			get { return new Vector3(xyzPosition); }
			set
			{
				xyzPosition.X = (float)value.X;
				xyzPosition.Y = (float)value.Y;
				xyzPosition.Z = (float)value.Z;
			}
		}

		public double X
		{
			get { return xyzPosition.X; }
			set
			{
				if (MovementType == MovementTypes.Absolute)
				{
					xyzPosition.X = (float)value;
				}
				else
				{
					xyzPosition.X += (float)value;
				}
			}
		}

		public double Y
		{
			get { return xyzPosition.Y; }
			set
			{
				if (MovementType == MovementTypes.Absolute)
				{
					xyzPosition.Y = (float)value;
				}
				else
				{
					xyzPosition.Y += (float)value;
				}
			}
		}

		public double Z
		{
			get { return xyzPosition.Z; }
			set
			{
				if (MovementType == MovementTypes.Absolute)
				{
					xyzPosition.Z = (float)value;
				}
				else
				{
					xyzPosition.Z += (float)value;
				}
			}
		}
	}
}