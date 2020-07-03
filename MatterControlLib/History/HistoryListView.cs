/*
Copyright (c) 2018, Kevin Pope, John Lewin
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

using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl.PrintHistory
{
	public class HistoryListView : FlowLayoutWidget, IListContentView
	{
		private readonly ThemeConfig theme = ApplicationController.Instance.Theme;

		public int ThumbWidth { get; } = 50;

		public int ThumbHeight { get; } = 50;

		// Parameterless constructor required for ListView
		public HistoryListView()
			: base(FlowDirection.TopToBottom)
		{
		}

		public HistoryListView(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Right)
			{
				var theme = ApplicationController.Instance.MenuTheme;

				// show a right click menu ('Set as Default' & 'Help')
				var popupMenu = new PopupMenu(theme);

				var historyItems = PrintHistoryData.Instance.GetHistoryItems(1).Select(f => new PrintHistoryItem(f)).ToList<ILibraryItem>();

				var exportPrintHistory = popupMenu.CreateMenuItem("Export History".Localize() + "...");
				exportPrintHistory.Enabled = historyItems.Count > 0;
				exportPrintHistory.Click += (s, e) =>
				{
				};

				bool showFilter = false;
				if (showFilter)
				{
					popupMenu.CreateSubMenu("Filter".Localize(), theme, (subMenu) =>
					{
						// foreach (var printer in AllPrinters)
						// {
						// 	var menuItem = subMenu.CreateMenuItem(nodeOperation.Title, nodeOperation.IconCollector?.Invoke(menuTheme.InvertIcons));
						// 	menuItem.Click += (s, e) =>
						// 	{
						// 		nodeOperation.Operation(selectedItem, scene).ConfigureAwait(false);
						// 	};
						// }
					});
				}

				popupMenu.CreateSeparator();
				var clearPrintHistory = popupMenu.CreateMenuItem("Clear History".Localize());
				clearPrintHistory.Enabled = historyItems.Count > 0;
				clearPrintHistory.Click += (s, e) =>
				{
					// clear history
					StyledMessageBox.ShowMessageBox(
					(clearHistory) =>
					{
						if (clearHistory)
						{
							PrintHistoryData.Instance.ClearHistory();
						}
					},
					"Are you sure you want to clear your print history?".Localize(),
					"Clear History?".Localize(),
					StyledMessageBox.MessageType.YES_NO,
					"Clear History".Localize());
				};

				ShowMenu(mouseEvent, popupMenu);
			}

			base.OnClick(mouseEvent);
		}

		private void ShowMenu(MouseEventArgs mouseEvent, PopupMenu popupMenu)
		{
			var sourceEvent = mouseEvent.Position;
			var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
			this.Parents<SystemWindow>().FirstOrDefault().ToolTipManager.Clear();
			systemWindow.ShowPopup(
				new MatePoint(this)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
				},
				new MatePoint(popupMenu)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
				},
				altBounds: new RectangleDouble(sourceEvent.X + 1, sourceEvent.Y + 1, sourceEvent.X + 1, sourceEvent.Y + 1));
		}

		public ListViewItemBase AddItem(ListViewItem item)
		{
			var historyRowItem = item.Model as PrintHistoryItem;
			var detailsView = new PrintHistoryListItem(item, this.ThumbWidth, this.ThumbHeight, historyRowItem?.PrintTask, theme);
			detailsView.Selectable = false;
			this.AddChild(detailsView);

			return detailsView;
		}

		public void ClearItems()
		{
		}

		public void BeginReload()
		{
		}

		public void EndReload()
		{
		}
	}
}