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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.PlatformAbstract;
using System.IO;
using MatterHackers.Agg.UI.Tests;

namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class CheckBoxInLibraryIsClickable
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void ClickOnLibraryCheckBoxes()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);

				// Now do the actions specific to this test. (replace this for new tests)
				{
					testRunner.ClickByName("Library Tab");
					testRunner.Wait(1);
					SystemWindow systemWindow;
					GuiWidget rowItem = testRunner.GetWidgetByName("Local Library Row Item Collection", out systemWindow);
					testRunner.Wait(1);

					SearchRegion rowItemRegion = testRunner.GetRegionByName("Local Library Row Item Collection");

					testRunner.ClickByName("Library Edit Button");
					testRunner.Wait(1);

					SystemWindow containingWindow;
					GuiWidget foundWidget = testRunner.GetWidgetByName("Row Item Select Checkbox", out containingWindow, searchRegion: rowItemRegion);
					CheckBox checkBoxWidget = foundWidget as CheckBox;
					resultsHarness.AddTestResult(checkBoxWidget != null, "We should have an actual checkbox");
					resultsHarness.AddTestResult(checkBoxWidget.Checked == false, "currently not checked");

					testRunner.ClickByName("Row Item Select Checkbox", searchRegion: rowItemRegion);
					testRunner.Wait(.5);
					resultsHarness.AddTestResult(checkBoxWidget.Checked == true, "currently checked");

					testRunner.ClickByName("Local Library Row Item Collection");
					testRunner.Wait(.5);
					resultsHarness.AddTestResult(checkBoxWidget.Checked == false, "currently not checked");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}

			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			// NOTE: In the future we may want to make the "Local Library Row Item Collection" not clickable. 
			// If that is the case fix this test to click on a child of "Local Library Row Item Collection" instead.
			Assert.IsTrue(testHarness.AllTestsPassed); 
			Assert.IsTrue(testHarness.TestCount == 4); // make sure we ran all our tests
		}
	}
}