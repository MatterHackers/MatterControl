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
using MatterHackers.MatterControl.PrintHistory;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class FirstPanelTabView : TabControl
	{
		public static int firstPanelCurrentTab = 0;

		private TabPage QueueTabPage;
		private TabPage LibraryTabPage;
		private TabPage HistoryTabPage;
		private RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;
		private QueueDataView queueDataView;

		private EventHandler unregisterEvents;

		public FirstPanelTabView(QueueDataView queueDataView)
		{
			this.queueDataView = queueDataView;
			this.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.TabBar.BorderColor = new RGBA_Bytes(0, 0, 0, 0);
			this.TabBar.Margin = new BorderDouble(0, 0);
			this.TabBar.Padding = new BorderDouble(0, 2);

			this.Margin = new BorderDouble(top: 4);

			QueueTabPage = new TabPage(new QueueDataWidget(queueDataView), "Queue".Localize().ToUpper());
			this.AddTab(new SimpleTextTabWidget(QueueTabPage, "Queue Tab", 15,
					ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			LibraryTabPage = new TabPage(new PrintLibraryWidget(), "Library".Localize().ToUpper());
			this.AddTab(new SimpleTextTabWidget(LibraryTabPage, "Library Tab", 15,
					ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			HistoryTabPage = new TabPage(new PrintHistoryWidget(), "History".Localize().ToUpper());
			this.AddTab(new SimpleTextTabWidget(HistoryTabPage, "History Tab", 15,
					ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			NumQueueItemsChanged(this, null);

			QueueData.Instance.ItemAdded.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);
			QueueData.Instance.ItemRemoved.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);

			WidescreenPanel.PreChangePanels.RegisterEvent(SaveCurrentTab, ref unregisterEvents);

			SelectedTabIndex = firstPanelCurrentTab;
		}

		private void NumQueueItemsChanged(object sender, EventArgs widgetEvent)
		{
			string queueStringBeg = "Queue".Localize().ToUpper();
			string queueString = string.Format("{1} ({0})", QueueData.Instance.ItemCount, queueStringBeg);
			QueueTabPage.Text = string.Format(queueString, QueueData.Instance.ItemCount);
		}

		private void SaveCurrentTab(object sender, EventArgs e)
		{
			firstPanelCurrentTab = SelectedTabIndex;
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
		}
	}
}