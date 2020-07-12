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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

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

		public class RowData
		{
			public string Printer { get; set; }

			public string QualitySettingsName { get; set; }

			public string MaterialSettingsName { get; set; }

			public string OverridSettings { get; set; }

			public string Name { get; set; }

			public DateTime Start { get; set; }

			public DateTime End { get; set; }

			public int Minutes { get; set; }

			public bool Compleated { get; set; }

			public double RecoveryCount { get; set; }

			public string ItemsPrinted { get; set; }
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Right)
			{
				var theme = ApplicationController.Instance.MenuTheme;

				// show a right click menu ('Set as Default' & 'Help')
				var popupMenu = new PopupMenu(theme);

				var historyItems = PrintHistoryData.Instance.GetHistoryItems(100);

				var exportPrintHistory = popupMenu.CreateMenuItem("Export History".Localize() + "...");
				exportPrintHistory.Enabled = historyItems.Any();
				exportPrintHistory.Click += (s, e) =>
				{
					if (ApplicationController.Instance.IsMatterControlPro())
					{
						ExportToCsv(historyItems);
					}
					else // upsell MatterControl Pro
					{
						StyledMessageBox.ShowMessageBox(
							"Exporting print history is a MatterControl Pro feature. Upgrade to Pro to unlock MatterControl Pro.".Localize(),
							"Upgrade to Pro".Localize(),
							StyledMessageBox.MessageType.OK);
					}
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
				clearPrintHistory.Enabled = historyItems.Any();
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

				popupMenu.ShowMenu(this, mouseEvent);
			}

			base.OnClick(mouseEvent);
		}

		private static void ExportToCsv(IEnumerable<PrintTask> historyItems)
		{
			// right click success or fail
			// user settings that are different
			var records = new List<RowData>();

			// do the export
			foreach (var item in historyItems)
			{
				string groupNames = PrintHistoryListItem.GetItemNamesFromMcx(item.PrintName);

				records.Add(new RowData()
				{
					Printer = item.PrinterName,
					Name = item.PrintName,
					Start = item.PrintStart,
					End = item.PrintEnd,
					Compleated = item.PrintComplete,
					ItemsPrinted = groupNames,
					Minutes = item.PrintTimeMinutes,
					RecoveryCount = item.RecoveryCount,
					QualitySettingsName = item.QualitySettingsName,
					MaterialSettingsName = item.MaterialSettingsName,
					OverridSettings = GetUserOverrides(),
				});
			}

			AggContext.FileDialogs.SaveFileDialog(
				new SaveFileDialogParams("MatterControl Printer Export|*.printer", title: "Export Printer Settings")
				{
					FileName = "Pinter Histor.csv",
					Filter = "CSV Files|*.csv"
				},
				(saveParams) =>
				{
					try
					{
						if (!string.IsNullOrWhiteSpace(saveParams.FileName))
						{
							using (var writer = new StreamWriter(saveParams.FileName))
							{
								using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
								{
									csv.WriteRecords(records);
								}
							}
						}
					}
					catch (Exception e2)
					{
						UiThread.RunOnIdle(() =>
						{
							StyledMessageBox.ShowMessageBox(e2.Message, "Couldn't save file".Localize());
						});
					}
				});
		}

		private static string GetUserOverrides()
		{
			return null;
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