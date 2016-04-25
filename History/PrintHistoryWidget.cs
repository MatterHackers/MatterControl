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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using System;

namespace MatterHackers.MatterControl.PrintHistory
{
	public class PrintHistoryWidget : GuiWidget
	{
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private CheckBox showOnlyCompletedCheckbox;
		private CheckBox showTimestampCheckbox;
		private PrintHistoryDataView historyView;

		private TextWidget completedPrintsCount;
		private TextWidget totalPrintTime;

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

				showOnlyCompletedCheckbox = new CheckBox("Only Show Completed".Localize(), historyPanelTextColor, textSize: 10);
				showOnlyCompletedCheckbox.Margin = new BorderDouble(top: 8);
				bool showOnlyCompleted = (UserSettings.Instance.get("PrintHistoryFilterShowCompleted") == "true");
				showOnlyCompletedCheckbox.Checked = showOnlyCompleted;
				showOnlyCompletedCheckbox.Width = 200;

				completedStatsContainer.AddChild(new TextWidget("Completed Prints:".Localize() + " ", pointSize: 10, textColor: historyPanelTextColor));
				completedPrintsCount = new TextWidget(GetCompletedPrints().ToString(), pointSize: 14, textColor: historyPanelTextColor);
				completedPrintsCount.AutoExpandBoundsToText = true;
				completedStatsContainer.AddChild(completedPrintsCount);
				completedStatsContainer.AddChild(new HorizontalSpacer());
				completedStatsContainer.AddChild(showOnlyCompletedCheckbox);

				FlowLayoutWidget historyStatsContainer = new FlowLayoutWidget();
				historyStatsContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
				historyStatsContainer.Padding = new BorderDouble(6, 2);

				showTimestampCheckbox = new CheckBox("Show Timestamp".Localize(), historyPanelTextColor, textSize: 10);
				//showTimestampCheckbox.Margin = new BorderDouble(top: 8);
				bool showTimestamp = (UserSettings.Instance.get("PrintHistoryFilterShowTimestamp") == "true");
				showTimestampCheckbox.Checked = showTimestamp;
				showTimestampCheckbox.Width = 200;

				historyStatsContainer.AddChild(new TextWidget("Total Print Time:".Localize() + " ", pointSize: 10, textColor: historyPanelTextColor));
				totalPrintTime = new TextWidget(GetPrintTimeString(), pointSize: 14, textColor: historyPanelTextColor);
				totalPrintTime.AutoExpandBoundsToText = true;
				historyStatsContainer.AddChild(totalPrintTime);
				historyStatsContainer.AddChild(new HorizontalSpacer());
				historyStatsContainer.AddChild(showTimestampCheckbox);

				FlowLayoutWidget searchPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
				searchPanel.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
				searchPanel.HAnchor = HAnchor.ParentLeftRight;
				searchPanel.Padding = new BorderDouble(0, 6, 0, 2);

				searchPanel.AddChild(completedStatsContainer);
				searchPanel.AddChild(historyStatsContainer);

				FlowLayoutWidget buttonPanel = new FlowLayoutWidget();
				buttonPanel.HAnchor = HAnchor.ParentLeftRight;
				buttonPanel.Padding = new BorderDouble(0, 3);
				{
					buttonPanel.AddChild(new HorizontalSpacer());
				}

				allControls.AddChild(searchPanel);
				historyView = new PrintHistoryDataView();
				historyView.DoneLoading += historyView_DoneLoading;
				allControls.AddChild(historyView);
				allControls.AddChild(buttonPanel);
			}
			allControls.AnchorAll();

			this.AddChild(allControls);

			AddHandlers();
		}

		private void historyView_DoneLoading(object sender, EventArgs e)
		{
			UpdateCompletedCount();
		}

		private void UpdateCompletedCount()
		{
			completedPrintsCount.Text = GetCompletedPrints().ToString();
			totalPrintTime.Text = GetPrintTimeString();
		}

		private void AddHandlers()
		{
			showOnlyCompletedCheckbox.CheckedStateChanged += UpdateHistoryFilterShowCompleted;
			showTimestampCheckbox.CheckedStateChanged += UpdateHistoryFilterShowTimestamp;
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

			historyView.LoadHistoryItems();
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
			historyView.ShowTimestamp = showTimestampCheckbox.Checked;
			historyView.LoadHistoryItems();
		}

		private int GetCompletedPrints()
		{
			//string query = "SELECT COUNT(*) FROM PrintTask WHERE PrintComplete = '{0}';".FormatWith(true);
			var results = Datastore.Instance.dbSQLite.Table<PrintTask>().Where(o => o.PrintComplete == true);
			return results.Count();
		}

		private int GetTotalPrintSeconds()
		{
			return Datastore.Instance.dbSQLite.ExecuteScalar<int>("SELECT SUM(PrintTimeSeconds) FROM PrintTask");
		}

		private string GetPrintTimeString()
		{
			int seconds = GetTotalPrintSeconds();
			TimeSpan span = new TimeSpan(0, 0, seconds);

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