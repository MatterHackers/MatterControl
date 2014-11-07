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
                    FlowLayoutWidget leftToRight = new FlowLayoutWidget();
                    leftToRight.AnchorAll();

                    outputScrollWidget = new TextScrollWidget(PrinterOutputCache.Instance.PrinterLines);
                    //outputScrollWidget.Height = 100;
                    outputScrollWidget.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
                    outputScrollWidget.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                    outputScrollWidget.HAnchor = HAnchor.ParentLeftRight;
                    outputScrollWidget.VAnchor = VAnchor.ParentBottomTop;
                    outputScrollWidget.Margin = new BorderDouble(0, 5);
                    outputScrollWidget.Padding = new BorderDouble(3, 0);


                    leftToRight.AddChild(outputScrollWidget);

                    GuiWidget fakeScrollBar = new GuiWidget(10, 10);
                    fakeScrollBar.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                    fakeScrollBar.BackgroundColor = RGBA_Bytes.Blue;
                    leftToRight.AddChild(fakeScrollBar);

                    manualEntryTopToBottomLayout.AddChild(leftToRight);
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
                    PrinterOutputCache.Instance.Clear();
                };

                //Output Console text to screen
                Button exportConsoleTextButton = controlButtonFactory.Generate(LocalizedString.Get("Export..."));
                exportConsoleTextButton.Click += (sender, mouseEvent) =>
                {
                    UiThread.RunOnIdle(DoExportExportLog_Click);
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

            AddChild(topLeftToRightLayout);
            this.AnchorAll();
        }

        void DoExportExportLog_Click(object state)
        {
            string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Save as Text|*.txt", initialDirectory: documentsPath);  
			saveParams.Title = "MatterControl: Terminal Log";
			saveParams.ActionButtonLabel = "Export";
            saveParams.FileName = "print_log.txt";

            FileDialog.SaveFileDialog(saveParams, onExportLogFileSelected);
        }

        void onExportLogFileSelected(SaveFileDialogParams saveParams)
		{
			if (saveParams.FileName != null)
			{
				string filePathToSave = saveParams.FileName;
				if (filePathToSave != null && filePathToSave != "")
				{
                    outputScrollWidget.WriteToFile(filePathToSave);
                }
			}
		}

        private void CloseWindow(object state)
        {
            this.Parent.Close();
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
            manualCommandTextEdit.Text = "";
        }
    }
}
