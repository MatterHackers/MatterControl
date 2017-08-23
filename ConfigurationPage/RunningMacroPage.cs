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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class RunningMacroPage : WizardPage
	{
		private long startTimeMs;
		private ProgressBar progressBar;

		private TextWidget progressBarText;

		private long timeToWaitMs;

		public RunningMacroPage(MacroCommandData macroData)
					: base("Cancel", macroData.title)
		{
			this.WindowTitle = "Running Macro".Localize();

			cancelButton.Click += (s, e) =>
			{
				PrinterConnection.Instance.MacroCancel();
			};

			if (macroData.showMaterialSelector)
			{
				int extruderIndex = 0;
				var materialSelector = new PresetSelectorWidget(string.Format($"{"Material".Localize()} {extruderIndex + 1}"), RGBA_Bytes.Transparent, NamedSettingsLayers.Material, extruderIndex);
				materialSelector.BackgroundColor = RGBA_Bytes.Transparent;
				materialSelector.Margin = new BorderDouble(0, 0, 0, 15);
				contentRow.AddChild(materialSelector);
			}

			PrinterConnection.Instance.WroteLine.RegisterEvent(LookForTempRequest, ref unregisterEvents);

			if (macroData.waitOk | macroData.expireTime > 0)
			{
				Button okButton = textImageButtonFactory.Generate("Continue".Localize());

				okButton.Click += (s, e) =>
				{
					PrinterConnection.Instance.MacroContinue();
					UiThread.RunOnIdle(() => WizardWindow?.Close());
				};

				footerRow.AddChild(okButton);
			}

			if (macroData.image != null)
			{
				var imageWidget = new ImageWidget(macroData.image)
				{
					HAnchor = HAnchor.Center,
					Margin = new BorderDouble(5,15),
				};

				contentRow.AddChild(imageWidget);
			}

			var holder = new FlowLayoutWidget();
			progressBar = new ProgressBar((int)(150 * GuiWidget.DeviceScale), (int)(15 * GuiWidget.DeviceScale))
			{
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
				BorderColor = ActiveTheme.Instance.PrimaryTextColor,
				BackgroundColor = RGBA_Bytes.White,
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

			if (macroData.countDown > 0)
			{
				timeToWaitMs = (long)(macroData.countDown * 1000);
				startTimeMs = UiThread.CurrentTimerMs;
				UiThread.RunOnIdle(CountDownTime);
			}

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}

		private EventHandler unregisterEvents;

		public class MacroCommandData
		{
			public bool waitOk = false;
			public string title = "";
			public bool showMaterialSelector = false;
			public double countDown = 0;
			public double expireTime = 0;
			public double expectedTemperature = 0;
			public ImageBuffer image = null;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if(e.OsEvent)
			{
				PrinterConnection.Instance.MacroCancel();
			}
			unregisterEvents?.Invoke(this, null);

			base.OnClosed(e);
		}

		private void CountDownTime()
		{
			progressBar.Visible = true;
			long timeSinceStartMs = UiThread.CurrentTimerMs - startTimeMs;
			progressBar.RatioComplete = timeToWaitMs == 0 ? 1 : Math.Max(0, Math.Min(1, ((double)timeSinceStartMs / (double)timeToWaitMs)));
			int seconds = (int)((timeToWaitMs - (timeToWaitMs * (progressBar.RatioComplete))) / 1000);
			progressBarText.Text = $"Time Remaining: {seconds / 60:#0}:{seconds % 60:00}";
			if (!HasBeenClosed && progressBar.RatioComplete < 1)
			{
				UiThread.RunOnIdle(CountDownTime, .2);
			}
		}

		double startingTemp;
		private void LookForTempRequest(object sender, EventArgs e)
		{
			var stringEvent = e as StringEventArgs;
			if(stringEvent != null
				&& stringEvent.Data.Contains("M104"))
			{
				startingTemp = PrinterConnection.Instance.GetActualExtruderTemperature(0);
				UiThread.RunOnIdle(ShowTempChangeProgress);
			}
		}

		private void ShowTempChangeProgress()
		{
			progressBar.Visible = true;
			double targetTemp = PrinterConnection.Instance.GetTargetExtruderTemperature(0);
			double actualTemp = PrinterConnection.Instance.GetActualExtruderTemperature(0);
			double totalDelta = targetTemp - startingTemp;
			double currentDelta = actualTemp - startingTemp;
			double ratioDone = totalDelta != 0 ? (currentDelta / totalDelta) : 1;
			progressBar.RatioComplete = Math.Min(Math.Max(0, ratioDone), 1);
			progressBarText.Text = $"Temperature: {actualTemp:0} / {targetTemp:0}";
			if (!HasBeenClosed && ratioDone < 1)
			{
				UiThread.RunOnIdle(ShowTempChangeProgress, 1);
			}
		}
	}
}