/*
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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class GCodeProcessingTests
	{
		[Test, Category("GCodeProcessing")]
		public void ReplaceMacroValuesWorking()
		{
			StaticData.Instance = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));
			
			TestMacroReplacement("[temperature]", "200");
			TestMacroReplacement("[first_layer_speed]", "1080");
			TestMacroReplacement("[bed_remove_part_temperature]", "0");
			TestMacroReplacement("[bridge_fan_speed]", "100");
			TestMacroReplacement("[bridge_speed]", "20");
			TestMacroReplacement("[external_perimeter_speed]", "1260");
			TestMacroReplacement("[extruder_wipe_temperature]", "0");
			TestMacroReplacement("[filament_diameter]", "3");
			TestMacroReplacement("[first_layer_bed_temperature]", "75");
			TestMacroReplacement("[first_layer_temperature]", "205");
			TestMacroReplacement("{max_fan_speed}", "100");
			TestMacroReplacement("{min_fan_speed}", "35");
			TestMacroReplacement("{perimeter_speed}", "1800");
			TestMacroReplacement("{raft_print_speed}", "3600");
			TestMacroReplacement("{retract_length}", "1");
			TestMacroReplacement("{retract_speed}", "1800");
			TestMacroReplacement("{support_material_speed}", "3600");
			TestMacroReplacement("{temperature}", "200");
			TestMacroReplacement("{z_offset}", "0");
			TestMacroReplacement("[" + SettingsKey.bed_temperature + "]", "70");
			TestMacroReplacement("{infill_speed}", "3600");
			TestMacroReplacement("{min_print_speed}", "600");
			TestMacroReplacement("{travel_speed}", "7800");
		}

		private void TestMacroReplacement(string inputText, string outputControl)
		{
			string outputTest = GCodeProcessing.ReplaceMacroValues(inputText);

			Assert.IsTrue(outputTest == outputControl);
		}
	}
}
