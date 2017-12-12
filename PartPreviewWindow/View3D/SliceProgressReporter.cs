/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.GCodeVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SliceProgressReporter : IProgress<ProgressStatus>
	{
		private double currentValue = 0;
		private double destValue = 10;
		private string lastOutputLine = "";
		private IProgress<ProgressStatus> parentProgress;
		private PrinterConfig printer;

		public SliceProgressReporter(IProgress<ProgressStatus> progressStatus, PrinterConfig printer)
		{
			this.parentProgress = progressStatus;
			this.printer = printer;
		}

		private Stopwatch timer = Stopwatch.StartNew();

		private string progressSection = "";

		public void Report(ProgressStatus progressStatus)
		{
			bool foundProgressNumbers = false;

			string value = progressStatus.Status;

			if (GCodeFile.GetFirstNumberAfter("", value, ref currentValue)
				&& GCodeFile.GetFirstNumberAfter("/", value, ref destValue))
			{
				if (destValue == 0)
				{
					destValue = 1;
				}

				foundProgressNumbers = true;

				int pos = value.IndexOf(currentValue.ToString());
				if (pos != -1)
				{
					progressSection = value.Substring(0, pos);
				}
				else
				{
					progressSection = value;
				}

				timer.Restart();

				progressStatus.Status = progressSection;
				progressStatus.Progress0To1 = 0;
			}
			else
			{
				printer.Connection.TerminalLog.WriteLine(value);
			}

			int lengthBeforeNumber = value.IndexOfAny("0123456789".ToCharArray()) - 1;
			lengthBeforeNumber = lengthBeforeNumber < 0 ? lengthBeforeNumber = value.Length : lengthBeforeNumber;
			if (lastOutputLine != value.Substring(0, lengthBeforeNumber))
			{
				lastOutputLine = value.Substring(0, lengthBeforeNumber);
			}

			if (foundProgressNumbers)
			{
				double complete = currentValue / destValue;
				progressStatus.Progress0To1 = complete;
			}

			parentProgress.Report(progressStatus);
		}
	}
}