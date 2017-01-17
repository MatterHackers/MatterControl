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
using System.Text;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class BabyStepsStream : GCodeStreamProxy
	{
		OffsetStream offsetStream;
		MaxLengthStream maxLengthStream;
		int layerCount = -1;

		public Vector3 Offset { get { return offsetStream.Offset; } set { offsetStream.Offset = value; } }

		public override void Dispose()
		{
			offsetStream.Dispose();
			maxLengthStream.Dispose();
		}

		public void OffsetAxis(PrinterConnectionAndCommunication.Axis moveAxis, double moveAmount)
		{
			offsetStream.Offset = offsetStream.Offset + new Vector3(
				(moveAxis == PrinterConnectionAndCommunication.Axis.X) ? moveAmount : 0,
				(moveAxis == PrinterConnectionAndCommunication.Axis.Y) ? moveAmount : 0,
				(moveAxis == PrinterConnectionAndCommunication.Axis.Z) ? moveAmount : 0);

			if(PrinterConnectionAndCommunication.Instance.CurrentlyPrintingLayer <= 1)
			{
				// store the offset
				ActiveSliceSettings.Instance.SetValue(SettingsKey.baby_step_z_offset, offsetStream.Offset.z.ToString("0.##"));
			}
		}

		public BabyStepsStream(GCodeStream internalStream)
			: base(null)
		{
			maxLengthStream = new MaxLengthStream(internalStream, 1);
			double zOffset = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.baby_step_z_offset);
			var layerZOffset = new Vector3(0, 0, zOffset);
			offsetStream = new OffsetStream(maxLengthStream, layerZOffset);
			base.internalStream = offsetStream;
		}

		public override string ReadLine()
		{
			string processedLine = offsetStream.ReadLine();
			if (processedLine != null
				&& layerCount < 1
				&& GCodeFile.IsLayerChange(processedLine))
			{
				layerCount++;
				if (layerCount == 1)
				{
					maxLengthStream.MaxSegmentLength = 5;
				}
			}
			return processedLine;
		}
	}
}