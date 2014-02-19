﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

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
            MinimumSize = new Vector2(0, 30);
            HAnchor = HAnchor.ParentLeftRight;
            BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
            Margin = new BorderDouble(0);

            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.LeftToRight);
            container.AnchorAll();
            container.Padding = new BorderDouble(7,0);

            RGBA_Bytes labelColor = ActiveTheme.Instance.PrimaryAccentColor;
            //labelColor.alpha = 220;

            printTimeElapsed = new TextWidget("2:30:00");
            printTimeElapsed.VAnchor = Agg.UI.VAnchor.ParentCenter;
            printTimeElapsed.TextColor = labelColor;
            

            printTimeRemaining = new TextWidget("4:50:30");
            printTimeRemaining.VAnchor = Agg.UI.VAnchor.ParentCenter;
            printTimeRemaining.TextColor = labelColor;

            GuiWidget spacer = new GuiWidget();
            spacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

            container.AddChild(printTimeElapsed);
            container.AddChild(spacer);
            container.AddChild(printTimeRemaining);

            AddChild(container);
            AddHandlers();
            UpdatePrintStatus();
            UiThread.RunOnIdle(OnIdle);
        }


        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            PrinterCommunication.Instance.WroteLine.RegisterEvent(Instance_WroteLine, ref unregisterEvents);
            PrinterCommunication.Instance.ActivePrintItemChanged.RegisterEvent(Instance_PrintItemChanged, ref unregisterEvents);
            PrinterCommunication.Instance.ConnectionStateChanged.RegisterEvent(Instance_PrintItemChanged, ref unregisterEvents);
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            //Set background color to new theme
            this.printTimeElapsed.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
            this.printTimeRemaining.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
            this.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
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
                currentPercent = PrinterCommunication.Instance.PercentComplete;
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
                currentPercent = PrinterCommunication.Instance.PercentComplete;
                UpdatePrintStatus();
                
            }
            UiThread.RunOnIdle(OnIdle);
        }

        private void UpdatePrintStatus()
        {
            if (PrinterCommunication.Instance.ActivePrintItem == null)
            {
                printTimeElapsed.Text = string.Format("");
                printTimeRemaining.Text = string.Format("");
            }

            else
            {
                int secondsPrinted = PrinterCommunication.Instance.SecondsPrinted;
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

                int secondsRemaining = PrinterCommunication.Instance.SecondsRemaining;
                int hoursRemaining = (int)(secondsRemaining / (60 * 60));
                int minutesRemaining = (int)(secondsRemaining / 60 - hoursRemaining * 60);
                secondsRemaining = secondsRemaining % 60;

                if (secondsRemaining > 0)
                {
                    if (hoursRemaining > 0)
                    {
                        printTimeRemaining.Text = string.Format("{0}:{1:00}:{2:00}",
                            hoursRemaining,
                            minutesRemaining,
                            secondsRemaining);
                    }
                    else
                    {
                        printTimeRemaining.Text = string.Format("{0}:{1:00}",
                            minutesRemaining,
                            secondsRemaining);
                    }
                }
                else if (PrinterCommunication.Instance.PrintIsFinished)
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
