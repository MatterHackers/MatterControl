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
using System.Threading;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class CreateLibraryFolder
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void CreateFolderStarsOutWithTextFiledFocusedAndEditable()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);

				// Now do the actions specific to this test. (replace this for new tests)
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.ClickByName("Create Folder From Library Button");

					testRunner.Wait(.5);
					testRunner.Type("Test Text");
					testRunner.Wait(.5);

					SystemWindow containingWindow;
					GuiWidget textInputWidget = testRunner.GetWidgetByName("Create Folder - Text Input", out containingWindow);
					MHTextEditWidget textWidgetMH = textInputWidget as MHTextEditWidget;
					resultsHarness.AddTestResult(textWidgetMH != null, "Found Text Widget");
					resultsHarness.AddTestResult(textWidgetMH.Text == "Test Text", "Had the right text");
					containingWindow.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}
}