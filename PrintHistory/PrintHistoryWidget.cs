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
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;

using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PrintHistory
{
    public class PrintHistoryWidget : GuiWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        Button deleteFromLibraryButton;
        CheckBox showOnlyCompletedCheckbox;
        CheckBox showTimestampCheckbox;

        public PrintHistoryWidget()
        {
            SetDisplayAttributes();

            textImageButtonFactory.borderWidth = 0;
            RGBA_Bytes historyPanelTextColor = ActiveTheme.Instance.PrimaryTextColor;

            FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {

                

                FlowLayoutWidget completedStatsContainer = new FlowLayoutWidget();
                completedStatsContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                completedStatsContainer.Padding = new BorderDouble(6, 2);

                showOnlyCompletedCheckbox = new CheckBox(LocalizedString.Get("Only Show Completed"), historyPanelTextColor, textSize: 10);
                showOnlyCompletedCheckbox.Margin = new BorderDouble(top: 8);
                bool showOnlyCompleted = (UserSettings.Instance.get("PrintHistoryFilterShowCompleted") == "true");
                showOnlyCompletedCheckbox.Checked = showOnlyCompleted;
                showOnlyCompletedCheckbox.Width = 200;

                completedStatsContainer.AddChild(new TextWidget("Completed Prints: ", pointSize: 10, textColor: historyPanelTextColor));
                completedStatsContainer.AddChild(new TextWidget(GetCompletedPrints().ToString(), pointSize: 14, textColor: historyPanelTextColor));
                completedStatsContainer.AddChild(new HorizontalSpacer());
                completedStatsContainer.AddChild(showOnlyCompletedCheckbox);

                FlowLayoutWidget historyStatsContainer = new FlowLayoutWidget();
                historyStatsContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                historyStatsContainer.Padding = new BorderDouble(6, 2);

                showTimestampCheckbox = new CheckBox(LocalizedString.Get("Show Timestamp"), historyPanelTextColor, textSize: 10);
                //showTimestampCheckbox.Margin = new BorderDouble(top: 8);
                bool showTimestamp = (UserSettings.Instance.get("PrintHistoryFilterShowTimestamp") == "true");
                showTimestampCheckbox.Checked = showTimestamp;
                showTimestampCheckbox.Width = 200;

                historyStatsContainer.AddChild(new TextWidget("Total Print Time: ", pointSize: 10, textColor: historyPanelTextColor));
                historyStatsContainer.AddChild(new TextWidget(GetPrintTimeString(), pointSize: 14, textColor: historyPanelTextColor));
                historyStatsContainer.AddChild(new HorizontalSpacer());
                historyStatsContainer.AddChild(showTimestampCheckbox);
                
                FlowLayoutWidget searchPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
                searchPanel.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
                searchPanel.HAnchor = HAnchor.ParentLeftRight;
                searchPanel.Padding = new BorderDouble(0,6,0,2);

                searchPanel.AddChild(completedStatsContainer);
                searchPanel.AddChild(historyStatsContainer);

                FlowLayoutWidget buttonPanel = new FlowLayoutWidget();
                buttonPanel.HAnchor = HAnchor.ParentLeftRight;
                buttonPanel.Padding = new BorderDouble(0, 3);
                {
                    GuiWidget spacer = new GuiWidget();
                    spacer.HAnchor = HAnchor.ParentLeftRight;
                    buttonPanel.AddChild(spacer);
                }

                allControls.AddChild(searchPanel);
                if (PrintHistoryListControl.Instance.Parent != null)
                {
                    PrintHistoryListControl.Instance.Parent.RemoveChild(PrintHistoryListControl.Instance);
                }
                allControls.AddChild(PrintHistoryListControl.Instance);
                allControls.AddChild(buttonPanel);
            }
            allControls.AnchorAll();

            this.AddChild(allControls);

            AddHandlers();
        }

        private void AddHandlers()
        {
            showOnlyCompletedCheckbox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(UpdateHistoryFilterShowCompleted);
            showTimestampCheckbox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(UpdateHistoryFilterShowTimestamp);
        }

        private void UpdateHistoryFilterShowCompleted(object sender, EventArgs e)
        {
            if (showOnlyCompletedCheckbox.Checked)
            {
                UserSettings.Instance.set("PrintHistoryFilterShowCompleted", "true");
            }
            else
            {
                UserSettings.Instance.set("PrintHistoryFilterShowCompleted", "false");
            }
            PrintHistoryListControl.Instance.LoadHistoryItems();
        }

        private void UpdateHistoryFilterShowTimestamp(object sender, EventArgs e)
        {
            if (showTimestampCheckbox.Checked)
            {
                UserSettings.Instance.set("PrintHistoryFilterShowTimestamp", "true");
            }
            else
            {
                UserSettings.Instance.set("PrintHistoryFilterShowTimestamp", "false");
            }
            PrintHistoryListControl.Instance.ShowTimestamp = showTimestampCheckbox.Checked;
            PrintHistoryListControl.Instance.LoadHistoryItems();
        }

        private int GetCompletedPrints()
        {            
            //string query = "SELECT COUNT(*) FROM PrintTask WHERE PrintComplete = '{0}';".FormatWith(true);
            var results = DataStorage.Datastore.Instance.dbSQLite.Table<PrintTask>().Where(o => o.PrintComplete == true);
            return results.Count();
        }

        private int GetTotalPrintSeconds()
        {
            string query = "SELECT SUM(PrintTimeSeconds) FROM PrintTask";
            var results = DataStorage.Datastore.Instance.dbSQLite.ExecuteScalar<int>(query);
            return results;
        }


        private string GetPrintTimeString()
        {
            int seconds = GetTotalPrintSeconds();
            TimeSpan span = new TimeSpan(0,0,seconds);

            string timeString;
            if (seconds <= 0)
            {
                timeString = "0min";
            }
            else if (seconds > 86400)
            {
                timeString = "{0}d {1}hrs {2}min".FormatWith(span.Days, span.Hours, span.Minutes);
            }
            else if (seconds > 3600)
            {
                timeString = "{0}hrs {1}min".FormatWith(span.Hours, span.Minutes);
            }
            else
            {
                timeString = "{0}min".FormatWith(span.Minutes);
            }
            return timeString;
        }

        private void SetDisplayAttributes()
        {
            this.Padding = new BorderDouble(3);
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.AnchorAll();
        }        
    }
}
