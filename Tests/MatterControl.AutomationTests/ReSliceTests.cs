﻿/*
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.VersionManagement;
using NUnit.Framework;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation")]
	public class ReSliceTests
	{
		// [Test, Category("Emulator"), Ignore("WIP")]
		[Test, ChildProcessTest, Category("Emulator")]
		public async Task ReSliceHasCorrectEPositions()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// testRunner.ClickByName("Connection Wizard Skip Sign In Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					ApplicationController.Instance.Allow32BitReSlice = true;
					var printer = testRunner.FirstPrinter();
					printer.Settings.SetValue(SettingsKey.enable_line_splitting, "0");

					var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
					var scene = view3D.Object3DControlLayer.Scene;

					// Add a callback to check that every line has an extruder
					// distance greater than the largest distance minus the max retraction
					// amount and less than some amount that is reasonable
					double lastAbsoluteEPosition = 0;
					double largestAbsoluteEPosition = 0;
					double largestRetraction = 0;

					emulator.EPositionChanged += (e, s) =>
					{
						largestAbsoluteEPosition = Math.Max(largestAbsoluteEPosition, emulator.CurrentExtruder.AbsoluteEPosition);
						var delta = emulator.CurrentExtruder.AbsoluteEPosition - lastAbsoluteEPosition;
						if (delta < largestRetraction)
						{
							largestRetraction = delta;
						}

						double printerRetraction = 7 + .1; // the airwolf has a retraction of 7 mm
						Assert.GreaterOrEqual(delta, -printerRetraction, "We should never move back more than the retraction amount");
						Assert.GreaterOrEqual(emulator.CurrentExtruder.AbsoluteEPosition, largestAbsoluteEPosition - printerRetraction, "Never go back more than the retraction amount");
						Assert.LessOrEqual(emulator.CurrentExtruder.AbsoluteEPosition, lastAbsoluteEPosition + 10, "We should never move up more than 10 mm");
						lastAbsoluteEPosition = emulator.CurrentExtruder.AbsoluteEPosition;
					};

					// Add a cube to the bed
					testRunner.NavigateToFolder("Queue Row Item Collection")
						.ClickByName("Row Item cube_20x20x20.stl")
						.ClickByName("Print Library Overflow Menu")
						.ClickByName("Add to Bed Menu Item")
						.Delay()
						.ClickByName("Print Library Overflow Menu")
						.Delay()
						.ClickByName("Add to Bed Menu Item")
						.Delay()
						// start the print
						.StartPrint(printer, pauseAtLayers: "50;60")
						// Wait for pause
						// the yes button is 'Resume'
						// NOTE: ClickByName Failed: Named GuiWidget not found [No Button]
						//       It appears that printing can just take too long.
						//       This might be fixed by using a semaphore in MatterHackers.PrinterEmulator.Emulator.
						.ClickByName("No Button", secondsToWait: 80)
						// Delete the cube
						.ClickByName("Bed Options Menu")
						.ClickByName("Clear Bed Menu Item")
						.Delay();
						// ensure there is nothing on the bed
					Assert.AreEqual(0, scene.Children.Count);

					// Add a cylinder
					testRunner.NavigateToFolder("Queue Row Item Collection")
						.ClickByName("Row Item cylinder_5x20.stl")
						.ClickByName("Print Library Overflow Menu")
						.ClickByName("Add to Bed Menu Item")
						.ClickByName("Add Content Menu")
						// re-slice the part
						.ClickByName("Re-Slice Button")
						// The change to new g-code
						.ClickByName("Switch Button", secondsToWait: 10)
						// and resume the print
						.ClickByName("Resume Task Button")
						// Wait for next pause
						.ClickByName("No Button", secondsToWait: 80)
						// Switch back to the cube
						// Delete the cylinder
						.ClickByName("Bed Options Menu")
						.ClickByName("Clear Bed Menu Item");
					
					// ensure there is nothing on the bed
					Assert.AreEqual(0, scene.Children.Count);

					// add the cube
					testRunner.NavigateToFolder("Queue Row Item Collection")
						.ClickByName("Row Item cube_20x20x20.stl")
						.ClickByName("Print Library Overflow Menu")
						.ClickByName("Add to Bed Menu Item")
						.ClickByName("Add Content Menu")
						// re-slice the part
						.ClickByName("Re-Slice Button")
						.ClickByName("Switch Button", secondsToWait: 10)
						// and resume the print
						.ClickByName("Resume Task Button")
						// Wait for done
						.WaitForPrintFinished(printer);

					// this will make sure we turned off line splitting and had good data about the extruder position
					Assert.AreEqual(-7, largestRetraction, "Airwolf HD has a retraction of 7mm, make sure we had one");
				}

				return Task.CompletedTask;
			},
			maxTimeToRun: 390,
			queueItemFolderToAdd: QueueTemplate.ReSliceParts);
		}
	}
}