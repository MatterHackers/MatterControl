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
        Stopwatch timeSinceLastUpdate = new Stopwatch();
        RGBA_Bytes completeColor = new RGBA_Bytes(255, 255, 255);
        TextWidget printTimeRemaining;
        TextWidget printTimeElapsed;


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
            printTimeElapsed.AutoExpandBoundsToText = true;
            printTimeElapsed.VAnchor = Agg.UI.VAnchor.ParentCenter;


            printTimeRemaining = new TextWidget("", pointSize: 11);
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
            PrinterConnectionAndCommunication.Instance.WroteLine.RegisterEvent(Instance_WroteLine, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(Instance_PrintItemChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(Instance_PrintItemChanged, ref unregisterEvents);
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
        }

		public void onProgressBarClick(object sender, MouseEventArgs e)
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

        void Instance_WroteLine(object sender, EventArgs e)
        {
            if (!timeSinceLastUpdate.IsRunning)
            {
                timeSinceLastUpdate.Start();
            }

            if (timeSinceLastUpdate.ElapsedMilliseconds > 999)
            {
                timeSinceLastUpdate.Restart();
                currentPercent = PrinterConnectionAndCommunication.Instance.PercentComplete;
                UpdatePrintStatus();
                this.Invalidate();                
            }
        }

        void OnIdle(object state)
        {
            if (!timeSinceLastUpdate.IsRunning)
            {
                timeSinceLastUpdate.Start();
            }

            if (timeSinceLastUpdate.ElapsedMilliseconds > 999)
            {
                timeSinceLastUpdate.Restart();
                currentPercent = PrinterConnectionAndCommunication.Instance.PercentComplete;
                UpdatePrintStatus();
                
            }

            if (!WidgetHasBeenClosed)
            {
                UiThread.RunOnIdle(OnIdle);
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
