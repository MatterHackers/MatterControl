/*
Copyright (c) 2016, Kevin Pope, Lars Brubaker, John Lewin
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

using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class GCodeMaxLengthStreamTests
	{
		[Test, Category("GCodeStream")]
		public void MaxLengthStreamTests()
		{
			string[] lines = new string[]
			{
				"G1 X0 Y0 Z0 E0 F500",
				"M105",
				"G1 X18 Y0 Z0 F2500",
				"G28",
				"G1 X0 Y0 Z0 E0 F500",
				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				"G1 X0 Y0 Z0 E0 F500",
				"M105",
				"G1 X6 F2500",
				"G1 X12",
				"G1 X18",
				"G28",
				"G1 X12 F500",
				"G1 X6",
				"G1 X0",
				null,
			};

			MaxLengthStream maxLengthStream = new MaxLengthStream(new TestGCodeStream(lines), 6);

			int expectedIndex = 0;
			string actualLine = maxLengthStream.ReadLine();
			string expectedLine = expected[expectedIndex++];

			Assert.AreEqual(expectedLine, actualLine, "Unexpected response from MaxLengthStream"); 

			while (actualLine != null)
			{
				actualLine = maxLengthStream.ReadLine();
				expectedLine = expected[expectedIndex++];

				Assert.AreEqual(expectedLine, actualLine, "Unexpected response from MaxLengthStream");
			}
		}
	}

	[TestFixture]
	public class GCodePipeTests
	{
		public static GCodeStream CreateTestGCodeStream(string[] inputLines)
		{
			GCodeStream gCodeFileStream0 = new TestGCodeStream(inputLines);
			GCodeStream pauseHandlingStream1 = new PauseHandlingStream(gCodeFileStream0);
			GCodeStream queuedCommandStream2 = new QueuedCommandsStream(pauseHandlingStream1);
			GCodeStream relativeToAbsoluteStream3 = new RelativeToAbsoluteStream(queuedCommandStream2);
			//GCodeStream printLevelingStream4 = new PrintLevelingStream(relativeToAbsoluteStream3);
			GCodeStream printLevelingStream4 = relativeToAbsoluteStream3;
			GCodeStream waitForTempStream5 = new WaitForTempStream(printLevelingStream4);
			GCodeStream babyStepsStream6 = new BabyStepsStream(waitForTempStream5);
			GCodeStream extrusionMultiplyerStream7 = new ExtrusionMultiplyerStream(babyStepsStream6);
			GCodeStream feedrateMultiplyerStream8 = new FeedRateMultiplyerStream(extrusionMultiplyerStream7);
			GCodeStream totalGCodeStream = feedrateMultiplyerStream8;

			return totalGCodeStream;
		}

		[Test, Category("GCodeStream")]
		public void CorrectEOutputPositions()
		{
			string[] inputLines = new string[]
			{
				"G1 E11 F300",
				// BCN tool change test
				// Before:
				"G92 E0",
				"G91",
				"G1 E - 5 F302",
				"G90",
				// After:
				"G91",
				"G1 E8 F150",
				"G90",
				"G4 P0",
				"G92 E0",
				"G4 P0",
				"G91",
				"G1 E-2 F301",
				"G90",
				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				"G1 E11 F300",
				// before
				"G92 E0",
				"",
				"G1 E-5 F302",
				"G90",
				// after
				"", // G91 is removed
				"G1 E3 F150", // altered to be absolute
				"G90",
				"G4 P0",
				"G92 E0",
				"G4 P0",
				"", // G91 is removed
				"G1 E-2 F301",
				"G90",
				null,
			};

			GCodeStream testStream = CreateTestGCodeStream(inputLines);

			int expectedIndex = 0;
			string actualLine = testStream.ReadLine();
			string expectedLine = expected[expectedIndex++];

			Assert.AreEqual(expectedLine, actualLine, "Unexpected response from testStream");

			while (actualLine != null)
			{
				actualLine = testStream.ReadLine();
				if (actualLine == "G92 E0")
				{
					testStream.SetPrinterPosition(new PrinterMove(new Vector3(), 0, 300));
				}

				expectedLine = expected[expectedIndex++];

				Assert.AreEqual(expectedLine, actualLine, "Unexpected response from testStream");
			}
		}

		[Test, Category("GCodeStream")]
		public void CorrectZOutputPositions()
		{
			string[] inputLines = new string[]
			{
				"G1 Z-2 F300",
				"G92 Z0",
				"G1 Z5 F300",
				"G28",
				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				"G1 Z-1 F300",
				"G1 Z-2",
				"G92 Z0",
				"G1 Z1 F300",
				"G1 Z2",
				"G1 Z3",
				"G1 Z4",
				"G1 Z5",
				"G28",
				null,
			};

			GCodeStream testStream = CreateTestGCodeStream(inputLines);

			int expectedIndex = 0;
			string actualLine = testStream.ReadLine();
			string expectedLine = expected[expectedIndex++];

			Assert.AreEqual(expectedLine, actualLine, "Unexpected response from testStream");

			while (actualLine != null)
			{
				actualLine = testStream.ReadLine();
				if (actualLine == "G92 Z0")
				{
					testStream.SetPrinterPosition(new PrinterMove(new Vector3(), 0, 0));
				}

				expectedLine = expected[expectedIndex++];

				Assert.AreEqual(expectedLine, actualLine, "Unexpected response from testStream");
			}
		}
	}

[TestFixture]
	public class GCodePauseStreamTests
	{
		[Test, Category("GCodeStream")]
		public void PauseHandlingStreamTests()
		{
			string[] inputLines = new string[]
			{
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0",
				"G1 X10 Y10 Z10 E10",
				"G1 X10 Y10 Z10 E30",

				"; the printer pauses",
				"G91",
				"G1 Z10 E - 10 F12000",
				"G90",

				"; the user moves the printer",

				"; the printer un-pauses",
				"G91",
				"G1 Z-10 E10.8 F12000",
				"G90",
				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0",
				"G1 X10 Y10 Z10 E10",
				"G1 X10 Y10 Z10 E30",

				"; the printer pauses",
				"", // G91 is removed
				"G1 Z20 E20 F12000", // altered to be absolute
				"G90",

				"; the user moves the printer",

				"; the printer un-pauses",
				"", // G91 is removed
				"G1 Z10 E30.8",
				"G90",
				null,
			};

			PauseHandlingStream pauseHandlingStream = new PauseHandlingStream(new TestGCodeStream(inputLines));

			int expectedIndex = 0;
			string actualLine = pauseHandlingStream.ReadLine();
			string expectedLine = expected[expectedIndex++];

			Assert.AreEqual(expectedLine, actualLine, "Unexpected response from PauseHandlingStream");

			while (actualLine != null)
			{
				expectedLine = expected[expectedIndex++];
				actualLine = pauseHandlingStream.ReadLine();

				Assert.AreEqual(expectedLine, actualLine, "Unexpected response from PauseHandlingStream");
			}
		}
	}

	public class TestGCodeStream : GCodeStream
	{
		private int index = 0;
		private string[] lines;

		public TestGCodeStream(string[] lines)
		{
			this.lines = lines;
		}

		public override void Dispose()
		{
		}

		public override string ReadLine()
		{
			return lines[index++];
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
		}
	}
}