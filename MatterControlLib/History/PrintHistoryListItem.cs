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
using System.Text.RegularExpressions;
using CsvHelper;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
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

			TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
			{
				var indicator = new GuiWidget
				{
					VAnchor = VAnchor.Stretch,
					Width = 15
				};
				if (printTask.PrintComplete)
				{
					indicator.BackgroundColor = new Color(38, 147, 51, 180);
				}
				else
				{
					indicator.BackgroundColor = new Color(252, 209, 22, 180);
				}

				var middleColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Stretch,
					Padding = new BorderDouble(6, 3)
				};
				{
					var labelContainer = new FlowLayoutWidget
					{
						HAnchor = HAnchor.Stretch
					};

					string labelName = textInfo.ToTitleCase(printTask.PrintName);
					labelName = labelName.Replace('_', ' ');

					if (!string.IsNullOrEmpty(printTask.PrinterName))
					{
						labelName = printTask.PrinterName + ":\n" + labelName;
					}

					string groupNames = GetItemNamesFromMcx(printTask.PrintName);
					if (!string.IsNullOrEmpty(groupNames))
					{
						labelName += "\n" + groupNames;
					}

					var partLabel = new TextWidget(labelName, pointSize: 15)
					{
						TextColor = Color.Black
					};

					labelContainer.AddChild(partLabel);

					middleColumn.AddChild(labelContainer);
				}

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

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Right)
			{
				var theme = ApplicationController.Instance.MenuTheme;
				var printTasks = PrintHistoryData.Instance.GetHistoryItems(1000);

				var popupMenu = new PopupMenu(theme);
				AddRatingsMenu(popupMenu);
				AddNotesMenu(popupMenu, printTasks);

				popupMenu.CreateSeparator();

				AddExportMenu(popupMenu, printTasks);

				popupMenu.CreateSeparator();

				AddClearHistorMenu(popupMenu, printTasks);

				popupMenu.ShowMenu(this, mouseEvent);
			}

			base.OnClick(mouseEvent);
		}

		private void AddNotesMenu(PopupMenu popupMenu, IEnumerable<PrintTask> printTasks)
		{
			var addNotest = popupMenu.CreateMenuItem("Add Note...".Localize());
			addNotest.Enabled = printTasks.Any();
			addNotest.Click += (s, e) =>
			{
				DialogWindow.Show(
					new InputBoxPage(
						"Print History Note".Localize(),
						"Note".Localize(),
						printTask.Note == null ? "" : printTask.Note,
						"Enter Note Here".Localize(),
						printTask.Note == null ? "Add Note".Localize() : "Update".Localize(),
						(newNote) =>
						{
							printTask.Note = newNote;
							printTask.Commit();
							popupMenu.Unfocus();
						}));
			};
		}

		private void AddRatingsMenu(PopupMenu popupMenu)
		{
			var content = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Stretch
			};

			var textWidget = new TextWidget("Rating".Localize() + ":", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				// Padding = MenuPadding,
				VAnchor = VAnchor.Center
			};
			content.AddChild(textWidget);

			content.AddChild(new HorizontalSpacer());

			var siblings = new List<GuiWidget>();
			var toolTips = new string[]
			{
				"Failed".Localize(),
				"Terrible".Localize(),
				"Bad".Localize(),
				"Good".Localize(),
				"Great".Localize(),
			};

			for (int i = 0; i < toolTips.Length; i++)
			{
				var button = new RadioButton(new TextWidget(i.ToString(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor))
				{
					Border = new BorderDouble(1, 0, 0, 0),
					BorderColor = theme.MinimalShade,
					Enabled = printTask.PrintComplete,
					Checked = printTask.RatingWasSet && printTask.Rating == i,
					ToolTipText = toolTips[i],
				};

				siblings.Add(button);

				if (button.Checked && button.Enabled)
				{
					button.BackgroundColor = theme.AccentMimimalOverlay;
				}

				button.SiblingRadioButtonList = siblings;

				content.AddChild(button);

				button.Click += (s, e) =>
				{
					printTask.Rating = siblings.IndexOf((GuiWidget)s);
					printTask.RatingWasSet = true;
					printTask.Commit();
					popupMenu.Unfocus();
				};
			}

			var menuItem = new PopupMenu.MenuItem(content, theme)
			{
				HAnchor = HAnchor.Fit | HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				HoverColor = Color.Transparent,
			};
			popupMenu.AddChild(menuItem);
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
					StyledMessageBox.ShowMessageBox(
						"Exporting print history is a MatterControl Pro feature. Upgrade to Pro to unlock MatterControl Pro.".Localize(),
						"Upgrade to Pro".Localize(),
						StyledMessageBox.MessageType.OK);
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

			public double RecoveryCount { get; set; }

			public string ItemsPrinted { get; set; }

			public string Notes { get; set; }

			public int Rating { get; set; }
		}

		private static void ExportToCsv(IEnumerable<PrintTask> printTasks)
		{
			// right click success or fail
			// user settings that are different
			var records = new List<RowData>();

			// do the export
			foreach (var printTask in printTasks)
			{
				string groupNames = PrintHistoryListItem.GetItemNamesFromMcx(printTask.PrintName);

				records.Add(new RowData()
				{
					Printer = printTask.PrinterName,
					Name = printTask.PrintName,
					Start = printTask.PrintStart,
					End = printTask.PrintEnd,
					Compleated = printTask.PrintComplete,
					Rating = printTask.Rating,
					ItemsPrinted = groupNames,
					Minutes = printTask.PrintTimeMinutes,
					RecoveryCount = printTask.RecoveryCount,
					QualitySettingsName = printTask.QualitySettingsName,
					MaterialSettingsName = printTask.MaterialSettingsName,
					Notes = printTask.Note,
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

		public static string GetItemNamesFromMcx(string mcxFileName)
		{
			// add in the cache path
			mcxFileName = Path.Combine(ApplicationDataStorage.Instance.PlatingDirectory, mcxFileName);
			if (File.Exists(mcxFileName))
			{
				var names = JsonConvert.DeserializeObject<McxDocument.McxNode>(File.ReadAllText(mcxFileName)).AllNames();
				var grouped = names.GroupBy(n => n)
					.Select(g =>
					{
						if (g.Count() > 1)
						{
							return g.Key + " (" + g.Count() + ")";
						}
						else
						{
							return g.Key;
						}
					})
					.OrderBy(n => n);
				var groupNames = string.Join(", ", grouped);
				return groupNames;
			}

			return null;
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

		private void OnConfirmRemove(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				int index = QueueData.Instance.GetIndex(itemToRemove);
				UiThread.RunOnIdle(() => QueueData.Instance.RemoveAt(index));
			}
		}
	}

	public static class McxDocument
	{
		public class McxNode
		{
			public List<McxNode> Children { get; set; }

			public string Name { get; set; }

			public bool Visible { get; set; }

			public string MeshPath { get; set; }

			private static Regex fileNameNumberMatch = new Regex("\\(\\d+\\)\\s*$", RegexOptions.Compiled);

			public IEnumerable<string> AllNames()
			{
				if (Children?.Count > 0)
				{
					foreach (var child in Children)
					{
						foreach (var name in child.AllNames())
						{
							yield return name;
						}
					}
				}
				else if (!string.IsNullOrWhiteSpace(Name))
				{
					if (Name.Contains("("))
					{
						yield return fileNameNumberMatch.Replace(Name, "").Trim();
					}
					else
					{
						yield return Name;
					}
				}
			}
		}
	}
}