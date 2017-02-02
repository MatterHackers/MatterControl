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
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class BabyStepsStream : GCodeStreamProxy
	{
		private int layerCount = -1;
		private MaxLengthStream maxLengthStream;
		private OffsetStream offsetStream;
		private EventHandler unregisterEvents;

		public BabyStepsStream(GCodeStream internalStream)
			: base(null)
		{
			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if ((e as StringEventArgs)?.Data == SettingsKey.baby_step_z_offset)
				{
					OffsetChanged();
				}

			}, ref unregisterEvents);

			maxLengthStream = new MaxLengthStream(internalStream, 1);
			offsetStream = new OffsetStream(maxLengthStream, new Vector3(0, 0, ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.baby_step_z_offset)));
			base.internalStream = offsetStream;
		}

		public Vector3 Offset { get { return offsetStream.Offset; } set { offsetStream.Offset = value; } }

		public override void Dispose()
		{
			offsetStream.Dispose();
			maxLengthStream.Dispose();
			unregisterEvents?.Invoke(this, null);
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

		private void OffsetChanged()
		{
			offsetStream.Offset = new Vector3(0, 0, ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.baby_step_z_offset));
		}
	}
}