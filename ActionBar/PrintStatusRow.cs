/*
Copyright (c) 2014, Kevin Pope
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ActionBar
{
    public class PrintStatusRow : FlowLayoutWidget
    {
        Stopwatch timeSinceLastDrawTime = new Stopwatch();
        event EventHandler unregisterEvents;

        TextWidget activePrintName;
        TextWidget activePrintLabel;
        TextWidget activePrintInfo;
        TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

        TextWidget activePrintStatus;

        QueueDataView queueDataView;
        PartThumbnailWidget activePrintPreviewImage;

        public PrintStatusRow(QueueDataView queueDataView)
        {
            Initialize();
            
            this.HAnchor = HAnchor.ParentLeftRight;

            AddChildElements();
            AddHandlers();

            this.queueDataView = queueDataView;

            onActivePrintItemChanged(null, null);
        }

        string ActivePrintStatusText
        {
            set 
            {
                if (activePrintStatus.Text != value)
                {
                    activePrintStatus.Text = value;
                }
            }
        }

        protected void Initialize()
        {
            UiThread.RunOnIdle(OnIdle);
            this.Margin = new BorderDouble(6, 3, 6, 6);
        }

        void onActivePrintItemChanged(object sender, EventArgs e)
        {
            // first we have to remove any link to an old part (the part currently in the view)
            if (activePrintPreviewImage.PrintItem != null)
            {
                activePrintPreviewImage.PrintItem.SlicingOutputMessage -= PrintItem_SlicingOutputMessage;
            }

            activePrintPreviewImage.PrintItem = PrinterCommunication.Instance.ActivePrintItem;

            // then hook up our new part
            if (activePrintPreviewImage.PrintItem != null)
            {
                activePrintPreviewImage.PrintItem.SlicingOutputMessage += PrintItem_SlicingOutputMessage;
            }

            activePrintPreviewImage.Invalidate();
        }

        void PrintItem_SlicingOutputMessage(object sender, EventArgs e)
        {
            StringEventArgs message = e as StringEventArgs;
            ActivePrintStatusText = message.Data;
        }

        static FlowLayoutWidget iconContainer;
        public delegate void OpenNotificationsWindow();
        public static OpenNotificationsWindow openNotificationsWindowFunction = null;
        public static OpenNotificationsWindow OpenNotificationsWindowFunction 
        {
            get { return openNotificationsWindowFunction; }
            set
            {
                openNotificationsWindowFunction = value;
                AddNotificationButton(iconContainer);
            }
        }

        TemperatureWidgetBase extruderTemperatureWidget;
        TemperatureWidgetBase bedTemperatureWidget;
        void AddChildElements()
        {            
            activePrintPreviewImage = new PartThumbnailWidget(null, "part_icon_transparent_100x100.png", "building_thumbnail_100x100.png", new Vector2(115, 115));
            activePrintPreviewImage.VAnchor = VAnchor.ParentTop;
            activePrintPreviewImage.Padding = new BorderDouble(0);
            activePrintPreviewImage.HoverBackgroundColor = new RGBA_Bytes();
            activePrintPreviewImage.BorderWidth = 3;

            FlowLayoutWidget temperatureWidgets = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {
                extruderTemperatureWidget = new TemperatureWidgetExtruder();
                bedTemperatureWidget = new TemperatureWidgetBed();

                temperatureWidgets.AddChild(extruderTemperatureWidget);
                temperatureWidgets.AddChild(bedTemperatureWidget);
            }            
            temperatureWidgets.VAnchor |= VAnchor.ParentTop;
            temperatureWidgets.Margin = new BorderDouble(left: 6);

            FlowLayoutWidget printStatusContainer = CreateActivePrinterInfoWidget();
            printStatusContainer.VAnchor |= VAnchor.ParentTop;

            iconContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            iconContainer.Name = "PrintStatusRow.IconContainer";
            iconContainer.VAnchor |= VAnchor.ParentTop;
            iconContainer.Margin = new BorderDouble(top: 3);
            if (OpenNotificationsWindowFunction != null)
            {
                AddNotificationButton(iconContainer);
            }
            iconContainer.AddChild(GetAutoLevelIndicator());

            this.AddChild(activePrintPreviewImage);
            this.AddChild(printStatusContainer);
            this.AddChild(iconContainer);
            this.AddChild(temperatureWidgets);

            UpdatePrintStatus();
            UpdatePrintItemName();
        }

        private void SetVisibleStatus()
        {
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                if (ActivePrinterProfile.Instance.ActivePrinter.GetFeatures().HasHeatedBed())
                {
                    bedTemperatureWidget.Visible = true;
                }
                else
                {
                    bedTemperatureWidget.Visible = false;
                }
            }
        }

        private static void AddNotificationButton(FlowLayoutWidget iconContainer)
        {
            ImageButtonFactory imageButtonFactory = new ImageButtonFactory();
            imageButtonFactory.invertImageColor = false;
            string notifyIconPath = Path.Combine("Icons", "PrintStatusControls", "notify.png");
            string notifyHoverIconPath = Path.Combine("Icons", "PrintStatusControls", "notify-hover.png");
            Button notifyButton = imageButtonFactory.Generate(notifyIconPath, notifyHoverIconPath);
            notifyButton.Cursor = Cursors.Hand;
            notifyButton.Margin = new Agg.BorderDouble(top: 3);
            notifyButton.Click += (sender, mouseEvent) => { OpenNotificationsWindowFunction(); };
            notifyButton.MouseEnterBounds += (sender, mouseEvent) => { HelpTextWidget.Instance.ShowHoverText("Edit notification settings"); };
            notifyButton.MouseLeaveBounds += (sender, mouseEvent) => { HelpTextWidget.Instance.HideHoverText(); };

            iconContainer.AddChild(notifyButton);
        }

        private Button GetAutoLevelIndicator()
        {
            ImageButtonFactory imageButtonFactory = new ImageButtonFactory();
			string notifyIconPath = Path.Combine("Icons", "PrintStatusControls", "leveling-16x16.png");
			string notifyHoverIconPath = Path.Combine("Icons", "PrintStatusControls", "leveling-16x16.png");
            Button notifyButton = imageButtonFactory.Generate(notifyIconPath, notifyHoverIconPath);
            notifyButton.Cursor = Cursors.Hand;
            notifyButton.Margin = new Agg.BorderDouble(top: 3);
            notifyButton.MouseEnterBounds += (sender, mouseEvent) => { HelpTextWidget.Instance.ShowHoverText("Print leveling is enabled."); };
            notifyButton.MouseLeaveBounds += (sender, mouseEvent) => { HelpTextWidget.Instance.HideHoverText(); };
            notifyButton.Visible = ActivePrinterProfile.Instance.DoPrintLeveling;

            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent((sender, e) =>
            {
                notifyButton.Visible = ActivePrinterProfile.Instance.DoPrintLeveling;

            }, ref unregisterEvents);

            ActivePrinterProfile.Instance.DoPrintLevelingChanged.RegisterEvent((sender, e) =>
            {
                notifyButton.Visible = ActivePrinterProfile.Instance.DoPrintLeveling;

            }, ref unregisterEvents);

            return notifyButton;
        }

        private FlowLayoutWidget CreateActivePrinterInfoWidget()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(6, 0,6,3);
            container.HAnchor = HAnchor.ParentLeftRight;
            container.VAnchor |= VAnchor.ParentTop;

            FlowLayoutWidget topRow = new FlowLayoutWidget();
            topRow.Name = "PrintStatusRow.ActivePrinterInfo.TopRow";
            topRow.HAnchor = HAnchor.ParentLeftRight;

			string nextPrintLabel = LocalizedString.Get("Next Print");
			string nextPrintLabelFull = string.Format("{0}:", nextPrintLabel);       
			activePrintLabel = getPrintStatusLabel(nextPrintLabelFull, pointSize: 11);
            activePrintLabel.VAnchor = VAnchor.ParentTop;

            topRow.AddChild(activePrintLabel);

			activePrintName = getPrintStatusLabel("this is the biggest name we will allow", pointSize: 14);
            activePrintName.AutoExpandBoundsToText = false;
			activePrintStatus = getPrintStatusLabel("this is the biggest label we will allow - bigger", pointSize: 11);
            activePrintStatus.AutoExpandBoundsToText = false;
            activePrintStatus.Text = "";
            activePrintStatus.Margin = new BorderDouble(top: 3);

            activePrintInfo = getPrintStatusLabel("", pointSize: 11);
            activePrintInfo.AutoExpandBoundsToText = true;

            PrintActionRow printActionRow = new PrintActionRow(queueDataView);

            container.AddChild(topRow);
            container.AddChild(activePrintName);
            container.AddChild(activePrintStatus);
            //container.AddChild(activePrintInfo);
            container.AddChild(printActionRow);
            container.AddChild(new MessageActionRow());

            return container;
        }

        protected void AddHandlers()
        {
            PrinterCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onPrintItemChanged, ref unregisterEvents);
            PrinterCommunication.Instance.ConnectionStateChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
            PrinterCommunication.Instance.WroteLine.RegisterEvent(Instance_WroteLine, ref unregisterEvents);
            PrinterCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            timeSinceLastDrawTime.Restart();
            base.OnDraw(graphics2D);
        }

        void OnIdle(object state)
        {
            if (PrinterCommunication.Instance.PrinterIsPrinting)
            {
                if (!timeSinceLastDrawTime.IsRunning)
                {
                    timeSinceLastDrawTime.Start();
                }
                else if (timeSinceLastDrawTime.ElapsedMilliseconds > 999)
                {
                    UpdatePrintStatus();
                    timeSinceLastDrawTime.Restart();
                }
            }

            if (!WidgetHasBeenClosed)
            {
                UiThread.RunOnIdle(OnIdle);
            }
        }

        void Instance_WroteLine(object sender, EventArgs e)
        {
            UpdatePrintStatus();
        }

        private void onStateChanged(object sender, EventArgs e)
        {
            UpdatePrintStatus();
        }

        private void UpdatePrintStatus()
        {
            if (PrinterCommunication.Instance.ActivePrintItem != null)
            {
                int totalSecondsInPrint = PrinterCommunication.Instance.TotalSecondsInPrint;

                int totalHoursInPrint = (int)(totalSecondsInPrint / (60 * 60));
                int totalMinutesInPrint = (int)(totalSecondsInPrint / 60 - totalHoursInPrint * 60);
                totalSecondsInPrint = totalSecondsInPrint % 60;

                string totalTimeLabel = LocalizedString.Get("Est. Print Time");
                string calculatingLabel = LocalizedString.Get("Calculating...");
                string totalPrintTimeText;

                if (totalSecondsInPrint > 0)
                {
                    
                    if (totalHoursInPrint > 0)
                    {
						
						totalPrintTimeText = string.Format("{3} {0}h {1:00}m {2:00}s",
                            totalHoursInPrint,
                            totalMinutesInPrint,
							totalSecondsInPrint,
							totalTimeLabel);
                    }
                    else
                    {
						totalPrintTimeText = string.Format("{2} {0}m {1:00}s",
                            totalMinutesInPrint,
							totalSecondsInPrint,
							totalTimeLabel);
                    }
                }
                else
                {
                    totalPrintTimeText = string.Format("{0}: {1}", totalTimeLabel, calculatingLabel);
                }

                //GC.WaitForFullGCComplete();

                string printPercentRemainingText;
				string printPercentCompleteText = LocalizedString.Get("complete");
				printPercentRemainingText = string.Format("{0:0.0}% {1}", PrinterCommunication.Instance.PercentComplete,printPercentCompleteText);

                switch (PrinterCommunication.Instance.CommunicationState)
                {
				case PrinterCommunication.CommunicationStates.PreparingToPrint:
						string preparingPrintLabel = LocalizedString.Get("Preparing To Print");
						string preparingPrintLabelFull = string.Format("{0}:", preparingPrintLabel);
						activePrintLabel.Text = preparingPrintLabelFull;
                        //ActivePrintStatusText = ""; // set by slicer
                        activePrintInfo.Text = "";
                        break;

                    case PrinterCommunication.CommunicationStates.Printing:
                        {
                            activePrintLabel.Text = PrinterCommunication.Instance.PrintingStateString;
                            ActivePrintStatusText = totalPrintTimeText;
                        }
                        break;

                    case PrinterCommunication.CommunicationStates.Paused:
                        {
							string activePrintLabelText = LocalizedString.Get ("Printing Paused");
							string activePrintLabelTextFull = string.Format("{0}:", activePrintLabelText);
							activePrintLabel.Text = activePrintLabelTextFull;
                            ActivePrintStatusText = totalPrintTimeText;
                        }
                        break;

				case PrinterCommunication.CommunicationStates.FinishedPrint:
					string donePrintingText = LocalizedString.Get ("Done Printing");
					string donePrintingTextFull = string.Format ("{0}:", donePrintingText);
					activePrintLabel.Text = donePrintingTextFull;
                    ActivePrintStatusText = totalPrintTimeText;
                        break;

				default:
						string nextPrintLabelActive = LocalizedString.Get ("Next Print");
						string nextPrintLabelActiveFull = string.Format("{0}: ", nextPrintLabelActive);

						activePrintLabel.Text = nextPrintLabelActiveFull;
                        ActivePrintStatusText = "";
                        activePrintInfo.Text = "";
                        break;
                }
            }
            else
            {
				string nextPrintLabel = LocalizedString.Get ("Next Print");
				string nextPrintLabelFull = string.Format ("{0}:", nextPrintLabel);

				activePrintLabel.Text = nextPrintLabelFull;
				ActivePrintStatusText = string.Format(LocalizedString.Get("Press 'Add' to choose an item to print"));
                activePrintInfo.Text = "";
            }
        }

        protected void onPrintItemChanged(object sender, EventArgs e)
        {
            UpdatePrintItemName();
            UpdatePrintStatus();
        }

        void UpdatePrintItemName()
        {
            if (PrinterCommunication.Instance.ActivePrintItem != null)
            {
                string labelName = textInfo.ToTitleCase(PrinterCommunication.Instance.ActivePrintItem.Name);
                labelName = labelName.Replace('_', ' ');
                this.activePrintName.Text = labelName;
            }
            else
            {
				this.activePrintName.Text = LocalizedString.Get("No items in the print queue");
            }
        }

        private TextWidget getPrintStatusLabel(string text, int pointSize)
        {
            TextWidget widget = new TextWidget(text, pointSize: pointSize);
            widget.TextColor = RGBA_Bytes.White;
            widget.AutoExpandBoundsToText = true;
            widget.MinimumSize = new Vector2(widget.Width, widget.Height);
            return widget;
        }
    }
}
