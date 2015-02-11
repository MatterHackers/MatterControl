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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl
{    
    public class PrintProgressBar : GuiWidget
    {
        double currentPercent = 0;
        RGBA_Bytes completeColor = new RGBA_Bytes(255, 255, 255);
        TextWidget printTimeRemaining;
        TextWidget printTimeElapsed;

        public bool WidgetIsExtended { get; set; }

        public PrintProgressBar()
        {
            MinimumSize = new Vector2(0, 24);
            HAnchor = HAnchor.ParentLeftRight;
            BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
            Margin = new BorderDouble(0);

            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.LeftToRight);
            container.AnchorAll();
            container.Padding = new BorderDouble(6,0);

            printTimeElapsed = new TextWidget("", pointSize:11);
			printTimeElapsed.Printer.DrawFromHintedCache = true;
            printTimeElapsed.AutoExpandBoundsToText = true;
            printTimeElapsed.VAnchor = Agg.UI.VAnchor.ParentCenter;


            printTimeRemaining = new TextWidget("", pointSize: 11);
			printTimeRemaining.Printer.DrawFromHintedCache = true;
			printTimeRemaining.AutoExpandBoundsToText = true;
            printTimeRemaining.VAnchor = Agg.UI.VAnchor.ParentCenter;

            GuiWidget spacer = new GuiWidget();
            spacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

            container.AddChild(printTimeElapsed);
            container.AddChild(spacer);
            container.AddChild(printTimeRemaining);

            AddChild(container);

			ClickWidget clickOverlay = new ClickWidget();
			clickOverlay.AnchorAll();
			clickOverlay.Click += onProgressBarClick;

			AddChild(clickOverlay);

            AddHandlers();
            SetThemedColors();
            UpdatePrintStatus();            
            UiThread.RunOnIdle(OnIdle);
        }


        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(Instance_PrintItemChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(Instance_PrintItemChanged, ref unregisterEvents);
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
        }

        public void onProgressBarClick(object sender, EventArgs e)
		{
			ApplicationController.Instance.MainView.ToggleTopContainer();
		}

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
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

        void Instance_PrintItemChanged(object sender, EventArgs e)
        {
            UpdatePrintStatus();
        }

        void OnIdle(object state)
        {
            currentPercent = PrinterConnectionAndCommunication.Instance.PercentComplete;
            UpdatePrintStatus();

            if (!WidgetHasBeenClosed)
            {
                UiThread.RunOnIdle(OnIdle, 1);
            }
        }

        private void UpdatePrintStatus()
        {
            if (PrinterConnectionAndCommunication.Instance.ActivePrintItem == null)
            {
                printTimeElapsed.Text = string.Format("");
                printTimeRemaining.Text = string.Format("");
            }
            else
            {
                int secondsPrinted = PrinterConnectionAndCommunication.Instance.SecondsPrinted;
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

                if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting || PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
                {
                    printTimeRemaining.Text = printPercentRemainingText;
                }
                else if (PrinterConnectionAndCommunication.Instance.PrintIsFinished)
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
