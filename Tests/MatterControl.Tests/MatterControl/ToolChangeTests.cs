/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.PrinterEmulator;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl.ToolChanges
{
	public class ToolChangeTests
	{
		[Test, Category("GCodeStream")]
		public async Task ToolChangeNoHeat()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// send some movement commands with tool switching
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0 F2500",
				"T1",
				"G1 X10 Y10 Z10 E0",
				"T0",
				"G1 X10 Y10 Z10 E0",
			};

			// create a printer for dual extrusion printing
			PrinterConfig printer = SetupToolChangeSettings();

			// validate that no heater is heated at anytime during the print
			printer.Connection.HotendTargetTemperatureChanged += (s, extruderIndex) =>
			{
				if (printer.Connection.GetTargetHotendTemperature(extruderIndex) > 0)
				{
					Assert.Fail("No hotend should ever change temp during this test.");
				}
			};

			var sentLines = await printer.RunSimulatedPrint(inputLines);

			var expected = new string[]
			{
				"M114", // initial position request
				"T0", // initial tool assignment (part of starting a default print)
				"M114", // we always ask position after tool assignment
				"G1 X10 Y10 Z10 F2500", // go to the position requested
				"G1 Y111", // the pre switch T1 code
				"M114", // always sent after a ; NO_PROCESSING command
				"T1",
				"M114", // after a tool change we inject an M114
				"G1 Y222", // the post switch T1 code
				"M114", // always sent after a ; NO_PROCESSING command
				"G1 X9 Y8 F3000", // the destination position with consideration of T1 offset
				"G1 Z7 F315", // we set xy than z, so this is the z
				"G1 F2500", // we then reset the F after the pre and post gcode run
				"G1 X111", // pre T0 code
				"M114", // always sent after a ; NO_PROCESSING command
				"T0",
				"M114", // always send after switch
				"G1 X222", // post switch T0 code
				"M114", // always sent after a ; NO_PROCESSING command
				"G1 X10 Y10 F3000", // return to extruder position
				"G1 Z10 F315",
				"G1 F2500",
			};

			Assert.AreEqual(expected, sentLines);
		}

		// A test that proves that: T0, no move, T1, T0, move does not send switch extruder gcode
		[Test, Category("GCodeStream")]
		public async Task NoToolChangeIfNoMove()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// send some movement commands with tool switching
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0 F2500",
				"T1",
				"T0",
				"G1 X11 Y11 Z11 E0 F2500",
			};

			// create a printer for dual extrusion printing
			PrinterConfig printer = SetupToolChangeSettings();

			var sentLines = await printer.RunSimulatedPrint(inputLines);

			var expected = new string[]
			{
				"M114", // initial position request
				"T0", // initial tool assignment (part of starting a default print)
				"M114", // we always ask position after tool assignment
				"G1 X10 Y10 Z10 F2500", // go to the position requested
				"G1 X11 Y11 Z11", // go to the position requested
			};

			Assert.AreEqual(expected, sentLines);
		}

		// A test that proves that: T0, no move, T1, temp set, T0, move does not send switch extruder gcode
		// but there is the correct extruder set, T1, then temp, than T0
		[Test, Category("GCodeStream")]
		public async Task ToolChangeTempSetWithNoMove()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// send some movement commands with tool switching
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0 F2500",
				"T1",
				"M104 S100",
				"T0",
				"G1 X11 Y11 Z11 E0 F2500",
			};

			// create a printer for dual extrusion printing
			PrinterConfig printer = SetupToolChangeSettings();

			var sentLines = await printer.RunSimulatedPrint(inputLines);

			var expected = new string[]
			{
				"M114", // initial position request
				"T0", // initial tool assignment (part of starting a default print)
				"M114", // we always ask position after tool assignment
				"G1 X10 Y10 Z10 F2500", // go to the position requested
				"T1", // switch to T1 for temp
				"M104 S100", // set the temp
				"T0", // smoothie command to ensure still on T0 after temp set
				"M114", // always ask position after T
				"G1 X11 Y11 Z11", // go to the position requested
			};
			Assert.AreEqual(expected, sentLines);
		}

		// A test that proves that: T0, no move, T1, extrude, T0, move does not send switch extruder gcode
		// but does switch to and back for extrude
		[Test, Category("GCodeStream")]
		public async Task NoMoveOnToolChangeButWithExtrude()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// send some movement commands with tool switching
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0 F2500",
				"T1",
				"G1 E10",
				"G1 E20",
				"T0",
				"G1 E30",
				"G1 X11 Y11 Z11 E30 F2500",
			};

			// create a printer for dual extrusion printing
			PrinterConfig printer = SetupToolChangeSettings();

			var sentLines = await printer.RunSimulatedPrint(inputLines);

			var expected = new string[]
			{
				"M114", // initial position request
				"T0", // initial tool assignment (part of starting a default print)
				"M114", // we always ask position after tool assignment
				"G1 X10 Y10 Z10 F2500", // go to the position requested
				"T1", // switch to do extrusion
				"G92 E0", // set the extrusion after switch (for smoothie)
				"G1 E10", // the first extrusion on T1
				"T0", // switch back to T0
				"G92 E10", // set the extrusion after switch (for smoothie)
				"M114", // 10
				"T1",
				"G92 E10", // set the extrusion after switch (for smoothie)
				"G1 E20", // a second extrusion without changing back to T0
				"T0", // the 'no move' switch back to T0
				"G92 E20", // set the extrusion after switch (for smoothie)
				"M114",
				"G1 E30", // extrude on T0
				"G1 X11 Y11 Z11", // go to the position requested
			};
			Assert.AreEqual(expected, sentLines);
		}

		[Test, Category("GCodeStream")]
		public async Task ToolChangeTempAndSwitch()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// tell the printer to heat up
				"M104 T1 S240", // start with T0 to test smoothie temp change code
				"M104 T0 S230",
				// send some movement commands with tool switching
				"; the printer is moving normally",
				"G1 X10 Y10 Z10 E0 F2500",
				"T1",
				"G1 X10 Y10 Z10 E0",
				"T0",
				"G1 X10 Y10 Z10 E0",
				// now do the same thing with a long enough print to cause
				// cooling and heating
			};

			PrinterConfig printer = SetupToolChangeSettings();

			var sentLines = await printer.RunSimulatedPrint(inputLines);

			// validate that both temperatures get set and only once each
			var expected = new string[]
			{
				"M114",
				"T0",
				"M114",
				"M104 T1 S240", // initial heating
				"M104 T0 S230",
				"T0",
				"M114",
				"G1 X10 Y10 Z10 F2500",
				"G1 Y111",
				"M114",
				"T1",
				"M114",
				"M104 T1 S240", // **** BUG **** this should not be here
				"T1",
				"M114",
				"G1 Y222",
				"M114",
				"G1 X9 Y8 F3000",
				"G1 Z7 F315",
				"G1 F2500",
				"G1 X111",
				"M114",
				"T0",
				"M114",
				"G1 X222",
				"M114",
				"G1 X10 Y10 F3000",
				"G1 Z10 F315",
				"G1 F2500",
			};
			Assert.AreEqual(expected, sentLines);
		}

		[Test, Category("GCodeStream")]
		public async Task ToolChangeHeatOnlyT0()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// tell the printer to heat up
				"M104 T0 S230",
				// send some movement commands with tool switching
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

			PrinterConfig printer = SetupToolChangeSettings();

			// register to make sure that T0 is heated (only once) and T1 is not heated
			printer.Connection.HotendTargetTemperatureChanged += (s, extruderIndex) =>
			{
				Assert.AreEqual(0, printer.Connection.GetTargetHotendTemperature(1));
			};

			await printer.RunSimulatedPrint(inputLines);
		}

		[Test, Category("GCodeStream")]
		public async Task ToolChangeHeatOnlyT1()
		{
			string[] inputLines = new string[]
			{
				"T0",
				// tell the printer to heat up
				"M104 T1 S230",
				// send some movement commands with tool switching
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

			PrinterConfig printer = SetupToolChangeSettings();
			// register to make sure that T0 is heated (only once) and T1 is not heated
			printer.Connection.HotendTargetTemperatureChanged += (s, extruderIndex) =>
			{
				Assert.AreEqual(0, printer.Connection.GetTargetHotendTemperature(0));
			};

			await printer.RunSimulatedPrint(inputLines);
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

			printer.Settings.SetValue(SettingsKey.before_toolchange_gcode, "G1 X111 ; NO_PROCESSING");
			printer.Settings.SetValue(SettingsKey.toolchange_gcode, "G1 X222 ; NO_PROCESSING");

			printer.Settings.SetValue(SettingsKey.before_toolchange_gcode_1, "G1 Y111 ; NO_PROCESSING");
			printer.Settings.SetValue(SettingsKey.toolchange_gcode_1, "G1 Y222 ; NO_PROCESSING");

			// set some data for T1
			printer.Settings.Helpers.SetExtruderOffset(1, new Vector3(1, 2, 3));

			return printer;
		}
	}

	public static class ExtensionMethods
	{
		public static async Task<List<string>> RunSimulatedPrint(this PrinterConfig printer, string[] inputGCode)
		{
			// set up our serial port finding
			FrostedSerialPortFactory.GetPlatformSerialPort = (_) =>
			{
				return new Emulator();
			};

			FrostedSerialPort.AllowEmulator = true;

			var sentLines = new List<string>();

			// register to listen to the printer responses
			printer.Connection.LineSent += (s, line) =>
			{
				if (printer.Connection.Printing)
				{
					sentLines.Add(line);
				}
			};

			// set up the emulator
			printer.Settings.SetValue($"{Environment.MachineName}_com_port", "Emulator");

			// connect to the emulator
			printer.Connection.Connect();

			var timer = Stopwatch.StartNew();

			// wait for the printer to be connected
			while (!printer.Connection.IsConnected
				&& timer.ElapsedMilliseconds < (1000 * 40))
			{
				Thread.Sleep(100);
			}

			// start a print
			var inputStream = new MemoryStream(Encoding.ASCII.GetBytes(string.Join("\n", inputGCode)));
			printer.Connection.CommunicationState = MatterHackers.MatterControl.PrinterCommunication.CommunicationStates.PreparingToPrint;
			await printer.Connection.StartPrint(inputStream);

			// wait up to 40 seconds for the print to finish
			timer = Stopwatch.StartNew();
			while (printer.Connection.Printing
				&& timer.ElapsedMilliseconds < (1000 * 40))
			{
				Thread.Sleep(100);
			}

			// Project to string without checksum which is not an M105 request
			return sentLines.Select(l => GCodeFile.GetLineWithoutChecksum(l)).Where(l => !l.StartsWith("M105")).ToList();
		}
	}
}