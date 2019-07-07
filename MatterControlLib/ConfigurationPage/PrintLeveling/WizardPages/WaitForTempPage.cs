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
using System.Linq;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class WaitForTempPage : WizardPage
	{
		private ProgressBar bedProgressBar;
		private TextWidget bedProgressBarText;
		private double bedStartingTemp;
		private RunningInterval runningInterval;
		private TextWidget bedDoneText;
		private double bedTargetTemp;

		private List<ProgressBar> hotEndProgressBars = new List<ProgressBar>();
		private List<TextWidget> hotEndProgressBarTexts = new List<TextWidget>();
		private List<TextWidget> hotEndDoneTexts = new List<TextWidget>();
		private double[] targetHotendTemps;

		public WaitForTempPage(ISetupWizard setupWizard, string step, string instructions, double targetBedTemp, double[] targetHotendTemps)
			: base(setupWizard, step, instructions)
		{
			this.bedTargetTemp = targetBedTemp;
			this.targetHotendTemps = targetHotendTemps;

			var extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			for (int i = 0; i < targetHotendTemps.Length; i++)
			{
				var hotEndTargetTemp = targetHotendTemps[i];
				if (hotEndTargetTemp > 0)
				{
					var hotEndProgressHolder = new FlowLayoutWidget()
					{
						Margin = new BorderDouble(0, 5)
					};

					var labelText = "Hotend Temperature:".Localize();
					if (extruderCount > 1)
					{
						labelText = "Hotend {0} Temperature:".Localize().FormatWith(i + 1);
					}

					// put in bar name
					contentRow.AddChild(new TextWidget(labelText, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
					{
						AutoExpandBoundsToText = true,
						Margin = new BorderDouble(5, 0, 5, 5),
					});

					// put in the progress bar
					var hotEndProgressBar = new ProgressBar((int)(150 * GuiWidget.DeviceScale), (int)(15 * GuiWidget.DeviceScale))
					{
						FillColor = theme.PrimaryAccentColor,
						BorderColor = theme.TextColor,
						BackgroundColor = Color.White,
						Margin = new BorderDouble(3, 0, 0, 0),
						VAnchor = VAnchor.Center
					};
					hotEndProgressHolder.AddChild(hotEndProgressBar);
					hotEndProgressBars.Add(hotEndProgressBar);

					// put in the status
					var hotEndProgressBarText = new TextWidget("", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
					{
						AutoExpandBoundsToText = true,
						Margin = new BorderDouble(5, 0, 5, 5),
						VAnchor = VAnchor.Center
					};
					hotEndProgressHolder.AddChild(hotEndProgressBarText);
					hotEndProgressBarTexts.Add(hotEndProgressBarText);

					// message to show when done
					var hotEndDoneText = new TextWidget("Done!", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
					{
						AutoExpandBoundsToText = true,
						Visible = false,
					};
					hotEndProgressHolder.AddChild(hotEndDoneText);
					hotEndDoneTexts.Add(hotEndDoneText);

					contentRow.AddChild(hotEndProgressHolder);
				}
			}

			if (bedTargetTemp > 0)
			{
				var bedProgressHolder = new FlowLayoutWidget()
				{
					Margin = new BorderDouble(0, 5)
				};

				// put in bar name
				contentRow.AddChild(new TextWidget("Bed Temperature:".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
				{
					AutoExpandBoundsToText = true,
					Margin = new BorderDouble(5, 0, 5, 5),
				});

				// put in progress bar
				bedProgressBar = new ProgressBar((int)(150 * GuiWidget.DeviceScale), (int)(15 * GuiWidget.DeviceScale))
				{
					FillColor = theme.PrimaryAccentColor,
					BorderColor = theme.TextColor,
					BackgroundColor = Color.White,
					Margin = new BorderDouble(3, 0, 0, 0),
					VAnchor = VAnchor.Center
				};
				bedProgressHolder.AddChild(bedProgressBar);

				// put in status
				bedProgressBarText = new TextWidget("", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
				{
					AutoExpandBoundsToText = true,
					Margin = new BorderDouble(5, 0, 0, 0),
					VAnchor = VAnchor.Center
				};
				bedProgressHolder.AddChild(bedProgressBarText);

				// message to show when done
				bedDoneText = new TextWidget("Done!", textColor: theme.TextColor)
				{
					AutoExpandBoundsToText = true,
					Visible = false,
				};

				bedProgressHolder.AddChild(bedDoneText);

				contentRow.AddChild(bedProgressHolder);
			}
		}

		private void WizardWindow_Closed(object sender, EventArgs e)
		{
			// Make sure when the wizard closes we turn off the bed heating
			printer.Connection.TurnOffBedAndExtruders(TurnOff.AfterDelay);
			this.DialogWindow.Closed -= WizardWindow_Closed;
		}

		public override void OnLoad(EventArgs args)
		{
			// hook our parent so we can turn off the bed when we are done with leveling
			this.DialogWindow.Closed += WizardWindow_Closed;

			bedStartingTemp = printer.Connection.ActualBedTemperature;

			runningInterval = UiThread.SetInterval(ShowTempChangeProgress, 1);

			if (bedTargetTemp > 0)
			{
				// start heating the bed and show our progress
				printer.Connection.TargetBedTemperature = bedTargetTemp;
			}

			for (int i = 0; i < targetHotendTemps.Length; i++)
			{
				if (targetHotendTemps[i] > 0)
				{
					// start heating the hot end and show our progress
					printer.Connection.SetTargetHotendTemperature(i, targetHotendTemps[i]);
				}
			}

			NextButton.Enabled = false;

			// if we are trying to go to a temp of 0 than just move on to next window
			if (bedTargetTemp == 0
				&& targetHotendTemps.All(i => i == 0))
			{
				// advance to the next page
				UiThread.RunOnIdle(() => NextButton.InvokeClick());
			}

			base.OnLoad(args);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			UiThread.ClearInterval(runningInterval);

			base.OnClosed(e);
		}

		private void ShowTempChangeProgress()
		{
			int progressBarIndex = 0;
			for (int i = 0; i < targetHotendTemps.Length; i++)
			{
				if (targetHotendTemps[i] > 0)
				{
					hotEndProgressBars[progressBarIndex].Visible = true;
					double targetTemp = printer.Connection.GetTargetHotendTemperature(i);
					double actualTemp = printer.Connection.GetActualHotendTemperature(i);
					double totalDelta = targetTemp;
					double currentDelta = actualTemp;
					double ratioDone = hotEndDoneTexts[progressBarIndex].Visible ? 1 : totalDelta != 0 ? (currentDelta / totalDelta) : 1;
					hotEndProgressBars[progressBarIndex].RatioComplete = Math.Min(Math.Max(0, ratioDone), 1);
					hotEndProgressBarTexts[progressBarIndex].Text = $"{actualTemp:0} / {targetTemp:0}";

					// if we are within 1 degree of our target
					if (Math.Abs(targetTemp - actualTemp) < 2
						&& hotEndDoneTexts[progressBarIndex].Visible == false)
					{
						hotEndDoneTexts[progressBarIndex].Visible = true;
						NextButton.Enabled = true;
					}
					progressBarIndex++;
				}
			}

			if (bedTargetTemp > 0)
			{
				bedProgressBar.Visible = true;
				double targetTemp = printer.Connection.TargetBedTemperature;
				double actualTemp = printer.Connection.ActualBedTemperature;
				double totalDelta = targetTemp - bedStartingTemp;
				double currentDelta = actualTemp - bedStartingTemp;
				double ratioDone = bedDoneText.Visible ? 1 : totalDelta != 0 ? (currentDelta / totalDelta) : 1;
				bedProgressBar.RatioComplete = Math.Min(Math.Max(0, ratioDone), 1);
				bedProgressBarText.Text = $"{actualTemp:0} / {targetTemp:0}";

				// if we are within 1 degree of our target
				if (Math.Abs(targetTemp - actualTemp) < 2
					&& bedDoneText.Visible == false)
				{
					bedDoneText.Visible = true;
					NextButton.Enabled = true;
				}
			}

			if ((bedTargetTemp == 0 || bedDoneText.Visible)
				&& (targetHotendTemps.All(i => i == 0) || hotEndDoneTexts.All(i => i.Visible))
				&& !HasBeenClosed)
			{
				// advance to the next page
				UiThread.RunOnIdle(() => NextButton.InvokeClick());
			}
		}
	}
}