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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.IO;

using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Localizations;

using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.MatterControl.PrintHistory
{
   

    public class PrintHistoryListItem : FlowLayoutWidget
    {        
        public PrintTask printTask;
        public RGBA_Bytes WidgetTextColor;
        public RGBA_Bytes WidgetBackgroundColor;

        public bool isActivePrint = false;
        public bool isSelectedItem = false;
        public bool isHoverItem = false;
        bool showTimestamp;
        TextWidget partLabel;
        Button printAgainLink;
        public CheckBox selectionCheckBox;
        FlowLayoutWidget buttonContainer;
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

        public PrintHistoryListItem(PrintTask printTask, bool showTimestamp)
        {            
            this.printTask = printTask;
            this.showTimestamp = showTimestamp;
            SetDisplayAttributes();
            AddChildElements();
            AddHandlers();
        }

        void AddChildElements()
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;            
            {
                GuiWidget indicator = new GuiWidget();
                indicator.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                indicator.Width = 15;
                if (printTask.PrintComplete)
                {
                    indicator.BackgroundColor = new RGBA_Bytes(38, 147, 51, 180);
                }
                else
                {
                    indicator.BackgroundColor = new RGBA_Bytes(252, 209, 22, 180);
                }

                FlowLayoutWidget middleColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                middleColumn.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                middleColumn.Padding = new BorderDouble(6,3);
                {
                    FlowLayoutWidget labelContainer = new FlowLayoutWidget();
                    labelContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                    string labelName = textInfo.ToTitleCase(printTask.PrintName);
                    labelName = labelName.Replace('_', ' ');
                    partLabel = new TextWidget(labelName, pointSize: 15);
                    partLabel.TextColor = WidgetTextColor;

                    labelContainer.AddChild(partLabel);


                    middleColumn.AddChild(labelContainer);
                }

                RGBA_Bytes timeTextColor = RGBA_Bytes.Gray;

                buttonContainer = new FlowLayoutWidget();
                buttonContainer.Margin = new BorderDouble(0);
                buttonContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                {
                    TextWidget statusIndicator = new TextWidget("Status: Completed".Localize(), pointSize:8);
                    statusIndicator.Margin = new BorderDouble(right: 3);
                    //buttonContainer.AddChild(statusIndicator);

                    string printTimeLabel = "Print Time".Localize().ToUpper();
                    string printTimeLabelFull = string.Format("{0}: ", printTimeLabel);
                    TextWidget timeLabel = new TextWidget(printTimeLabelFull, pointSize: 8);
                    timeLabel.TextColor = timeTextColor;

                    TextWidget timeIndicator;
                    int minutes = printTask.PrintTimeMinutes;
                    if (minutes < 0)
                    {
                        timeIndicator = new TextWidget("Unknown".Localize());
                    }
                    else if (minutes > 60)
                    {
                        timeIndicator = new TextWidget("{0}hrs {1}min".FormatWith(printTask.PrintTimeMinutes / 60, printTask.PrintTimeMinutes % 60), pointSize: 12);
                    }
                    else
                    {
                        timeIndicator = new TextWidget(string.Format("{0}min", printTask.PrintTimeMinutes), pointSize: 12);
                    }
                    
                    timeIndicator.Margin = new BorderDouble(right: 6);
                    timeIndicator.TextColor = timeTextColor;

                    buttonContainer.AddChild(timeLabel);
                    buttonContainer.AddChild(timeIndicator);
                    
                    printAgainLink = linkButtonFactory.Generate("Print Again".Localize());
                    printAgainLink.Margin = new BorderDouble(left: 0, right: 10);
                    printAgainLink.VAnchor = VAnchor.ParentCenter;

                    printAgainLink.Click += (sender, e) =>
                    {
                        QueueData.Instance.AddItem(new PrintItemWrapper(printTask.PrintItemId).PrintItem);
                    };

                    buttonContainer.AddChild(printAgainLink);
                    middleColumn.AddChild(buttonContainer);
                }

                this.AddChild(indicator);
                this.AddChild(middleColumn);

                
                if (showTimestamp)
                {
                    FlowLayoutWidget rightColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    rightColumn.BackgroundColor = RGBA_Bytes.LightGray;
                    rightColumn.Padding = new BorderDouble(6, 0);
                    
                    FlowLayoutWidget startTimeContainer = new FlowLayoutWidget();
                    startTimeContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                    startTimeContainer.Padding = new BorderDouble(0, 3);

                    string startLabelFull = "{0}:".FormatWith("Start".Localize().ToUpper());
                    TextWidget startLabel = new TextWidget(startLabelFull, pointSize: 8);
                    startLabel.TextColor = timeTextColor;

                    string startTimeString = printTask.PrintStart.ToString("MMM d yyyy h:mm ") + printTask.PrintStart.ToString("tt").ToLower();
                    TextWidget startDate = new TextWidget(startTimeString, pointSize: 12);
                    startDate.TextColor = timeTextColor;

                    startTimeContainer.AddChild(startLabel);
                    startTimeContainer.AddChild(new HorizontalSpacer());
                    startTimeContainer.AddChild(startDate);


                    FlowLayoutWidget endTimeContainer = new FlowLayoutWidget();
                    endTimeContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                    endTimeContainer.Padding = new BorderDouble(0, 3);

                    string endLabelFull = "{0}:".FormatWith("End".Localize().ToUpper());
                    TextWidget endLabel = new TextWidget(endLabelFull, pointSize: 8);
                    endLabel.TextColor = timeTextColor;

                    string endTimeString;
                    if (printTask.PrintEnd != DateTime.MinValue)
                    {
                        endTimeString = printTask.PrintEnd.ToString("MMM d yyyy h:mm ") + printTask.PrintEnd.ToString("tt").ToLower();
                    }
                    else
                    {
                        endTimeString = "Unknown".Localize();
                    }

                    TextWidget endDate = new TextWidget(endTimeString, pointSize: 12);
                    endDate.TextColor = timeTextColor;

                    endTimeContainer.AddChild(endLabel);
                    endTimeContainer.AddChild(new HorizontalSpacer());
                    endTimeContainer.AddChild(endDate);

                    HorizontalLine horizontalLine = new HorizontalLine();
                    horizontalLine.BackgroundColor = RGBA_Bytes.Gray;

                    rightColumn.AddChild(endTimeContainer);
                    rightColumn.AddChild(horizontalLine);
                    rightColumn.AddChild(startTimeContainer);

                    rightColumn.Width = 220;
                    this.AddChild(rightColumn);
                }
                
                
            }
        }

        void SetDisplayAttributes()
        {
            linkButtonFactory.fontSize = 10;            
            this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            this.Height = 70;
            this.BackgroundColor = this.WidgetBackgroundColor;
            this.Padding = new BorderDouble(0);
            this.Margin = new BorderDouble(6, 0, 6, 6);
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            //Set background and text color to new theme
            this.Invalidate();
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

            if (this.isHoverItem)
            {
                //buttonContainer.Visible = true;
            }
            else
            {
                //buttonContainer.Visible = false;
            }

            base.OnDraw(graphics2D);

            if (this.isSelectedItem)
            {
                this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
                this.partLabel.TextColor = RGBA_Bytes.White;
                this.selectionCheckBox.TextColor = RGBA_Bytes.White;

                //RectangleDouble Bounds = LocalBounds;
                //RoundedRect rectBorder = new RoundedRect(Bounds, 0);
                //graphics2D.Render(new Stroke(rectBorder, 3), RGBA_Bytes.White);
            }

            else if (this.isHoverItem)
            {
                RectangleDouble Bounds = LocalBounds;
                RoundedRect rectBorder = new RoundedRect(Bounds, 0);

                this.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
                this.partLabel.TextColor = RGBA_Bytes.White;
                this.selectionCheckBox.TextColor = RGBA_Bytes.White;

                graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.PrimaryAccentColor);
            }
            else
            {
                this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 255);
                this.partLabel.TextColor = RGBA_Bytes.Black;
            }

        }
    }
}
