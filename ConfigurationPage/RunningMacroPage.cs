/*
Copyright (c) 2016, Lars Brubaker
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
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class RunningMacroPage : WizardPage
	{
		private long endTimeMs;
		private ProgressBar progressBar;

		private TextWidget progressBarText;

		private long timeToWaitMs;

		public RunningMacroPage(string message, bool showOkButton, bool showMaterialSelector, double expectedSeconds, double expectedTemperature)
					: base("Close", "Macro Feedback")
		{
			TextWidget syncingText = new TextWidget(message, textColor: ActiveTheme.Instance.PrimaryTextColor);
			contentRow.AddChild(syncingText);

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			if (showMaterialSelector)
			{
				int extruderIndex = 0;
				contentRow.AddChild(new PresetSelectorWidget(string.Format($"{"Material".Localize()} {extruderIndex + 1}"), RGBA_Bytes.Orange, NamedSettingsLayers.Material, extruderIndex));
			}

			var holder = new FlowLayoutWidget();
			progressBar = new ProgressBar((int)(150 * GuiWidget.DeviceScale), (int)(15 * GuiWidget.DeviceScale))
			{
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
				BorderColor = ActiveTheme.Instance.PrimaryTextColor,
				//HAnchor = HAnchor.ParentCenter,
				Margin = new BorderDouble(3, 0, 0, 10),
			};
			progressBarText = new TextWidget("", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(5, 0, 0, 0),
			};
			holder.AddChild(progressBar);
			holder.AddChild(progressBarText);
			contentRow.AddChild(holder);
			progressBar.Visible = false;

			if (expectedSeconds > 0)
			{
				timeToWaitMs = (long)(expectedSeconds * 1000);
				endTimeMs = UiThread.CurrentTimerMs + timeToWaitMs;
				UiThread.RunOnIdle(CountDownTime, 1);
				progressBar.Visible = true;
			}

			PrinterConnectionAndCommunication.Instance.WroteLine.RegisterEvent(LookForTempRequest, ref unregisterEvents);

			if (showOkButton)
			{
				Button okButton = textImageButtonFactory.Generate("Continue".Localize());
				okButton.Margin = new BorderDouble(0, 0, 0, 25);
				okButton.HAnchor = HAnchor.ParentCenter;

				okButton.Click += (s, e) =>
				{
					PrinterConnectionAndCommunication.Instance.MacroContinue();
					UiThread.RunOnIdle(() => WizardWindow?.Close());
				};

				contentRow.AddChild(okButton);
			}
		}

		private EventHandler unregisterEvents;

		public static void Show(string message, bool showOkButton = false, bool showMaterialSelector = false, double expectedSeconds = 0, double expectedTemperature = 0)
		{
			WizardWindow.Show("Macro", "Running Macro", new RunningMacroPage(message, showOkButton, showMaterialSelector, expectedSeconds, expectedTemperature));
		}

		public override void OnClosed(EventArgs e)
		{
			PrinterConnectionAndCommunication.Instance.MacroContinue();
			unregisterEvents?.Invoke(this, null);

			base.OnClosed(e);
		}

		private void CountDownTime()
		{
			long timeWaitedMs = endTimeMs - UiThread.CurrentTimerMs;
			double ratioDone = 1 - timeToWaitMs != 0 ? ((double)timeWaitedMs / (double)timeToWaitMs) : 1;
			progressBar.RatioComplete = Math.Min(Math.Max(0, ratioDone), 1);
			int seconds = (int)((timeToWaitMs * (1 - ratioDone)) / 1000);
			progressBarText.Text = $"Time Remaining: {seconds / 60:#0}:{seconds % 60:00}";
			if (!HasBeenClosed && ratioDone < 1)
			{
				UiThread.RunOnIdle(CountDownTime, 1);
			}
		}

		double startingTemp;
		private void LookForTempRequest(object sender, EventArgs e)
		{
			var stringEvent = e as StringEventArgs;
			if(stringEvent != null
				&& stringEvent.Data.Contains("M104"))
			{
				startingTemp = PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(0);
				UiThread.RunOnIdle(ShowTempChangeProgress, 1);
				progressBar.Visible = true;
			}
		}

		private void ShowTempChangeProgress()
		{
			double totalDelta = PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(0) - startingTemp;
			double currentDelta = PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(0) - startingTemp;
			double ratioDone = totalDelta != 0 ? (currentDelta / totalDelta) : 1;
			progressBar.RatioComplete = Math.Min(Math.Max(0, ratioDone), 1);
			//progressBarText.Text = $"Time Remaining: {seconds / 60:#0}:{seconds % 60:00}";
			if (!HasBeenClosed && ratioDone < 1)
			{
				UiThread.RunOnIdle(ShowTempChangeProgress, 1);
			}
		}
	}
}