/*
Copyright (c) 2015, Kevin Pope
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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class PrintProgressBar : GuiWidget
	{
		private double currentPercent = 0;
		private RGBA_Bytes completeColor = new RGBA_Bytes(255, 255, 255);
		private TextWidget printTimeRemaining;
		private TextWidget printTimeElapsed;
		private EventHandler unregisterEvents;
		private bool widgetIsExtended;
		private Agg.Image.ImageBuffer upImageBuffer;
		private Agg.Image.ImageBuffer downImageBuffer;
		private ImageWidget indicatorWidget;

		public bool WidgetIsExtended
		{
			get { return widgetIsExtended; }

			set
			{
				widgetIsExtended = value;
				ToggleExtendedDisplayProperties();
			}
		}

		private void ToggleExtendedDisplayProperties()
		{
			if (!WidgetIsExtended)
			{
				indicatorWidget.Image = downImageBuffer;
			}
			else
			{
				indicatorWidget.Image = upImageBuffer;
			}
		}

		public PrintProgressBar(bool widgetIsExtended = true)
		{
			MinimumSize = new Vector2(0, 24);

			HAnchor = HAnchor.ParentLeftRight;
			BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
			Margin = new BorderDouble(0);

			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.LeftToRight);
			container.AnchorAll();
			container.Padding = new BorderDouble(6, 0);

			printTimeElapsed = new TextWidget("", pointSize: 11);
			printTimeElapsed.Printer.DrawFromHintedCache = true;
			printTimeElapsed.AutoExpandBoundsToText = true;
			printTimeElapsed.VAnchor = VAnchor.ParentCenter;

			printTimeRemaining = new TextWidget("", pointSize: 11);
			printTimeRemaining.Printer.DrawFromHintedCache = true;
			printTimeRemaining.AutoExpandBoundsToText = true;
			printTimeRemaining.VAnchor = VAnchor.ParentCenter;

			container.AddChild(printTimeElapsed);
			container.AddChild(new HorizontalSpacer());
			container.AddChild(printTimeRemaining);

			AddChild(container);

			if (UserSettings.Instance.IsTouchScreen)
			{
				upImageBuffer = StaticData.Instance.LoadIcon("TouchScreen/arrow_up_32x24.png");
				downImageBuffer = StaticData.Instance.LoadIcon("TouchScreen/arrow_down_32x24.png");

				indicatorWidget = new ImageWidget(upImageBuffer);
				indicatorWidget.HAnchor = HAnchor.ParentCenter;
				indicatorWidget.VAnchor = VAnchor.ParentCenter;

				WidgetIsExtended = widgetIsExtended;

				GuiWidget indicatorOverlay = new GuiWidget();
				indicatorOverlay.AnchorAll();
				indicatorOverlay.AddChild(indicatorWidget);

				AddChild(indicatorOverlay);
			}

			var clickOverlay = new GuiWidget();
			clickOverlay.AnchorAll();
			clickOverlay.Click += (s, e) =>
			{
				// In touchscreen mode, expand or collapse the print status row when clicked
				ApplicationView mainView = ApplicationController.Instance.MainView;
				if(mainView is TouchscreenView)
				{
					((TouchscreenView)mainView).ToggleTopContainer();
				}
			};
			AddChild(clickOverlay);

			PrinterConnection.Instance.ActivePrintItemChanged.RegisterEvent(Instance_PrintItemChanged, ref unregisterEvents);
			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(Instance_PrintItemChanged, ref unregisterEvents);

			SetThemedColors();

			// This is a bit of a hack. Now that we have the printing window we don't want this to show progress but it is still used on touch screen for expanding the display.
			if (!UserSettings.Instance.IsTouchScreen)
			{
				UiThread.RunOnIdle(OnIdle);
				UpdatePrintStatus();
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void SetThemedColors()
		{
			this.printTimeElapsed.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
			this.printTimeRemaining.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
			this.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			//Set background color to new theme
			SetThemedColors();
			this.Invalidate();
		}

		private void Instance_PrintItemChanged(object sender, EventArgs e)
		{
			UpdatePrintStatus();
		}

		private void OnIdle()
		{
			currentPercent = PrinterConnection.Instance.PercentComplete;
			UpdatePrintStatus();

			if (!HasBeenClosed)
			{
				UiThread.RunOnIdle(OnIdle, 1);
			}
		}

		private void UpdatePrintStatus()
		{
			if (PrinterConnection.Instance.ActivePrintItem == null)
			{
				printTimeElapsed.Text = string.Format("");
				printTimeRemaining.Text = string.Format("");
			}
			else
			{
				int secondsPrinted = PrinterConnection.Instance.SecondsPrinted;
				int hoursPrinted = (int)(secondsPrinted / (60 * 60));
				int minutesPrinted = (int)(secondsPrinted / 60 - hoursPrinted * 60);
				secondsPrinted = secondsPrinted % 60;

				if (secondsPrinted > 0)
				{
					if (hoursPrinted > 0)
					{
						printTimeElapsed.Text = string.Format("{0}:{1:00}:{2:00}",
							hoursPrinted,
							minutesPrinted,
							secondsPrinted);
					}
					else
					{
						printTimeElapsed.Text = string.Format("{0}:{1:00}",
							minutesPrinted,
							secondsPrinted);
					}
				}
				else
				{
					printTimeElapsed.Text = string.Format("");
				}

				string printPercentRemainingText = string.Format("{0:0.0}%", currentPercent);

				if (PrinterConnection.Instance.PrinterIsPrinting || PrinterConnection.Instance.PrinterIsPaused)
				{
					printTimeRemaining.Text = printPercentRemainingText;
				}
				else if (PrinterConnection.Instance.PrintIsFinished)
				{
					printTimeRemaining.Text = "Done!";
				}
				else
				{
					printTimeRemaining.Text = string.Format("");
				}
			}
			this.Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.FillRectangle(0, 0, Width * currentPercent / 100, Height, completeColor);

			base.OnDraw(graphics2D);
		}
	}
}