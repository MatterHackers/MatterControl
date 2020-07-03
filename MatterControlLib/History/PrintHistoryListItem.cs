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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.SerialPortCommunication;

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

			var mainContainer = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};

			TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
			{
				var indicator = new GuiWidget
				{
					VAnchor = Agg.UI.VAnchor.Stretch,
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
					HAnchor = Agg.UI.HAnchor.Stretch,
					Padding = new BorderDouble(6, 3)
				};
				{
					var labelContainer = new FlowLayoutWidget
					{
						HAnchor = Agg.UI.HAnchor.Stretch
					};

					string labelName = textInfo.ToTitleCase(printTask.PrintName);
					labelName = labelName.Replace('_', ' ');

					if (!string.IsNullOrEmpty(printTask.PrinterName))
					{
						labelName = printTask.PrinterName + ":\n" + labelName;
					}

					var mcxFileName = Path.Combine(ApplicationDataStorage.Instance.PlatingDirectory, printTask.PrintName);
					if (File.Exists(mcxFileName))
					{
						labelName += "\n" + GetStlNames(mcxFileName);
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
					HAnchor = Agg.UI.HAnchor.Stretch
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

		private string GetStlNames(string mcxFileName)
		{
			var foundNames = new HashSet<string>();
			var allLines = File.ReadAllLines(mcxFileName);
			foreach (var line in allLines)
			{
				var find = "\"Name\":";
				var end = line.IndexOf(find) + find.Length;
				if (end >= find.Length
					&& !line.ToLower().Contains(".mcx"))
				{
					var nameStart = line.IndexOf("\"", end);
					var nameEnd = line.IndexOf("\"", nameStart + 1);
					var name = line.Substring(nameStart + 1, nameEnd - nameStart - 1);
					foundNames.Add(name);
				}
			}

			return string.Join(", ", foundNames);
		}

		private void AddTimeStamp(PrintTask printTask, Color timeTextColor, FlowLayoutWidget primaryFlow)
		{
			var timestampColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = Agg.UI.VAnchor.Stretch,
				BackgroundColor = Color.LightGray,
				Padding = new BorderDouble(6, 0)
			};

			var startTimeContainer = new FlowLayoutWidget
			{
				HAnchor = Agg.UI.HAnchor.Stretch,
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
				HAnchor = Agg.UI.HAnchor.Stretch,
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