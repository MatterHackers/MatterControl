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
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class CheckBoxInLibraryIsClickable
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task ClickOnLibraryCheckBoxes()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("Library Tab", 3);
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				SystemWindow systemWindow;
				string itemName = "Row Item Calibration - Box";

				GuiWidget rowItem = testRunner.GetWidgetByName(itemName, out systemWindow, 3);

				SearchRegion rowItemRegion = testRunner.GetRegionByName(itemName, 3);

				testRunner.ClickByName("Library Edit Button", 3);
				testRunner.Wait(.5);

				GuiWidget foundWidget = testRunner.GetWidgetByName("Row Item Select Checkbox", out systemWindow, 3, searchRegion: rowItemRegion);
				CheckBox checkBoxWidget = foundWidget as CheckBox;
				Assert.IsTrue(checkBoxWidget != null, "We should have an actual checkbox");
				Assert.IsTrue(checkBoxWidget.Checked == false, "currently not checked");

				testRunner.ClickByName("Row Item Select Checkbox", 3, searchRegion: rowItemRegion);
				testRunner.ClickByName("Library Tab");
				Assert.IsTrue(checkBoxWidget.Checked == true, "currently checked");

				testRunner.ClickByName(itemName, 3);
				testRunner.ClickByName("Library Tab");
				Assert.IsTrue(checkBoxWidget.Checked == false, "currently not checked");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}