/*
Copyright (c) 2020, Kevin Pope, John Lewin, Lars Brubaker
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
using System.Text.RegularExpressions;
using CsvHelper;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.PrintHistory
{
	public class PrintHistoryListItem : ListViewItemBase
	{
		public PrintHistoryListItem(ListViewItem listViewItem, int thumbWidth, int thumbHeight, PrintTask printTask, ThemeConfig theme)
			: base(listViewItem, thumbWidth, thumbHeight, theme)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Padding = new BorderDouble(0);
			this.Margin = new BorderDouble(6, 0, 6, 6);
			this.printTask = printTask;

			var mainContainer = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			{
				indicator = new GuiWidget
				{
					VAnchor = VAnchor.Stretch,
					Width = 15
				};

				SetIndicatorColor();

				var middleColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Stretch,
					Padding = new BorderDouble(6, 3)
				};

				var labelContainer = new FlowLayoutWidget
				{
					HAnchor = HAnchor.Stretch
				};

				printInfoWidget = new TextWidget(GetPrintInfo(), pointSize: 15)
				{
					TextColor = Color.Black,
					AutoExpandBoundsToText = true,
				};

				labelContainer.AddChild(printInfoWidget);

				middleColumn.AddChild(labelContainer);

				var timeTextColor = new Color(34, 34, 34);

				var detailsRow = new FlowLayoutWidget
				{
					Margin = new BorderDouble(0),
					HAnchor = HAnchor.Stretch
				};
				{
					var timeLabel = new TextWidget("Time".Localize().ToUpper() + ": ", pointSize: 8)
					{
						TextColor = timeTextColor
					};

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

					if (printTask.PercentDone > 0)
					{
						timeIndicator.AutoExpandBoundsToText = true;
						timeIndicator.Text += $" ({printTask.PercentDone:0.0}%)";

						if (printTask.RecoveryCount > 0)
						{
							if (printTask.RecoveryCount == 1)
							{
								timeIndicator.Text += " - " + "recovered once".Localize();
							}
							else
							{
								timeIndicator.Text += " - " + "recovered {0} times".FormatWith(printTask.RecoveryCount);
							}
						}
					}

					if (printTask.PrintCanceled)
					{
						timeIndicator.Text += " - Canceled";
					}

					timeIndicator.Margin = new BorderDouble(right: 6);
					timeIndicator.TextColor = timeTextColor;

					detailsRow.AddChild(timeLabel);
					detailsRow.AddChild(timeIndicator);
					detailsRow.AddChild(new HorizontalSpacer());
					middleColumn.AddChild(detailsRow);
				}

				var primaryContainer = new GuiWidget
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};

				var primaryFlow = new FlowLayoutWidget(FlowDirection.LeftToRight)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};

				primaryFlow.AddChild(indicator);
				primaryFlow.AddChild(middleColumn);

				primaryContainer.AddChild(primaryFlow);

				AddTimeStamp(printTask, timeTextColor, primaryFlow);

				mainContainer.AddChild(primaryContainer);

				this.AddChild(mainContainer);
			}

			this.BackgroundColor = new Color(255, 255, 255, 255);
		}

		private void SetIndicatorColor()
		{
			if (printTask.PrintComplete)
			{
				if (printTask.QualityWasSet && printTask.PrintQuality == 0)
				{
					indicator.BackgroundColor = new Color(252, 38, 51, 180);
				}
				else
				{
					indicator.BackgroundColor = new Color(38, 147, 51, 180);
				}
			}
			else if (printTask.PrintCanceled)
			{
				indicator.BackgroundColor = new Color(252, 209, 22, 180);
			}
			else
			{
				indicator.BackgroundColor = Color.LightGray;
			}
		}

		private string GetPrintInfo()
		{
			TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

			var labelName = "";
			if (!string.IsNullOrEmpty(printTask.PrinterName))
			{
				labelName = printTask.PrinterName + ":\n" + labelName;
			}

			labelName += textInfo.ToTitleCase(printTask.PrintName).Replace('_', ' ');

			string groupNames = printTask.ItemsPrinted();
			if (!string.IsNullOrEmpty(groupNames))
			{
				labelName += "\n" + groupNames;
			}

			if (printTask.QualityWasSet)
			{
				labelName += "\n" + "Print Quality".Localize() + ": " + PrintHistoryEditor.QualityNames[printTask.PrintQuality];
			}

			if (!string.IsNullOrWhiteSpace(printTask.Note))
			{
				labelName += "\n" + printTask.Note;
			}

			return labelName;
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Right)
			{
				var theme = ApplicationController.Instance.MenuTheme;
				var printTasks = PrintHistoryData.Instance.GetHistoryItems(1000);

				var popupMenu = new PopupMenu(theme);
				var printHistoryEditor = new PrintHistoryEditor(theme, printTask, printTasks);
				var qualityWidget = PrintHistoryEditor.GetQualityWidget(theme,
					printTask,
					() =>
					{
						popupMenu.Unfocus();
						printInfoWidget.Text = GetPrintInfo();
						SetIndicatorColor();
					},
					theme.DefaultFontSize);

				var menuItem = new PopupMenu.MenuItem(qualityWidget, theme)
				{
					HAnchor = HAnchor.Fit | HAnchor.Stretch,
					VAnchor = VAnchor.Fit,
					HoverColor = Color.Transparent,
				};
				popupMenu.AddChild(menuItem);

				printHistoryEditor.AddNotesMenu(popupMenu, printTasks, () =>
				{
					printInfoWidget.Text = GetPrintInfo();
				});

				popupMenu.CreateSeparator();

				AddExportMenu(popupMenu, printTasks);

				popupMenu.CreateSeparator();

				AddClearHistorMenu(popupMenu, printTasks);

				popupMenu.ShowMenu(this, mouseEvent);
			}

			base.OnClick(mouseEvent);
		}

		private void AddClearHistorMenu(PopupMenu popupMenu, IEnumerable<PrintTask> printTasks)
		{
			var clearPrintHistory = popupMenu.CreateMenuItem("Clear History".Localize());
			clearPrintHistory.Enabled = printTasks.Any();
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
		}

		private void AddExportMenu(PopupMenu popupMenu, IEnumerable<PrintTask> printTasks)
		{
			var exportPrintHistory = popupMenu.CreateMenuItem("Export History".Localize() + "...");
			exportPrintHistory.Enabled = printTasks.Any();
			exportPrintHistory.Click += (s, e) =>
			{
				if (ApplicationController.Instance.IsMatterControlPro())
				{
					ExportToCsv(printTasks);
				}
				else // upsell MatterControl Pro
				{
					string text = "Exporting print history is a MatterControl Pro feature. Upgrade to Pro to unlock MatterControl Pro.".Localize();
					WebCache.RetrieveText(
						"https://matterhackers.github.io/MatterControl-Docs/ProContent/Unlock_Export_Print_History.md",
						(markDown) =>
						{
							// push the new text into the widget
							text = markDown;
						});

					StyledMessageBox.ShowMessageBox(text,
						"Upgrade to Pro".Localize(),
						StyledMessageBox.MessageType.OK,
						useMarkdown: true,
						width: 540,
						height: 400);
				}
			};
		}

		public class RowData
		{
			public string Printer { get; set; }

			public string QualitySettingsName { get; set; }

			public string MaterialSettingsName { get; set; }

			public string Name { get; set; }

			public DateTime Start { get; set; }

			public DateTime End { get; set; }

			public int Minutes { get; set; }

			public bool Compleated { get; set; }

			public bool Canceled { get; internal set; }

			public double RecoveryCount { get; set; }

			public string ItemsPrinted { get; set; }

			public string Notes { get; set; }

			public string Guid { get; set; }

			public int PrintQuality { get; set; }
		}

		private static void ExportToCsv(IEnumerable<PrintTask> printTasks)
		{
			// right click success or fail
			// user settings that are different
			var records = new List<RowData>();

			// do the export
			foreach (var printTask in printTasks)
			{
				string groupNames = printTask.ItemsPrinted();

				records.Add(new RowData()
				{
					Printer = printTask.PrinterName,
					Name = printTask.PrintName,
					Start = printTask.PrintStart,
					End = printTask.PrintEnd,
					Compleated = printTask.PrintComplete,
					Canceled = printTask.PrintCanceled,
					PrintQuality = printTask.PrintQuality,
					ItemsPrinted = groupNames,
					Minutes = printTask.PrintTimeMinutes,
					RecoveryCount = printTask.RecoveryCount,
					QualitySettingsName = printTask.QualitySettingsName,
					MaterialSettingsName = printTask.MaterialSettingsName,
					Notes = printTask.Note,
					Guid = printTask.Guid,
				});
			}

			AggContext.FileDialogs.SaveFileDialog(
				new SaveFileDialogParams("MatterControl Printer Export|*.printer", title: "Export Printer Settings")
				{
					FileName = "Printer History.csv",
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

		private void AddTimeStamp(PrintTask printTask, Color timeTextColor, FlowLayoutWidget primaryFlow)
		{
			var timestampColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Stretch,
				BackgroundColor = Color.LightGray,
				Padding = new BorderDouble(6, 0)
			};

			var startTimeContainer = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(0, 3)
			};

			string startLabelFull = "{0}:".FormatWith("Start".Localize().ToUpper());
			var startLabel = new TextWidget(startLabelFull, pointSize: 8)
			{
				TextColor = timeTextColor
			};

			string startTimeString = printTask.PrintStart.ToString("MMM d yyyy h:mm ") + printTask.PrintStart.ToString("tt").ToLower();
			var startDate = new TextWidget(startTimeString, pointSize: 12)
			{
				TextColor = timeTextColor
			};

			startTimeContainer.AddChild(startLabel);
			startTimeContainer.AddChild(new HorizontalSpacer());
			startTimeContainer.AddChild(startDate);

			var endTimeContainer = new FlowLayoutWidget
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(0, 3)
			};

			string endLabelFull = "{0}:".FormatWith("End".Localize().ToUpper());
			var endLabel = new TextWidget(endLabelFull, pointSize: 8)
			{
				TextColor = timeTextColor
			};

			string endTimeString;
			if (printTask.PrintEnd != DateTime.MinValue)
			{
				endTimeString = printTask.PrintEnd.ToString("MMM d yyyy h:mm ") + printTask.PrintEnd.ToString("tt").ToLower();
			}
			else
			{
				endTimeString = "Unknown".Localize();
			}

			var endDate = new TextWidget(endTimeString, pointSize: 12)
			{
				TextColor = timeTextColor
			};

			endTimeContainer.AddChild(endLabel);
			endTimeContainer.AddChild(new HorizontalSpacer());
			endTimeContainer.AddChild(endDate);

			var horizontalLine = new HorizontalLine
			{
				BackgroundColor = Color.Gray
			};

			timestampColumn.AddChild(endTimeContainer);
			timestampColumn.AddChild(horizontalLine);
			timestampColumn.AddChild(startTimeContainer);

			timestampColumn.HAnchor = HAnchor.Stretch;
			timestampColumn.Padding = new BorderDouble(5, 0, 15, 0);

			primaryFlow.AddChild(timestampColumn);
		}

		public void ShowCantFindFileMessage(PrintItemWrapper printItemWrapper)
		{
			itemToRemove = printItemWrapper;
			UiThread.RunOnIdle(() =>
			{
				string maxLengthName = printItemWrapper.FileLocation;
				int maxLength = 43;
				if (maxLengthName.Length > maxLength)
				{
					string start = maxLengthName.Substring(0, 15) + "...";
					int amountRemaining = maxLength - start.Length;
					string end = maxLengthName.Substring(maxLengthName.Length - amountRemaining, amountRemaining);
					maxLengthName = start + end;
				}

				string notFoundMessage = "Oops! Could not find this file".Localize() + ":";
				string message = "{0}:\n'{1}'".FormatWith(notFoundMessage, maxLengthName);
				string titleLabel = "Item not Found".Localize();
				StyledMessageBox.ShowMessageBox(OnConfirmRemove, message, titleLabel, StyledMessageBox.MessageType.OK);
			});
		}

		private PrintItemWrapper itemToRemove;
		private PrintTask printTask;
		private GuiWidget indicator;
		private TextWidget printInfoWidget;

		private void OnConfirmRemove(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				int index = QueueData.Instance.GetIndex(itemToRemove);
				UiThread.RunOnIdle(() => QueueData.Instance.RemoveAt(index));
			}
		}
	}
}