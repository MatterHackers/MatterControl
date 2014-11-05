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
   

    public class PrintHistoryListItem : GuiWidget
    {        
        public PrintTask printTask;
        public RGBA_Bytes WidgetTextColor;
        public RGBA_Bytes WidgetBackgroundColor;

        public bool isActivePrint = false;
        public bool isSelectedItem = false;
        public bool isHoverItem = false;
        bool showTimestamp;
        TextWidget partLabel;        
        public CheckBox selectionCheckBox;
        
        SlideWidget rightButtonOverlay;

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
            GuiWidget mainContainer = new GuiWidget();
            mainContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            mainContainer.VAnchor = VAnchor.ParentBottomTop;

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
                middleColumn.Padding = new BorderDouble(6, 3);
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

                RGBA_Bytes timeTextColor = new RGBA_Bytes(34, 34, 34);

                FlowLayoutWidget buttonContainer = new FlowLayoutWidget();
                buttonContainer.Margin = new BorderDouble(0);
                buttonContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                {
                    TextWidget statusIndicator = new TextWidget("Status: Completed".Localize(), pointSize: 8);
                    statusIndicator.Margin = new BorderDouble(right: 3);
                    //buttonContainer.AddChild(statusIndicator);

                    string printTimeLabel = "Time".Localize().ToUpper();
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
                    buttonContainer.AddChild(new HorizontalSpacer());
                    middleColumn.AddChild(buttonContainer);
                }

                GuiWidget primaryContainer = new GuiWidget();
                primaryContainer.HAnchor = HAnchor.ParentLeftRight;
                primaryContainer.VAnchor = VAnchor.ParentBottomTop;

                
                FlowLayoutWidget primaryFlow = new FlowLayoutWidget(FlowDirection.LeftToRight);
                primaryFlow.HAnchor = HAnchor.ParentLeftRight;
                primaryFlow.VAnchor = VAnchor.ParentBottomTop;

                primaryFlow.AddChild(indicator);
                primaryFlow.AddChild(middleColumn);

                primaryContainer.AddChild(primaryFlow);

                rightButtonOverlay = new SlideWidget();
                rightButtonOverlay.VAnchor = VAnchor.ParentBottomTop;
                rightButtonOverlay.HAnchor = Agg.UI.HAnchor.ParentRight;
                rightButtonOverlay.Width = 200;
                rightButtonOverlay.Visible = false;
                
                FlowLayoutWidget rightMiddleColumnContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
                rightMiddleColumnContainer.VAnchor = VAnchor.ParentBottomTop;
                {
                    ClickWidget viewButton = new ClickWidget();
                    viewButton.VAnchor = VAnchor.ParentBottomTop;
                    viewButton.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
                    viewButton.Width = 100;

                    TextWidget viewLabel = new TextWidget("View".Localize());
                    viewLabel.TextColor = RGBA_Bytes.White;
                    viewLabel.VAnchor = VAnchor.ParentCenter;
                    viewLabel.HAnchor = HAnchor.ParentCenter;

                    viewButton.AddChild(viewLabel);
                    viewButton.Click += ViewButton_Click;
                    rightMiddleColumnContainer.AddChild(viewButton);

                    ClickWidget printButton = new ClickWidget();
                    printButton.VAnchor = VAnchor.ParentBottomTop;
                    printButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
                    printButton.Width = 100;

                    TextWidget printLabel = new TextWidget("Print".Localize());
                    printLabel.TextColor = RGBA_Bytes.White;
                    printLabel.VAnchor = VAnchor.ParentCenter;
                    printLabel.HAnchor = HAnchor.ParentCenter;

                    printButton.AddChild(printLabel);
                    printButton.Click += (sender, e) =>
                    {
                        QueueData.Instance.AddItem(new PrintItemWrapper(printTask.PrintItemId),0);                        
                        QueueData.Instance.SelectedIndex = 0;
                        PrinterCommunication.PrinterConnectionAndCommunication.Instance.PrintActivePartIfPossible();
                    };
                    rightMiddleColumnContainer.AddChild(printButton);


                }
                rightButtonOverlay.AddChild(rightMiddleColumnContainer);
                
                if (showTimestamp)
                {
                    FlowLayoutWidget timestampColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    timestampColumn.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                    timestampColumn.BackgroundColor = RGBA_Bytes.LightGray;
                    timestampColumn.Padding = new BorderDouble(6, 0);

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

                    timestampColumn.AddChild(endTimeContainer);
                    timestampColumn.AddChild(horizontalLine);
                    timestampColumn.AddChild(startTimeContainer);

                    timestampColumn.Width = 200;

                    primaryFlow.AddChild(timestampColumn);
                }

                mainContainer.AddChild(primaryContainer);
                mainContainer.AddChild(rightButtonOverlay);

                this.AddChild(mainContainer);
            }
        }

        void SetDisplayAttributes()
        {
            linkButtonFactory.fontSize = 10;            
            this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            this.Height = 50;
            this.BackgroundColor = this.WidgetBackgroundColor;
            this.Padding = new BorderDouble(0);
            this.Margin = new BorderDouble(6, 0, 6, 6);
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
            MouseEnterBounds += new EventHandler(HistoryItem_MouseEnterBounds);
            MouseLeaveBounds += new EventHandler(HistoryItem_MouseLeaveBounds);
        }

        void ViewButton_Click(object sender, EventArgs e)
        {
            
            PrintItem printItem = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.PrintItem>().Where(v => v.Id == this.printTask.PrintItemId).Take(1).FirstOrDefault();
            
            if (printItem != null)
            {
                string pathAndFile = printItem.FileLocation;
                if (File.Exists(pathAndFile))
                {
                    bool shiftKeyDown = Keyboard.IsKeyDown(Keys.ShiftKey);
                    if (shiftKeyDown)
                    {
                        OpenPartPreviewWindow(printItem, View3DWidget.AutoRotate.Disabled);
                    }
                    else
                    {
                        OpenPartPreviewWindow(printItem, View3DWidget.AutoRotate.Enabled);
                    }
                }
                else
                {
                    PrintItemWrapper itemWrapper = new PrintItemWrapper(printItem);
                    ShowCantFindFileMessage(itemWrapper);
                }
            }
        }

        public void ShowCantFindFileMessage(PrintItemWrapper printItemWrapper)
        {
            itemToRemove = printItemWrapper;
            UiThread.RunOnIdle((state) =>
            {
                string maxLengthName = printItemWrapper.FileLocation;
                int maxLength = 43;
                if (maxLengthName.Length > maxLength)
                {
                    string start = maxLengthName.Substring(0, 15) + "...";
                    int amountRemaining = (maxLength - start.Length);
                    string end = maxLengthName.Substring(maxLengthName.Length - amountRemaining, amountRemaining);
                    maxLengthName = start + end;
                }
                string notFoundMessage = LocalizedString.Get("Oops! Could not find this file:");
                string message = "{0}:\n'{1}'".FormatWith(notFoundMessage, maxLengthName);
                string titleLabel = LocalizedString.Get("Item not Found");
                StyledMessageBox.ShowMessageBox(onConfirmRemove, message, titleLabel, StyledMessageBox.MessageType.OK);
            });
        }
        PrintItemWrapper itemToRemove;

        void onConfirmRemove(bool messageBoxResponse)
        {
            if (messageBoxResponse)
            {
                QueueData.Instance.RemoveIndexOnIdle(QueueData.Instance.GetIndex(itemToRemove));
            }
        }

        PartPreviewMainWindow partPreviewWindow;
        private void OpenPartPreviewWindow(PrintItem printItem, View3DWidget.AutoRotate autoRotate)
        {

            PrintItemWrapper itemWrapper = new PrintItemWrapper(printItem.Id);
            if (partPreviewWindow == null)
            {
                partPreviewWindow = new PartPreviewMainWindow(itemWrapper, autoRotate);
                partPreviewWindow.Closed += new EventHandler(PartPreviewWindow_Closed);
            }
            else
            {
                partPreviewWindow.BringToFront();
            }
        }

        void PartPreviewWindow_Closed(object sender, EventArgs e)
        {
            this.partPreviewWindow = null;
        }


        void HistoryItem_MouseLeaveBounds(object sender, EventArgs e)
        {            
            rightButtonOverlay.SlideOut();
        }

        void HistoryItem_MouseEnterBounds(object sender, EventArgs e)
        {
            rightButtonOverlay.SlideIn();
        }


        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public void ThemeChanged(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {

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
