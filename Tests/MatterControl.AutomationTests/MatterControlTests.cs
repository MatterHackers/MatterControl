/*
Copyright (c) 2017, John Lewin
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class MatterControlTests
	{
		[Test]
		public async Task ThumbnailGenerationMode()
		{
			await MatterControlUtilities.RunTest(async (testRunner) =>
			{
				// Automation tests should initialize with orthographic thumbnails
				var item = new FileSystemFileItem(MatterControlUtilities.GetTestItemPath("Rook.amf"));
				var provider = ApplicationController.Instance.Library.GetContentProvider(item);

				// Generate thumbnail
				var stopWatch = Stopwatch.StartNew();
				await provider.GetThumbnail(item, 400, 400);

				Assert.Less(stopWatch.ElapsedMilliseconds, 2000, "Elapsed thumbnail generation for Rook.amf should be less than 2 seconds for expected orthographic mode");
			});
		}

		[Test]
		public async Task View3DOverflowMenus()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.WaitForFirstDraw();
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				testRunner.ClickByName("Model View Button");
				testRunner.ClickByName("View3D Overflow Menu");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task PrinterTabRemainsAfterReloadAll()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForFirstDraw();

				// Add Guest printers
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");
				Assert.AreEqual(
					1,
					ApplicationController.Instance.ActivePrinters.Count(),
					"One printer should exist after Airwolf add");

				testRunner.SwitchToSliceSettings();

				// Move to Adhesion tab
				testRunner.SelectSliceSettingsField(PrinterSettings.Layout.SliceSettings, "skirts");

				// Click Brim toggle field forcing ReloadAll
				testRunner.WaitForReloadAll(() => testRunner.ClickByName("Create Brim Field"));

				// Ensure tabs remain
				Assert.AreEqual(
					1,
					ApplicationController.Instance.ActivePrinters.Count(),
					"One printer should exist after Airwolf add");

				testRunner.Delay(2);

				Assert.AreEqual(
					1,
					ApplicationController.Instance.MainView.TabControl.AllTabs.Select(t => t.TabContent).OfType<PrinterTabPage>().Count(),
					"One printer tab should exist after ReloadAll");

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}
	}
}