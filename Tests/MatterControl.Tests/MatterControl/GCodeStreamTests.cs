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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.Library.Export;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.PrinterEmulator;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, RunInApplicationDomain]
	public class GCodeStreamTests
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
				"G1 X0 Y0 Z0 E0 F500",
				null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			PrinterConfig printer = null;

			MaxLengthStream maxLengthStream = new MaxLengthStream(printer, new TestGCodeStream(printer, lines), 6);
			ValidateStreamResponse(expected, maxLengthStream);
		}

		[Test]
		public void ExportStreamG30Tests()
		{
			string[] inputLines = new string[]
			{
				"M117 Starting Print",
				"M104 S0",
				"; comment line",
				"G28 ; home all axes",
				"G0 Z10 F1800",
				"G0 Z11 F1800",
				"G0 X1Y0Z9 F1800",
				"G0 Z10 F1801",
				"G30 Z0",
				"M114",
				"G0 Z10 F1800",
				"M114",
				"M109 S[temperature]",
				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				"M117 Starting Print",
				"M104 S0",
				"; comment line",
				"G28 ; home all axes",
				"G1 Z10 F1800",
				"G1 Z11",
				"G1 X1 Y0 Z9",
				"G1 Z10 F1801",
				"G30 Z0",
				"M114",
				"G1 Z10 F1800",
				"M114",
				"M109 S[temperature]",
				 null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());

			var testStream = GCodeExport.GetExportStream(printer, new TestGCodeStream(printer, inputLines), true);
			ValidateStreamResponse(expected, testStream);
		}

		[Test]
		public void SmoothieRewriteTest()
		{
			string[] inputLines = new string[]
			{
				"G28",
				"M119",
				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				"G28",
				"M280 P0 S10.6",
				"G4 P400",
				"M280 P0 S7",
				"G4 P400",
				"M117 Ready ",
				"M119",
				"switch filament; WRITE_RAW",
				 null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());

			var write_filter = "\"^(G28)\", \"G28,M280 P0 S10.6,G4 P400,M280 P0 S7,G4 P400,M117 Ready \"";
			write_filter += "\\n\"^(M119)\", \"M119,switch filament; WRITE_RAW\"";
			printer.Settings.SetValue(SettingsKey.write_regex, write_filter);

			var testStream = GCodeExport.GetExportStream(printer, new TestGCodeStream(printer, inputLines), true);
			ValidateStreamResponse(expected, testStream);
		}

		[Test]
		public void LineCuttingOffWhenNoLevelingTest()
		{
			string[] inputLines = new string[]
			{
				"G1 X0Y0Z0E0 F1000",
				"G1 X10 Y0 Z0 F1000",
				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				"G1 X0 Y0 Z0 E0 F1000",
				"G1 X10",
				 null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());

			printer.Settings.SetValue(SettingsKey.has_hardware_leveling, "1");

			var testStream = GCodeExport.GetExportStream(printer, new TestGCodeStream(printer, inputLines), true);
			ValidateStreamResponse(expected, testStream);
		}

		[Test]
		public void LineCuttingOnWhenLevelingOnTest()
		{
			string[] inputLines = new string[]
			{
				"G1 X0Y0Z0E0F1000",
				"G1 X0Y0Z0E1F1000",
				"G1 X10 Y0 Z0 F1000",
				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				"; Software Leveling Applied",
				"G1 X0 Y0 Z-0.1 E0 F1000",
				"G1 E1",
				"G1 X1 Y0 Z-0.1",
				"G1 X2 Y0 Z-0.1",
				"G1 X3 Y0 Z-0.1",
				"G1 X4 Y0 Z-0.1",
				"G1 X5 Y0 Z-0.1",
				"G1 X6 Y0 Z-0.1",
				"G1 X7 Y0 Z-0.1",
				"G1 X8 Y0 Z-0.1",
				"G1 X9 Y0 Z-0.1",
				"G1 X10 Y0 Z-0.1",
				 null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());

			printer.Settings.SetValue(SettingsKey.print_leveling_enabled, "1");

			var testStream = GCodeExport.GetExportStream(printer, new TestGCodeStream(printer, inputLines), true);
			ValidateStreamResponse(expected, testStream);
		}

		public static GCodeStream CreateTestGCodeStream(PrinterConfig printer, string[] inputLines, out List<GCodeStream> streamList)
		{
			streamList = new List<GCodeStream>();
			streamList.Add(new TestGCodeStream(printer, inputLines));
			streamList.Add(new PauseHandlingStream(printer, streamList[streamList.Count - 1]));
			streamList.Add(new QueuedCommandsStream(printer, streamList[streamList.Count - 1]));
			streamList.Add(new RelativeToAbsoluteStream(printer, streamList[streamList.Count - 1]));
			streamList.Add(new WaitForTempStream(printer, streamList[streamList.Count - 1]));
			streamList.Add(new BabyStepsStream(printer, streamList[streamList.Count - 1]));
			streamList.Add(new MaxLengthStream(printer, streamList[streamList.Count - 1], 1));
			streamList.Add(new ExtrusionMultiplyerStream(printer, streamList[streamList.Count - 1]));
			streamList.Add(new FeedRateMultiplyerStream(printer, streamList[streamList.Count - 1]));
			GCodeStream totalGCodeStream = streamList[streamList.Count - 1];

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
				"G92 E0",
				"",
				"G1 E-1 F302",
				"G1 E-2",
				"G1 E-3",
				"G1 E-4",
				"G1 E-5",
				"G90",
				"",
				"G1 E-4 F150",
				"G1 E-3",
				"G1 E-2",
				"G1 E-1",
				"G1 E0",
				"G1 E1",
				"G1 E2",
				"G1 E3",
				"G90",
				"G4 P0",
				"G92 E0",
				"G4 P0",
				"",
				"G1 E-1 F301",
				"G1 E-2",
				"G90",
				 null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());

			GCodeStream testStream = CreateTestGCodeStream(printer, inputLines, out List<GCodeStream> streamList);
			ValidateStreamResponse(expected, testStream);
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
				"G1 Z-2 F300",
				"G92 Z0",
				"G1 Z1 F300",
				"G1 Z2",
				"G1 Z3",
				"G1 Z4",
				"G1 Z5",
				"G28",
				null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());
			GCodeStream testStream = CreateTestGCodeStream(printer, inputLines, out List<GCodeStream> streamList);
			ValidateStreamResponse(expected, testStream);
		}

		[Test, Category("GCodeStream")]
		public void PauseHandlingStreamTests()
		{
			int readX = 50;
			// Validate that the number parsing code is working as expected, specifically ignoring data that appears in comments
			// This is a regression that we saw in the Lulzbot Mini profile after adding macro processing.
			GCodeFile.GetFirstNumberAfter("X", "G1 Z10 E - 10 F12000 ; suck up XXmm of filament", ref readX);
			// did not change
			Assert.AreEqual(50, readX, "Don't change the x if it is after a comment");

			// a comments that looks more like a valid line
			GCodeFile.GetFirstNumberAfter("X", "G1 Z10 E - 10 F12000 ; X33", ref readX);
			// did not change
			Assert.AreEqual(50, readX, "Don't change the x if it is after a comment");
			// a line that should parse
			GCodeFile.GetFirstNumberAfter("X", "G1 Z10 E - 10 F12000 X33", ref readX);
			// did change
			Assert.AreEqual(33, readX, "not in a comment, do a change");

			string[] inputLines = new string[]
			{
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0",
				"G1 X10 Y10 Z10 E10",
				"G1 X10 Y10 Z10 E30",

				"; the printer pauses",
				"G91",
				"G1 Z10 E - 10 F12000 ; suck up XXmm of filament",
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
				"G1 E10",
				"G1 E30",

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

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());
			GCodeStream pauseHandlingStream = CreateTestGCodeStream(printer, inputLines, out List<GCodeStream> streamList);
			ValidateStreamResponse(expected, pauseHandlingStream);
		}

		[Test, Category("GCodeStream"), Ignore("WIP")]
		public void SoftwareEndstopstreamTests()
		{
			string[] inputLines = new string[]
			{
				// test x min
				// move without extrusion
				"G1 X100Y100Z0E0", // start at the bed center
				"G1 X-100", // move left off the bed
				"G1 Y110", // move while outside bounds
				"G1 X100", // move back on

				// move with extrusion
				"G1 X100Y100Z0E0", // start at the bed center
				"G1 X-100E10", // move left off the bed
				"G1 Y110E20", // move while outside bounds
				"G1 X100E30", // move back on

				// test x max
				// test y min
				// test y max
				// test z min
				// test z max

				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				// move without extrusion
				"G1 X100 Y100 Z0 E0", // strat position
				"G1 X0", // clamped x
				"", // move while outside
				"G1 Y110", // first position back in bounds
				"G1 X100", // move to requested x

				// move with extrusion
				"G1 X100Y100Z0E0", // start at the bed center
				"G1 X-100E10", // move left off the bed
				"G1 Y110E20", // move while outside bounds
				"G1 X100E30", // move back on

				null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());
			var pauseHandlingStream = new SoftwareEndstopsStream(printer, new TestGCodeStream(printer, inputLines));
			ValidateStreamResponse(expected, pauseHandlingStream);
		}

		[Test, Category("GCodeStream")]
		public void MorePauseHandlingStreamTests()
		{
			string[] inputLines = new string[]
			{
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0",
				"G1 X11 Y10 Z10 E10",
				"G1 X12 Y10 Z10 E30",

				"; the printer pauses",
				"@pause",

				"; do_resume", // just a marker for us to issue a resume

				// move some more
				"G1 X13 Y10 Z10 E40",
				null,
			};

			// We should go back to the above code when possible. It requires making pause part and move while paused part of the stream.
			// All communication should go through stream to minimize the difference between printing and controlling while not printing (all printing in essence).
			string[] expected = new string[]
			{
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0",
				"G1 X11 E10",
				"G1 X12 E30",
				"; the printer pauses",
				"",
				"",
				"G1 Z20 E20 F12000",
				"G90",
				"M114",
				"",
				"; do_resume",
				"G92 E-10",
				"G1 Z16.67 F3001",
				"G1 X12.01 Y10.01 Z13.34",
				"G1 Z10.01",
				"G1 X12 Y10 Z10 F3000",
				"",
				"G1 Z0 E30.8 F12000",
				"G90",
				"M114",
				"",
				"G1 X12.1 F1800",
				"G1 X12.2",
				"G90",
				"G1 X12.33 Z1.667 E32.333",
				"G1 X12.47 Z3.333 E33.867",
				"G1 X12.6 Z5 E35.4",
				"G1 X12.73 Z6.667 E36.933",
				"G1 X12.87 Z8.333 E38.467",
				"G1 X13 Z10 E40",
				null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// this is the pause and resume from the Eris
			var printer = new PrinterConfig(new PrinterSettings());

			printer.Settings.SetValue(SettingsKey.pause_gcode, "G91\nG1 Z10 E - 10 F12000\n  G90");
			printer.Settings.SetValue(SettingsKey.resume_gcode, "G91\nG1 Z-10 E10.8 F12000\nG90");

			GCodeStream pauseHandlingStream = CreateTestGCodeStream(printer, inputLines, out List<GCodeStream> streamList);
			ValidateStreamResponse(expected, pauseHandlingStream, streamList);
		}

		[Test, Category("GCodeStream")]
		public Task ToolChangeNoHeatNoExtrusion()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// send some movement comands with tool switching
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0 F2500",
				"T1",
				"G1 X10 Y10 Z10 E0",
				"T0",
				"G1 X10 Y10 Z10 E0",
				// now do the same thing with a long enough print to cause
				// cooling and heating
				null,
			};

			string[] expected = new string[]
			{
				"M114",
				"T0",
				"M114",
				"G1 X10 Y10 Z10 F2500",
				"G1 Y111",
				"T1",
				"M114", // after a tool change we inject an M114
				"G1 Y220",
				"G1 X9 Y8 F3000",
				"G1 Z7 F315",
				"G1 F2500", // 11
				"G1",
				"G1 X332",
				"T0", // 14
				"M114",
				"G1 X444",
				"G1 X10 Y10 F3000",
				"G1 Z10 F315",
				"G1 F2500",
				"G1",
				"Communication State: FinishedPrint",
				null,
			};

			PrinterConfig printer = SetupToolChangeSettings();
			ValidateStreamResponseWhilePrintingAsync(expected, printer, inputLines).GetAwaiter().GetResult();
			return Task.CompletedTask;
		}

		[Test, Category("GCodeStream")]
		public Task ToolChangeHeatNoExtrusion()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// tell the printer to heat up
				"M104 T1 S240", // start with T0 to test smoothie temp change code
				"M104 T0 S230",
				// send some movement comands with tool switching
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0 F2500",
				"T1",
				"G1 X10 Y10 Z10 E0",
				"T0",
				"G1 X10 Y10 Z10 E0",
				// now do the same thing with a long enough print to cause
				// cooling and heating
				null,
			};

			string[] expected = new string[]
			{
				"M114",
				"T0",
				"M114",
				"M104 T1 S240",
				"T0", // temp switching set extruder after for smoothie
				"M114", // 6
				"M114",
				"M104 T0 S230",
				"G1 X10 Y10 Z10 F2500",
				"G1 Y111",
				"M104 T0 S200",
				"T1",
				"M114", // after a tool change we inject an M114
				"M104 T1 S240",
				"G1 Y220",
				"G1 X9 Y8 F3000",
				"G1 Z7 F315",
				"G1 F2500",
				"G1",
				"G1 X332",
				"M104 T1 S210",
				"T0",
				"M114",
				"G1 X444",
				"G1 X10 Y10 F3000",
				"G1 Z10 F315",
				"G1 F2500",
				"G1",
				"Communication State: FinishedPrint",
				null
			};

			PrinterConfig printer = SetupToolChangeSettings();
			ValidateStreamResponseWhilePrintingAsync(expected, printer, inputLines).GetAwaiter().GetResult();
			return Task.CompletedTask;
		}

		[Test, Category("GCodeStream")]
		public Task ToolChangeHeatT0NoExtrusion()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// tell the printer to heat up
				"M104 T0 S230",
				// send some movement comands with tool switching
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0 F2500",
				"T1",
				"G1 X10 Y10 Z10 E0",
				"T0",
				"G1 X10 Y10 Z10 E0",
				// now do the same thing with a long enough print to cause
				// cooling and heating
				null,
			};

			string[] expected = new string[]
			{
				"M114",
				"T0",
				"M114",
				"M104 T0 S230",
				"G1 X10 Y10 Z10 F2500",
				"G1 Y111",
				"M104 T0 S200",
				"T1",
				"M114",
				"G1 Y220",
				"G1 X9 Y8 F3000",
				"G1 Z7 F315",
				"G1 F2500",
				"G1",
				"G1 X332",
				"T0",
				"M114",
				"M104 T0 S200",
				"G1 X444",
				"G1 X10 Y10 F3000",
				"G1 Z10 F315",
				"G1 F2500",
				"G1",
				"Communication State: FinishedPrint",
				null
			};

			PrinterConfig printer = SetupToolChangeSettings();
			ValidateStreamResponseWhilePrintingAsync(expected, printer, inputLines).GetAwaiter().GetResult();
			return Task.CompletedTask;
		}

		private static PrinterConfig SetupToolChangeSettings()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// this is the pause and resume from the Eris
			var printer = new PrinterConfig(new PrinterSettings());

			// setup for dual extrusion
			printer.Settings.SetValue(SettingsKey.extruder_count, "2");

			printer.Settings.SetValue(SettingsKey.enable_line_splitting, "0");

			printer.Settings.SetValue(SettingsKey.before_toolchange_gcode, "G1 X333");
			printer.Settings.SetValue(SettingsKey.toolchange_gcode, "G1 X444");

			printer.Settings.SetValue(SettingsKey.before_toolchange_gcode_1, "G1 Y111");
			printer.Settings.SetValue(SettingsKey.toolchange_gcode_1, "G1 Y222");

			// set some data for T1
			printer.Settings.Helpers.SetExtruderOffset(1, new Vector3(1, 2, 3));
			return printer;
		}

		private static void ValidateStreamResponse(string[] expected, GCodeStream testStream, List<GCodeStream> streamList = null)
		{
			int expectedIndex = 0;
			string actualLine = testStream.ReadLine();
			string expectedLine = expected[expectedIndex++];

			Assert.AreEqual(expectedLine, actualLine, "Unexpected response from testStream");
			Debug.WriteLine(actualLine);

			while (actualLine != null)
			{
				expectedLine = expected[expectedIndex++];

				actualLine = testStream.ReadLine();
				if (actualLine == "G92 E0")
				{
					testStream.SetPrinterPosition(new PrinterMove(new Vector3(), 0, 300));
				}


				if (actualLine == "G92 Z0")
				{
					testStream.SetPrinterPosition(new PrinterMove(new Vector3(), 0, 0));
				}

				if (actualLine == "; do_resume")
				{
					PauseHandlingStream pauseStream = null;
					foreach (var stream in streamList)
					{
						if (stream as PauseHandlingStream != null)
						{
							pauseStream = (PauseHandlingStream)stream;
							pauseStream.Resume();
						}
					}
				}

				if (expectedLine != actualLine)
				{
					int a = 0;
				}

				Debug.WriteLine(actualLine);
				Assert.AreEqual(expectedLine, actualLine, "Unexpected response from testStream");
			}
		}

		private static async System.Threading.Tasks.Task ValidateStreamResponseWhilePrintingAsync(string[] expected, PrinterConfig printer, string[] inputGCode)
		{
			// set up our serial port finding
			FrostedSerialPortFactory.GetPlatformSerialPort = (serialPortName) =>
			{
				return new Emulator();
			};

			int expectedIndex = 0;

			// register to listen to the printer responses
			printer.Connection.LineSent += (s, actualLine) =>
			{
				if (printer.Connection.Printing)
				{
					var actualLineWithoutChecksum = GCodeFile.GetLineWithoutChecksum(actualLine);

					// this is so that we ignore temperature monitoring
					if(actualLineWithoutChecksum.StartsWith("M105"))
					{
						return;
					}

					string expectedLine = expected[expectedIndex++];
					if(expectedLine != actualLineWithoutChecksum)
					{
						int a = 0;
					}
					Debug.WriteLine("\"" + actualLineWithoutChecksum + "\",");
					//Assert.AreEqual(expectedLine, actualLineWithoutChecksum, "Unexpected response from testStream");
				}
			};

			// set up the emulator
			printer.Settings.SetValue($"{Environment.MachineName}_com_port", "Emulator");
			// connect to the emulator
			printer.Connection.Connect();
			var time = Stopwatch.StartNew();
			while (!printer.Connection.IsConnected
				&& time.ElapsedMilliseconds < (1000 * 60 * 3))
			{
				Thread.Sleep(1000);
			}
			
			// start a print
			var inputStream = new MemoryStream(Encoding.ASCII.GetBytes(string.Join("\n", inputGCode)));
			printer.Connection.CommunicationState = MatterHackers.MatterControl.PrinterCommunication.CommunicationStates.PreparingToPrint;
			await printer.Connection.StartPrint(inputStream);

			// wait for the print to finish (or 3 minutes to pass)
			time = Stopwatch.StartNew();
			while(printer.Connection.Printing
				&& time.ElapsedMilliseconds < (1000 * 60 * 3))
			{
				Thread.Sleep(1000);
			}

			Assert.AreEqual(expectedIndex + 1, expected.Length, "We should have seen all the expected lines");
		}

		private static void Connection_LineReceived(object sender, string e)
		{
			throw new System.NotImplementedException();
		}

		[Test, Category("GCodeStream")]
		public void WriteReplaceStreamTests()
		{
			string[] inputLines = new string[]
			{
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0",
				"M114",
				"G29",
				"G28",
				"G28 X0",
				"M107",
				"M107 ; extra stuff",
				null,
			};

			string[] expected = new string[]
			{
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0",
				"M114",
				"G29",
				"G28",
				"M115",
				"G28 X0",
				"M115",
				"; none",
				"; none ; extra stuff",
				null,
			};

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());

			printer.Settings.SetValue(SettingsKey.write_regex, "\"^(G28)\",\"G28,M115\"\\n\"^(M107)\",\"; none\"");

			var inputLinesStream = new TestGCodeStream(printer, inputLines);
			var queueStream = new QueuedCommandsStream(printer, inputLinesStream);
			ProcessWriteRegexStream writeStream = new ProcessWriteRegexStream(printer, queueStream, queueStream);
			ValidateStreamResponse(expected, writeStream);
		}

		[Test, Category("GCodeStream")]
		public void FeedRateRatioChangesFeedRate()
		{
			string line;
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			Assert.AreEqual(1, (int) FeedRateMultiplyerStream.FeedRateRatio, "FeedRateRatio should default to 1");

			PrinterConfig printer = null;
			var gcodeStream = new FeedRateMultiplyerStream(printer, new TestGCodeStream(printer, new string[] { "G1 X10 F1000", "G1 Y5 F1000" }));

			line = gcodeStream.ReadLine();

			Assert.AreEqual("G1 X10 F1000", line, "FeedRate should remain unchanged when FeedRateRatio is 1.0");

			FeedRateMultiplyerStream.FeedRateRatio = 2;

			line = gcodeStream.ReadLine();
			Assert.AreEqual("G1 Y5 F2000", line, "FeedRate should scale from F1000 to F2000 when FeedRateRatio is 2x");
		}

		[Test, Category("GCodeStream")]
		public void ExtrusionRatioChangesExtrusionAmount()
		{
			string line;
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			Assert.AreEqual(1, (int) ExtrusionMultiplyerStream.ExtrusionRatio, "ExtrusionRatio should default to 1");

			PrinterConfig printer = null;
			var gcodeStream = new ExtrusionMultiplyerStream(printer, new TestGCodeStream(printer, new string[] { "G1 E10", "G1 E0 ; Move back to 0", "G1 E12" }));

			line = gcodeStream.ReadLine();
			// Move back to E0
			gcodeStream.ReadLine();

			Assert.AreEqual("G1 E10", line, "ExtrusionMultiplyer should remain unchanged when FeedRateRatio is 1.0");

			ExtrusionMultiplyerStream.ExtrusionRatio = 2;

			line = gcodeStream.ReadLine();

			Assert.AreEqual("G1 E24", line, "ExtrusionMultiplyer should scale from E12 to E24 when ExtrusionRatio is 2x");
		}
	}

	public class TestGCodeStream : GCodeStream
	{
		private int index = 0;
		private string[] lines;

		public TestGCodeStream(PrinterConfig printer, string[] lines)
			: base(printer)
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

		public override GCodeStream InternalStream => null;

		public override string DebugInfo => "";
	}
}