﻿/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class GCodeProcessingTests
	{
		[Test, Category("GCodeProcessing")]
		public void ReadTemperaturesCorrectly()
		{
			ParseTempAndValidate("ok B:12.0 /0.0 T0:12.8 /0.0 T1:12.8 /0.0 T2:12.8 /0.0 @:0 B@:0", 12.8, 12.8, 12.8, 12.0);

			ParseTempAndValidate("ok T:139.6 /0.0 @:0.00W", 139.6, 0, 0, 0.0);
			ParseTempAndValidate("ok T:139.6 B:136.2 /0.0 @:0.00W", 139.6, 0, 0, 136.2);
		}

		private void ParseTempAndValidate(string gcodeString, double? extruder0, double? extruder1, double? extruder2, double? bedTemp)
		{
			double[] extruders = new double[16];
			double bed = 0;
			PrinterConnection.ParseTemperatureString(gcodeString, extruders, null, ref bed, null);
			Assert.IsTrue(extruders[0] == extruder0);
			Assert.IsTrue(extruders[1] == extruder1);
			Assert.IsTrue(extruders[2] == extruder2);
			Assert.IsTrue(bed == bedTemp);
		}

		[Test]
		public void SmoothieDualExtruderM105Response()
		{
			double[] extruders = new double[16];
			double bed = 0;

			// As of 2018-11-29 with an unknown users config
			string smoothieDualExtruderM105Response = "ok T:220.1 /220.0 @109 T1:222.7 /221.0 @49 B:32.2 /0.0 @0";

			PrinterConnection.ParseTemperatureString(smoothieDualExtruderM105Response, extruders, null, ref bed, null);

			Assert.AreEqual("220.1", extruders[0].ToString("0.#"), "Incorrect Extruder 0 result");
			Assert.AreEqual("222.7", extruders[1].ToString("0.#"), "Incorrect Extruder 1 result");
		}

		[Test, Category("GCodeProcessing")]
		public void ReplaceMacroValuesWorking()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

			var settings = new PrinterSettings();
			//settings.Slicer = new EngineMappingsMatterSlice();

			void TestMacroReplacement(string inputText, string outputControl)
			{
				Assert.AreEqual(outputControl, settings.ReplaceSettingsNamesWithValues(inputText));
			}

			TestMacroReplacement("[temperature]", "200");
			TestMacroReplacement("[first_layer_speed]", "1080");
			TestMacroReplacement("[bed_remove_part_temperature]", "0");
			TestMacroReplacement("[bridge_fan_speed]", "100");
			TestMacroReplacement("[bridge_speed]", "3");
			TestMacroReplacement("[external_perimeter_speed]", "1260");
			TestMacroReplacement("[extruder_wipe_temperature]", "0");
			TestMacroReplacement("[filament_diameter]", "3");
			TestMacroReplacement("[first_layer_bed_temperature]", "70");
			TestMacroReplacement("[first_layer_temperature]", "205");
			TestMacroReplacement("{max_fan_speed}", "100");
			TestMacroReplacement("{min_fan_speed}", "35");
			TestMacroReplacement("{perimeter_speed}", "1800");
			TestMacroReplacement("{raft_print_speed}", "3600");
			TestMacroReplacement("{retract_length}", "1");
			TestMacroReplacement("{retract_speed}", "1800");
			TestMacroReplacement("{support_material_speed}", "3600");
			TestMacroReplacement("{temperature}", "200");
			TestMacroReplacement("[bed_temperature]", "70");
			TestMacroReplacement("{infill_speed}", "3600");
			TestMacroReplacement("{min_print_speed}", "600");
			TestMacroReplacement("{travel_speed}", "7800");

			// make sure we pick up the right temp when there is a bed surface change
			settings.SetValue(SettingsKey.has_swappable_bed, "1");
			settings.SetValue(SettingsKey.bed_temperature_blue_tape, "84.2");
			settings.SetValue(SettingsKey.bed_surface, "Blue Tape");
			TestMacroReplacement("[bed_temperature]", "84.2");
		}
	}
}
