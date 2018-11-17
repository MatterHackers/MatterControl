/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class WaitForTempPage : PrinterSetupWizardPage
	{
		private ProgressBar bedProgressBar;
		private TextWidget bedProgressBarText;
		private double bedStartingTemp;
		private RunningInterval runningInterval;
		private TextWidget bedDoneText;
		private double bedTargetTemp;

		private ProgressBar hotEndProgressBar;
		private TextWidget hotEndProgressBarText;
		private TextWidget hotEndDoneText;
		private double hotEndTargetTemp;

		public WaitForTempPage(PrinterSetupWizard context,
			string step, string instructions,
			double targetBedTemp, double targetHotendTemp)
			: base(context, step, instructions)
		{
			this.bedTargetTemp = targetBedTemp;
			this.hotEndTargetTemp = targetHotendTemp;

			if (hotEndTargetTemp > 0)
			{
				var hotEndProgressHolder = new FlowLayoutWidget()
				{
					Margin = new BorderDouble(0, 5)
				};

				// put in bar name
				contentRow.AddChild(new TextWidget("Hotend Temperature:".Localize(), pointSize: 10, textColor: theme.TextColor)
				{
					AutoExpandBoundsToText = true,
					Margin = new BorderDouble(5, 0, 5, 5),
				});

				// put in the progress bar
				hotEndProgressBar = new ProgressBar((int)(150 * GuiWidget.DeviceScale), (int)(15 * GuiWidget.DeviceScale))
				{
					FillColor = theme.PrimaryAccentColor,
					BorderColor = theme.TextColor,
					BackgroundColor = Color.White,
					Margin = new BorderDouble(3, 0, 0, 0),
					VAnchor = VAnchor.Center
				};
				hotEndProgressHolder.AddChild(hotEndProgressBar);

				// put in the status
				hotEndProgressBarText = new TextWidget("", pointSize: 10, textColor: theme.TextColor)
				{
					AutoExpandBoundsToText = true,
					Margin = new BorderDouble(5, 0, 5, 5),
					VAnchor = VAnchor.Center
				};
				hotEndProgressHolder.AddChild(hotEndProgressBarText);

				// message to show when done
				hotEndDoneText = new TextWidget("Done!", textColor: theme.TextColor)
				{
					AutoExpandBoundsToText = true,
					Visible = false,
				};

				hotEndProgressHolder.AddChild(hotEndDoneText);

				contentRow.AddChild(hotEndProgressHolder);
			}

			if (bedTargetTemp > 0)
			{
				var bedProgressHolder = new FlowLayoutWidget()
				{
					Margin = new BorderDouble(0, 5)
				};

				// put in bar name
				contentRow.AddChild(new TextWidget("Bed Temperature:".Localize(), pointSize: 10, textColor: theme.TextColor)
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
				bedProgressBarText = new TextWidget("", pointSize: 10, textColor: theme.TextColor)
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

		public override void OnLoad(EventArgs args)
		{
			// hook our parent so we can turn off the bed when we are done with leveling
			this.DialogWindow.Closed += WizardWindow_Closed;
			base.OnLoad(args);
		}

		private void WizardWindow_Closed(object sender, EventArgs e)
		{
			// Make sure when the wizard closes we turn off the bed heating
			printer.Connection.TurnOffBedAndExtruders(TurnOff.AfterDelay);
			this.DialogWindow.Closed -= WizardWindow_Closed;
		}

		public override void PageIsBecomingActive()
		{
			bedStartingTemp = printer.Connection.ActualBedTemperature;

			runningInterval = UiThread.SetInterval(ShowTempChangeProgress, 1);

			if (bedTargetTemp > 0)
			{
				// start heating the bed and show our progress
				printer.Connection.TargetBedTemperature = bedTargetTemp;
			}

			if (hotEndTargetTemp > 0)
			{
				// start heating the hot end and show our progress
				printer.Connection.SetTargetHotendTemperature(0, hotEndTargetTemp);
			}

			NextButton.Enabled = false;

			// if we are trying to go to a temp of 0 than just move on to next window
			if(bedTargetTemp == 0 && hotEndTargetTemp == 0)
			{
				// advance to the next page
				UiThread.RunOnIdle(() => NextButton.InvokeClick());
			}

			base.PageIsBecomingActive();
		}

		public override void PageIsBecomingInactive()
		{
			NextButton.Enabled = true;

			base.PageIsBecomingInactive();
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			UiThread.ClearInterval(runningInterval);

			base.OnClosed(e);
		}

		private void ShowTempChangeProgress()
		{
			if (hotEndTargetTemp > 0)
			{
				hotEndProgressBar.Visible = true;
				double targetTemp = printer.Connection.GetTargetHotendTemperature(0);
				double actualTemp = printer.Connection.GetActualHotendTemperature(0);
				double totalDelta = targetTemp;
				double currentDelta = actualTemp;
				double ratioDone = hotEndDoneText.Visible ? 1 : totalDelta != 0 ? (currentDelta / totalDelta) : 1;
				hotEndProgressBar.RatioComplete = Math.Min(Math.Max(0, ratioDone), 1);
				hotEndProgressBarText.Text = $"{actualTemp:0} / {targetTemp:0}";

				// if we are within 1 degree of our target
				if (Math.Abs(targetTemp - actualTemp) < 2
					&& hotEndDoneText.Visible == false)
				{
					hotEndDoneText.Visible = true;
					NextButton.Enabled = true;
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
				&& (hotEndTargetTemp == 0 || hotEndDoneText.Visible)
				&& !HasBeenClosed)
			{
				// advance to the next page
				UiThread.RunOnIdle(() => NextButton.InvokeClick());
			}
		}
	}
}