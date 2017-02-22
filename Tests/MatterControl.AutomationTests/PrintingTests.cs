﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PrinterEmulator;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class PrintingTests
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task CompletingPrintTurnsoffHeat()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

					testRunner.ClickByName("Printer Tab", 1);
					testRunner.ClickByName("Custom G-Code Tab", 1);
					testRunner.ClickByName("end_gcode Edit Field");
					testRunner.Type("^a");
					testRunner.Type("{BACKSPACE}");
					testRunner.Type("G28");

					testRunner.ClickByName("Start Print Button", 1);

					testRunner.WaitForName("Done Button", 120);
					Assert.True(testRunner.NameExists("Done Button"), "The print has completed");
					testRunner.WaitForName("Print Again Button", 1);

					testRunner.Delay(5);

					Assert.Less(PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(0), 30);
					Assert.Less(PrinterConnectionAndCommunication.Instance.ActualBedTemperature, 10);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 200);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task PulseRequiresLevelingAndLevelingWorks()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator("Pulse", "A-134"))
				{
					var emulator = emulatorDisposable as Emulator;
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					// close the finish setup window
					testRunner.ClickByName("Cancel Button");

					MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

					testRunner.ClickByName("General Tab", 1);
					testRunner.ClickByName("Single Print Tab", 1);
					testRunner.ClickByName("Layer(s) To Pause: Edit");
					testRunner.Type("2");

					// switch to controls so we can see the heights
					testRunner.ClickByName("Controls Tab");

					// run the leveling wizard
					testRunner.ClickByName("Finish Setup Button");
					testRunner.ClickByName("Next Button");
					testRunner.ClickByName("Next Button");
					testRunner.ClickByName("Next Button");
					testRunner.ClickByName("Next Button");
					for (int i = 0; i < 3; i++)
					{
						testRunner.ClickByName("Move Z positive", .5);
						testRunner.ClickByName("Next Button", .5);
						testRunner.ClickByName("Next Button", .5);
						testRunner.ClickByName("Next Button", .5);
					}
					testRunner.ClickByName("Done Button");

					testRunner.Delay(1);

					// print a part
					testRunner.ClickByName("Start Print Button", 1);
					// assert the leveling is working
					testRunner.WaitForName("Resume Button", 200);

					Assert.Greater(emulator.ZPosition, 5);

					testRunner.ClickByName("Cancel Print Button", 1);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 300);
		}

		private EventHandler unregisterEvents;

		[Test, Apartment(ApartmentState.STA)]
		public async Task TuningAdjustmentsDefaultToOneAndPersist()
		{
			double targetExtrusionRate = 1.5;
			double targetFeedRate = 2;

			AutomationTest testToRun = (testRunner) =>
			{
				SystemWindow systemWindow;

				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.SwitchToSettingsAndControls();

					testRunner.ClickByName("Controls Tab", 1);

					testRunner.ClickByName("Start Print Button", 1);

					var container = testRunner.GetWidgetByName("ManualPrinterControls.ControlsContainer", out systemWindow, 5);

					// Scroll the widget into view
					var scrollable = container.Parents<ManualPrinterControls>().First().Parents<ScrollableWidget>().First();
					var width = scrollable.Width;

					// Workaround needed to scroll to the bottom of the Controls panel
					//scrollable.ScrollPosition = new Vector2();
					scrollable.ScrollPosition = new Vector2(0, 30);

					// Workaround to force layout to fix problems with size of Tuning Widgets after setting ScrollPosition manually
					scrollable.Width = width - 1;
					scrollable.Width = width;

					// Tuning values should default to 1 when missing
					ConfirmExpectedSpeeds(testRunner, 1, 1);

					testRunner.Delay();
					testRunner.ClickByName("Extrusion Multiplier NumberEdit");
					testRunner.Type(targetExtrusionRate.ToString());

					testRunner.ClickByName("Feed Rate NumberEdit");
					testRunner.Type(targetFeedRate.ToString());

					// Force focus away from the feed rate field, causing an persisted update
					testRunner.ClickByName("Controls Tab", 1);
					testRunner.Delay();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for slicing to complete before setting target values
					testRunner.Delay(() => PrinterConnectionAndCommunication.Instance.PrintingState == PrinterConnectionAndCommunication.DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for printing to complete
					var resetEvent = new AutoResetEvent(false);
					PrinterConnectionAndCommunication.Instance.PrintFinished.RegisterEvent((s, e) => resetEvent.Set(), ref unregisterEvents);
					resetEvent.WaitOne();

					testRunner.WaitForName("Done Button", 30);
					testRunner.WaitForName("Print Again Button", 1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Restart the print
					testRunner.ClickByName("Print Again Button", 1);
					testRunner.Delay(2);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					testRunner.CancelPrint();
					testRunner.Delay(1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideHeight:900, maxTimeToRun: 990);
		}

		private static void ConfirmExpectedSpeeds(AutomationRunner testRunner, double targetExtrusionRate, double targetFeedRate)
		{
			SystemWindow systemWindow;
			SolidSlider slider;

			// Assert the UI has the expected values
			slider = testRunner.GetWidgetByName("Extrusion Multiplier Slider", out systemWindow, 5) as SolidSlider;
			Assert.AreEqual(targetExtrusionRate, slider.Value);

			slider = testRunner.GetWidgetByName("Feed Rate Slider", out systemWindow, 5) as SolidSlider;
			Assert.AreEqual(targetFeedRate, slider.Value);

			testRunner.Delay(.2);

			// Assert the changes took effect on the model
			Assert.AreEqual(targetExtrusionRate, ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.extrusion_ratio));
			Assert.AreEqual(targetFeedRate, ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.feedrate_ratio));
		}
	}
}
