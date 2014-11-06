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

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class TerminalWidget : GuiWidget
    {
        Button sendCommand;
        CheckBox filterOutput;
        CheckBox autoUppercase;
        MHTextEditWidget manualCommandTextEdit;
        TextScrollWidget outputScrollWidget;
        RGBA_Bytes backgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
        RGBA_Bytes textColor = ActiveTheme.Instance.PrimaryTextColor;
        TextImageButtonFactory controlButtonFactory = new TextImageButtonFactory();

        public TerminalWidget(bool showInWindow)
        {
            this.BackgroundColor = backgroundColor;
            this.Padding = new BorderDouble(5, 0);
            FlowLayoutWidget topLeftToRightLayout = new FlowLayoutWidget();
            topLeftToRightLayout.AnchorAll();

            {
                FlowLayoutWidget manualEntryTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
                manualEntryTopToBottomLayout.VAnchor |= Agg.UI.VAnchor.ParentTop;
                manualEntryTopToBottomLayout.Padding = new BorderDouble(top: 8);

                {
                    FlowLayoutWidget topBarControls = new FlowLayoutWidget(FlowDirection.LeftToRight);
                    topBarControls.HAnchor |= HAnchor.ParentLeft;

                    string filterOutputChkTxt = LocalizedString.Get("Filter Output");

                    filterOutput = new CheckBox(filterOutputChkTxt);
                    filterOutput.Margin = new BorderDouble(5, 5, 5, 2);
                    filterOutput.Checked = false;
                    filterOutput.TextColor = this.textColor;
                    filterOutput.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(SetCorrectFilterOutputBehavior);
                    filterOutput.VAnchor = Agg.UI.VAnchor.ParentBottom;
                    topBarControls.AddChild(filterOutput);

                    string autoUpperCaseChkTxt = LocalizedString.Get("Auto Uppercase");

                    autoUppercase = new CheckBox(autoUpperCaseChkTxt);
                    autoUppercase.Margin = new BorderDouble(5, 5, 5, 2);
                    autoUppercase.Checked = true;
                    autoUppercase.TextColor = this.textColor;
                    autoUppercase.VAnchor = Agg.UI.VAnchor.ParentBottom;
                    topBarControls.AddChild(autoUppercase);
                    manualEntryTopToBottomLayout.AddChild(topBarControls);

                }

                {
                    outputScrollWidget = new TextScrollWidget();
                    //outputScrollWidget.Height = 100;
                    outputScrollWidget.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
                    outputScrollWidget.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                    outputScrollWidget.HAnchor = HAnchor.ParentLeftRight;
                    outputScrollWidget.VAnchor = VAnchor.ParentBottomTop;
                    outputScrollWidget.Margin = new BorderDouble(0, 5);
                    outputScrollWidget.Padding = new BorderDouble(3, 0);


                    manualEntryTopToBottomLayout.AddChild(outputScrollWidget);
                }

                FlowLayoutWidget manualEntryLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
                manualEntryLayout.BackgroundColor = this.backgroundColor;
                manualEntryLayout.HAnchor = HAnchor.ParentLeftRight;
                {
                    manualCommandTextEdit = new MHTextEditWidget("");
                    //manualCommandTextEdit.BackgroundColor = RGBA_Bytes.White;
                    manualCommandTextEdit.Margin = new BorderDouble(right: 3);
                    manualCommandTextEdit.HAnchor = HAnchor.ParentLeftRight;
                    manualCommandTextEdit.VAnchor = VAnchor.ParentBottom;
                    manualCommandTextEdit.ActualTextEditWidget.EnterPressed += new KeyEventHandler(manualCommandTextEdit_EnterPressed);
                    manualCommandTextEdit.ActualTextEditWidget.KeyDown += new KeyEventHandler(manualCommandTextEdit_KeyDown);
                    manualEntryLayout.AddChild(manualCommandTextEdit);
                }

                manualEntryTopToBottomLayout.AddChild(manualEntryLayout);

                Button clearConsoleButton = controlButtonFactory.Generate(LocalizedString.Get("Clear"));
                clearConsoleButton.Margin = new BorderDouble(0);
                clearConsoleButton.Click += (sender, e) =>
                {
                    outputScrollWidget.Clear();
                };

                //Output Console text to screen
                Button exportConsoleTextButton = controlButtonFactory.Generate(LocalizedString.Get("Export..."));
                exportConsoleTextButton.Click += (sender, mouseEvent) =>
                {
                    string logFilePath = String.Format("{0}\\logs\\{1}ConsoleOutput.txt",
                                                       System.IO.Directory.GetCurrentDirectory(),
                                                       System.Diagnostics.Stopwatch.GetTimestamp());
                    outputScrollWidget.WriteToFile(logFilePath);
                };

                Button closeButton = controlButtonFactory.Generate(LocalizedString.Get("Close"));
                closeButton.Click += (sender, e) =>
                {
                    UiThread.RunOnIdle(CloseWindow);
                };

                sendCommand = controlButtonFactory.Generate(LocalizedString.Get("Send"));
                sendCommand.Click += new EventHandler(sendManualCommandToPrinter_Click);

                FlowLayoutWidget bottomRowContainer = new FlowLayoutWidget();
                bottomRowContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                bottomRowContainer.Margin = new BorderDouble(0, 3);

                bottomRowContainer.AddChild(sendCommand);
                bottomRowContainer.AddChild(clearConsoleButton);
                bottomRowContainer.AddChild(exportConsoleTextButton);
                bottomRowContainer.AddChild(new HorizontalSpacer());

                if (showInWindow)
                {
                    bottomRowContainer.AddChild(closeButton);
                }

                manualEntryTopToBottomLayout.AddChild(bottomRowContainer);
                manualEntryTopToBottomLayout.AnchorAll();

                topLeftToRightLayout.AddChild(manualEntryTopToBottomLayout);
            }

            AddHandlers();

            AddChild(topLeftToRightLayout);
            SetCorrectFilterOutputBehavior(this, null);
            this.AnchorAll();
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            PrinterConnectionAndCommunication.Instance.ConnectionFailed.RegisterEvent(Instance_ConnectionFailed, ref unregisterEvents);
        }

        private void CloseWindow(object state)
        {
            this.Parent.Close();
        }

        public override void OnClosed(EventArgs e)
        {
            // make sure we are not holding onto this window (keeping a pointer that can't be garbage collected).
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        void monitorPrinterTemperature_CheckedStateChanged(object sender, EventArgs e)
        {
            PrinterConnectionAndCommunication.Instance.MonitorPrinterTemperature = ((CheckBox)sender).Checked;
        }

        List<string> commandHistory = new List<string>();
        int commandHistoryIndex = 0;
        void manualCommandTextEdit_KeyDown(object sender, KeyEventArgs keyEvent)
        {
            bool changeToHistory = false;
            if (keyEvent.KeyCode == Keys.Up)
            {
                commandHistoryIndex--;
                if (commandHistoryIndex < 0)
                {
                    commandHistoryIndex = 0;
                }
                changeToHistory = true;
            }
            else if (keyEvent.KeyCode == Keys.Down)
            {
                commandHistoryIndex++;
                if (commandHistoryIndex > commandHistory.Count - 1)
                {
                    commandHistoryIndex = commandHistory.Count - 1;
                }
                else
                {
                    changeToHistory = true;
                }
            }
            else if (keyEvent.KeyCode == Keys.Escape)
            {
                manualCommandTextEdit.Text = "";
            }

            if (changeToHistory && commandHistory.Count > 0)
            {
                manualCommandTextEdit.Text = commandHistory[commandHistoryIndex];
            }
        }

        void manualCommandTextEdit_EnterPressed(object sender, KeyEventArgs keyEvent)
        {
            sendManualCommandToPrinter_Click(null, null);
        }

        void sendManualCommandToPrinter_Click(object sender, EventArgs mouseEvent)
        {
            string textToSend = manualCommandTextEdit.Text.Trim();
            if (autoUppercase.Checked)
            {
                textToSend = textToSend.ToUpper();
            }
            commandHistory.Add(textToSend);
            commandHistoryIndex = commandHistory.Count;
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow(textToSend);
            if (!filterOutput.Checked)
            {
                outputScrollWidget.WriteLine(this, new StringEventArgs(textToSend));
            }
            manualCommandTextEdit.Text = "";
        }

        void SetCorrectFilterOutputBehavior(object sender, EventArgs e)
        {
            if (filterOutput.Checked)
            {
                PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalFromPrinter.UnregisterEvent(FromPrinter, ref unregisterEvents);
                PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalToPrinter.UnregisterEvent(ToPrinter, ref unregisterEvents);
                PrinterConnectionAndCommunication.Instance.ReadLine.RegisterEvent(outputScrollWidget.WriteLine, ref unregisterEvents);
            }
            else
            {
                PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalFromPrinter.RegisterEvent(FromPrinter, ref unregisterEvents);
                PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalToPrinter.RegisterEvent(ToPrinter, ref unregisterEvents);
                PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(outputScrollWidget.WriteLine, ref unregisterEvents);
            }
        }

        void FromPrinter(Object sender, EventArgs e)
        {
            StringEventArgs lineString = e as StringEventArgs;
            outputScrollWidget.WriteLine(sender, new StringEventArgs("<-" + lineString.Data));
        }

        void ToPrinter(Object sender, EventArgs e)
        {
            StringEventArgs lineString = e as StringEventArgs;
            outputScrollWidget.WriteLine(sender, new StringEventArgs("->" + lineString.Data));
        }

        void Instance_ConnectionFailed(object sender, EventArgs e)
        {
            outputScrollWidget.WriteLine(sender, new StringEventArgs("Lost connection to printer."));
        }
    }
    
    public class OutputScroll : GuiWidget
    {
        const int TOTOL_POW2 = 64;
        int lineCount = 0;
        string[] lines = new string[TOTOL_POW2];

        public RGBA_Bytes TextColor = new RGBA_Bytes(102, 102, 102);

        public OutputScroll()
        {
        }

        public void WriteLine(Object sender, EventArgs e)
        {
            StringEventArgs lineString = e as StringEventArgs;
            Write(lineString.Data + "\n");
        }

        TypeFacePrinter printer = new TypeFacePrinter();
        public void Write(string lineString)
        {
            string[] splitOnNL = lineString.Split('\n');
            foreach (string line in splitOnNL)
            {
                if (line.Length > 0)
                {
                    printer.Text = line;
                    Vector2 stringSize = printer.GetSize();

                    int arrayIndex = (lineCount % TOTOL_POW2);
                    lines[arrayIndex] = line;

                    lineCount++;
                }
            }

            Invalidate();
        }

        public void WriteToFile(string filePath)
        {
            System.IO.File.WriteAllLines(@filePath, lines);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            TypeFacePrinter printer = new TypeFacePrinter();
            printer.DrawFromHintedCache = true;

            RectangleDouble Bounds = LocalBounds;

            double y = LocalBounds.Bottom + printer.TypeFaceStyle.EmSizeInPixels * (TOTOL_POW2 - 1) + 5;
            for (int index = lineCount; index < lineCount + TOTOL_POW2; index++)
            {
                if (y > LocalBounds.Top)
                {
                    y -= printer.TypeFaceStyle.EmSizeInPixels;
                    continue;
                }
                int arrayIndex = (index % TOTOL_POW2);
                if (lines[arrayIndex] != null)
                {
                    printer.Text = lines[arrayIndex];
                    printer.Origin = new Vector2(Bounds.Left + 2, y);
                    printer.Render(graphics2D, TextColor);
                }
                y -= printer.TypeFaceStyle.EmSizeInPixels;
                if (y < -printer.TypeFaceStyle.EmSizeInPixels)
                {
                    break;
                }
            }

            base.OnDraw(graphics2D);
        }

        public void Clear()
        {
            for (int index = 0; index < TOTOL_POW2; index++)
            {
                lines[index] = "";
            }
            lineCount = 0;
        }
    }
}
