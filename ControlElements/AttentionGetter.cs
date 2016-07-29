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
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl
{
	public static class UiNavigation
	{
		public static void OpenEditPrinterWizard_Click(object sender, EventArgs e)
		{
			Button editButton = sender as Button;
			editButton.ToolTipText = "Edit Current Printer Settings".Localize();
			if (editButton != null)
			{
				editButton.Closed += (s, e2) =>
				{
					editButton.Click -= OpenEditPrinterWizard_Click;
				};

				UiNavigation.OpenEditPrinterWizard("baud_rate Edit Field,auto_connect Edit Field,com_port Edit Field");
			}
		}

		public static void OpenEditPrinterWizard(string widgetNameToHighlight)
		{
			if (PrinterConnectionAndCommunication.Instance?.ActivePrinter?.ID != null
				&& ActiveSliceSettings.Instance.PrinterSelected
				&& !WizardWindow.IsOpen("PrinterSetup"))
			{
				UiThread.RunOnIdle(() =>
				{
					WizardWindow.Show<EditPrinterSettingsPage>("EditSettings", "Edit Printer Settings");
				});
			}
		}

		private static void HighlightWidget(AutomationRunner testRunner, string widgetNameToHighlight)
		{
			SystemWindow containingWindow;
			var autoLevelRowItem = testRunner.GetWidgetByName(widgetNameToHighlight, out containingWindow, .2);
			if (autoLevelRowItem != null)
			{
				AttentionGetter.GetAttention(autoLevelRowItem);
			}
		}
	}

	public class AttentionGetter
	{
		private static HashSet<GuiWidget> runningAttentions = new HashSet<GuiWidget>();
		private double animationDelay = 1 / 20.0;
		private int cycles = 1;
		private double lightnessChange = 1;
		private double pulseTime = 1.38;
		private RGBA_Bytes startColor;
		private Stopwatch timeSinceStart = null;
		private GuiWidget widgetToHighlight;

		private AttentionGetter(GuiWidget widgetToHighlight)
		{
			this.widgetToHighlight = widgetToHighlight;
			widgetToHighlight.AfterDraw += ConnectToWidget;
		}

		public static AttentionGetter GetAttention(GuiWidget widgetToHighlight)
		{
			if(!runningAttentions.Contains(widgetToHighlight))
			{
				runningAttentions.Add(widgetToHighlight);
				return new AttentionGetter(widgetToHighlight);
			}

			return null;
		}

		private void ChangeBackgroundColor()
		{
			if (widgetToHighlight != null)
			{
				double time = timeSinceStart.Elapsed.TotalSeconds;
				while (time > pulseTime)
				{
					time -= pulseTime;
				}
				time = time * 2 / pulseTime;
				if (time > 1)
				{
					time = 1 - (time - 1);
				}

				double lightnessMultiplier = EaseInOutQuad(time);

				widgetToHighlight.BackgroundColor = startColor.AdjustLightness(1 + lightnessChange * lightnessMultiplier).GetAsRGBA_Bytes();
				if (widgetToHighlight.HasBeenClosed || timeSinceStart.Elapsed.TotalSeconds > cycles * pulseTime)
				{
					widgetToHighlight.BackgroundColor = startColor;
					widgetToHighlight.AfterDraw -= ConnectToWidget;
					runningAttentions.Remove(widgetToHighlight);
					widgetToHighlight = null;
					return;
				}
				UiThread.RunOnIdle(ChangeBackgroundColor, animationDelay);
			}
		}

		private double EaseInOutQuad(double t)
		{
			if (t <= 0.5f)
			{
				return 2.0f * (t * t);
			}

			t -= 0.5f;
			return 2.0f * t * (1.0f - t) + 0.5;
		}

		private void ConnectToWidget(GuiWidget drawingWidget, DrawEventArgs e)
		{
			GuiWidget parent = drawingWidget;
			while (parent.BackgroundColor.Alpha0To255 == 0)
			{
				parent = parent.Parent;
			}
			startColor = parent.BackgroundColor;
			timeSinceStart = Stopwatch.StartNew();
			widgetToHighlight.AfterDraw -= ConnectToWidget;
			UiThread.RunOnIdle(ChangeBackgroundColor, animationDelay);
		}
	}
}