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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.VersionManagement;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class ReSliceTests
	{
		[Test, Category("Emulator"), Ignore("WIP")]
		public async Task ReSliceHasCorrectEPositions()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				//testRunner.ClickByName("Connection Wizard Skip Sign In Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
					var scene = view3D.InteractionLayer.Scene;

					testRunner.OpenPrintPopupMenu();

					// Add a pause on layer(in the center)
					testRunner.ClickByName("Layer(s) To Pause Field");
					testRunner.Type("50;60");

					// Add a callback to check that every line has an extruder 
					// distance greater than the largest distance minus the max retraction 
					// amount and less than some amount that is reasonable
					double lastAbsoluteEPostion = 0;
					emulator.EPositionChanged += (e, s) =>
					{
						Assert.GreaterOrEqual(emulator.CurrentExtruder.AbsoluteEPosition, lastAbsoluteEPostion - 5, "We should never move back more than 5 mm");
						lastAbsoluteEPostion = emulator.CurrentExtruder.AbsoluteEPosition;
					};

					// Add a cube to the bed
					testRunner.NavigateToFolder("Print Queue Row Item Collection");
					testRunner.ClickByName("Row Item cube_20x20x20");
					testRunner.ClickByName("Print Library Overflow Menu");
					testRunner.ClickByName("Add to Plate Menu Item");

					// start the print
					testRunner.StartPrint();

					// Wait for pause
					testRunner.WaitForName("No Button", 20);// the yes button is 'Resume'
					testRunner.ClickByName("No Button");

					// Delete the cube
					testRunner.ClickByName("Bed Options Menu");
					testRunner.ClickByName("Clear Bed Menu Item");

					testRunner.Delay(100);

					// ensure there is nothing on the bed
					Assert.AreEqual(0, scene.Children.Count);

					// Add a cylinder
					testRunner.ClickByName("Row Item cylinder_5x20");
					testRunner.ClickByName("Print Library Overflow Menu");
					testRunner.ClickByName("Add to Plate Menu Item");

					// re-slice the part
					testRunner.ClickByName("Re-Slice Button");
					testRunner.WaitForName("Yes Button", 20); // The change to new g-code
					testRunner.ClickByName("Yes Button");

					// and resume the print
					testRunner.ClickByName("Resume Task Button");

					// Wait for next pause
					testRunner.WaitForName("No Button", 20);// the yes button is 'Resume'
					testRunner.ClickByName("No Button");

					// Switch back to the cube
					// Delete the cylinder
					testRunner.ClickByName("Bed Options Menu");
					testRunner.ClickByName("Clear Bed Menu Item");

					// ensure there is nothing on the bed
					Assert.AreEqual(0, scene.Children.Count);

					// add the cube
					testRunner.ClickByName("Row Item cube_20x20x20");
					testRunner.ClickByName("Print Library Overflow Menu");
					testRunner.ClickByName("Add to Plate Menu Item");

					// re-slice the part
					testRunner.ClickByName("Re-Slice Button");
					testRunner.WaitForName("Yes Button", 20); // The change to new g-code
					testRunner.ClickByName("Yes Button");

					// and resume the print
					testRunner.ClickByName("Resume Task Button");

					// Wait for done
					testRunner.WaitForPrintFinished();
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 190, queueItemFolderToAdd: QueueTemplate.ReSliceParts);
		}
	}
}