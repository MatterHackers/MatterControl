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
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl.PrintHistory
{

	public class HistoryListView : FlowLayoutWidget, IListContentView
	{
		public int ThumbWidth { get; } = 50;
		public int ThumbHeight { get; } = 50;

		public HistoryListView()
			: base(FlowDirection.TopToBottom)
		{
		}

		public ListViewItemBase AddItem(ListViewItem item)
		{
			var historyRowItem = item.Model as PrintHistoryItem;
			var detailsView = new PrintHistoryListItem(item, this.ThumbWidth, this.ThumbHeight, historyRowItem?.PrintTask, true);
			this.AddChild(detailsView);

			return detailsView;
		}

		public void ClearItems()
		{
		}
	}

	public class PrintHistoryListItem : ListViewItemBase
	{
		public PrintTask printTask;
		public RGBA_Bytes WidgetTextColor;
		public RGBA_Bytes WidgetBackgroundColor;

		public bool isActivePrint = false;
		public bool isSelectedItem = false;
		public bool isHoverItem = false;
		private bool showTimestamp;
		private TextWidget partLabel;
		public CheckBox selectionCheckBox;
#if(__ANDROID__)
		private float pointSizeFactor = 0.85f;
		private static int rightOverlayWidth  = 240;

#else
		private float pointSizeFactor = 1f;
		private static int rightOverlayWidth  = 200;
#endif
		private int actionButtonSize = rightOverlayWidth/2;
		private SlideWidget rightButtonOverlay;

		public PrintHistoryListItem(ListViewItem listViewItem, int thumbWidth, int thumbHeight, PrintTask printTask, bool showTimestamp)
			: base(listViewItem, thumbWidth, thumbHeight)
		{
			this.printTask = printTask;
			this.showTimestamp = showTimestamp;

			this.HAnchor = Agg.UI.HAnchor.Stretch;
			this.Height = 50;
			this.BackgroundColor = this.WidgetBackgroundColor;
			this.Padding = new BorderDouble(0);
			this.Margin = new BorderDouble(6, 0, 6, 6);

			var mainContainer = new GuiWidget();
			mainContainer.HAnchor = HAnchor.Stretch;
			mainContainer.VAnchor = VAnchor.Stretch;

			TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
			{
				GuiWidget indicator = new GuiWidget();
				indicator.VAnchor = Agg.UI.VAnchor.Stretch;
				indicator.Width = 15;
				if (printTask.PrintComplete)
				{
					indicator.BackgroundColor = new RGBA_Bytes(38, 147, 51, 180);
				}
				else
				{
					indicator.BackgroundColor = new RGBA_Bytes(252, 209, 22, 180);
				}

				FlowLayoutWidget middleColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
				middleColumn.HAnchor = Agg.UI.HAnchor.Stretch;
				middleColumn.Padding = new BorderDouble(6, 3);
				{
					FlowLayoutWidget labelContainer = new FlowLayoutWidget();
					labelContainer.HAnchor = Agg.UI.HAnchor.Stretch;

					string labelName = textInfo.ToTitleCase(printTask.PrintName);
					labelName = labelName.Replace('_', ' ');
					partLabel = new TextWidget(labelName, pointSize: 15 * pointSizeFactor);
					partLabel.TextColor = WidgetTextColor;

					labelContainer.AddChild(partLabel);

					middleColumn.AddChild(labelContainer);
				}

				RGBA_Bytes timeTextColor = new RGBA_Bytes(34, 34, 34);

				FlowLayoutWidget buttonContainer = new FlowLayoutWidget();
				buttonContainer.Margin = new BorderDouble(0);
				buttonContainer.HAnchor = Agg.UI.HAnchor.Stretch;
				{
					var timeLabel = new TextWidget("Time".Localize().ToUpper() + ": ", pointSize: 8 * pointSizeFactor);
					timeLabel.TextColor = timeTextColor;

					TextWidget timeIndicator;
					int minutes = printTask.PrintTimeMinutes;
					if (minutes < 0)
					{
						timeIndicator = new TextWidget("Unknown".Localize());
					}
					else if (minutes > 60)
					{
						timeIndicator = new TextWidget("{0}hrs {1}min".FormatWith(printTask.PrintTimeMinutes / 60, printTask.PrintTimeMinutes % 60), pointSize: 12 * pointSizeFactor);
					}
					else
					{
						timeIndicator = new TextWidget(string.Format("{0}min", printTask.PrintTimeMinutes), pointSize: 12 * pointSizeFactor);
					}

					if (printTask.PercentDone > 0)
					{
						timeIndicator.AutoExpandBoundsToText = true;
						timeIndicator.Text += $" ({printTask.PercentDone:0.0}%)";
						
						if(printTask.RecoveryCount > 0)
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

					buttonContainer.AddChild(timeLabel);
					buttonContainer.AddChild(timeIndicator);
					buttonContainer.AddChild(new HorizontalSpacer());
					middleColumn.AddChild(buttonContainer);
				}

				GuiWidget primaryContainer = new GuiWidget();
				primaryContainer.HAnchor = HAnchor.Stretch;
				primaryContainer.VAnchor = VAnchor.Stretch;

				FlowLayoutWidget primaryFlow = new FlowLayoutWidget(FlowDirection.LeftToRight);
				primaryFlow.HAnchor = HAnchor.Stretch;
				primaryFlow.VAnchor = VAnchor.Stretch;

				primaryFlow.AddChild(indicator);
				primaryFlow.AddChild(middleColumn);

				primaryContainer.AddChild(primaryFlow);

				rightButtonOverlay = new SlideWidget();
				rightButtonOverlay.VAnchor = VAnchor.Stretch;
				rightButtonOverlay.HAnchor = Agg.UI.HAnchor.Right;
				rightButtonOverlay.Width = rightOverlayWidth;
				rightButtonOverlay.Visible = false;

				FlowLayoutWidget rightMiddleColumnContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
				rightMiddleColumnContainer.VAnchor = VAnchor.Stretch;
				{
					TextWidget viewLabel = new TextWidget("View".Localize());
					viewLabel.TextColor = RGBA_Bytes.White;
					viewLabel.VAnchor = VAnchor.Center;
					viewLabel.HAnchor = HAnchor.Center;

					TextWidget printLabel = new TextWidget("Print".Localize());
					printLabel.TextColor = RGBA_Bytes.White;
					printLabel.VAnchor = VAnchor.Center;
					printLabel.HAnchor = HAnchor.Center;

					FatFlatClickWidget printButton = new FatFlatClickWidget(printLabel);
					printButton.VAnchor = VAnchor.Stretch;
					printButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
					printButton.Width = actionButtonSize;
					printButton.Click += (sender, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							if (!ApplicationController.Instance.ActivePrinter.Connection.PrintIsActive)
							{
								// TODO: More work needed here. We need to stash the bedplate, switch to a new context, then invoke print as normal
								//ApplicationController.Instance.PrintActivePartIfPossible(new PrintItemWrapper(printTask.PrintItemId));
							}
							else
							{
								// TODO: Queue is somewhat deprecated. Consider disabling this feature/button while printing
								QueueData.Instance.AddItem(new PrintItemWrapper(printTask.PrintItemId));
							}
							rightButtonOverlay.SlideOut();
						});
					};
					rightMiddleColumnContainer.AddChild(printButton);
				}
				rightButtonOverlay.AddChild(rightMiddleColumnContainer);

				if (showTimestamp)
				{
					FlowLayoutWidget timestampColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
					timestampColumn.VAnchor = Agg.UI.VAnchor.Stretch;
					timestampColumn.BackgroundColor = RGBA_Bytes.LightGray;
					timestampColumn.Padding = new BorderDouble(6, 0);

					FlowLayoutWidget startTimeContainer = new FlowLayoutWidget();
					startTimeContainer.HAnchor = Agg.UI.HAnchor.Stretch;
					startTimeContainer.Padding = new BorderDouble(0, 3);

					string startLabelFull = "{0}:".FormatWith("Start".Localize().ToUpper());
					TextWidget startLabel = new TextWidget(startLabelFull, pointSize: 8 * pointSizeFactor);
					startLabel.TextColor = timeTextColor;

					string startTimeString = printTask.PrintStart.ToString("MMM d yyyy h:mm ") + printTask.PrintStart.ToString("tt").ToLower();
					TextWidget startDate = new TextWidget(startTimeString, pointSize: 12 * pointSizeFactor);
					startDate.TextColor = timeTextColor;

					startTimeContainer.AddChild(startLabel);
					startTimeContainer.AddChild(new HorizontalSpacer());
					startTimeContainer.AddChild(startDate);

					FlowLayoutWidget endTimeContainer = new FlowLayoutWidget();
					endTimeContainer.HAnchor = Agg.UI.HAnchor.Stretch;
					endTimeContainer.Padding = new BorderDouble(0, 3);

					string endLabelFull = "{0}:".FormatWith("End".Localize().ToUpper());
					TextWidget endLabel = new TextWidget(endLabelFull, pointSize: 8 * pointSizeFactor);
					endLabel.TextColor = timeTextColor;

					string endTimeString;
					if (printTask.PrintEnd != DateTime.MinValue)
					{
						endTimeString = printTask.PrintEnd.ToString("MMM d yyyy h:mm ") + printTask.PrintEnd.ToString("tt").ToLower();
					}
					else
					{
						endTimeString = "Unknown".Localize();
					}

					TextWidget endDate = new TextWidget(endTimeString, pointSize: 12 * pointSizeFactor);
					endDate.TextColor = timeTextColor;

					endTimeContainer.AddChild(endLabel);
					endTimeContainer.AddChild(new HorizontalSpacer());
					endTimeContainer.AddChild(endDate);

					HorizontalLine horizontalLine = new HorizontalLine();
					horizontalLine.BackgroundColor = RGBA_Bytes.Gray;

					timestampColumn.AddChild(endTimeContainer);
					timestampColumn.AddChild(horizontalLine);
					timestampColumn.AddChild(startTimeContainer);

					timestampColumn.HAnchor = HAnchor.Stretch;
					timestampColumn.Padding = new BorderDouble(5, 0, 15, 0);

					primaryFlow.AddChild(timestampColumn);
				}

				mainContainer.AddChild(primaryContainer);
				mainContainer.AddChild(rightButtonOverlay);

				this.AddChild(mainContainer);
			}
		}

		private EventHandler unregisterEvents;

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
					int amountRemaining = (maxLength - start.Length);
					string end = maxLengthName.Substring(maxLengthName.Length - amountRemaining, amountRemaining);
					maxLengthName = start + end;
				}
				string notFoundMessage = "Oops! Could not find this file:".Localize();
				string message = "{0}:\n'{1}'".FormatWith(notFoundMessage, maxLengthName);
				string titleLabel = "Item not Found".Localize();
				StyledMessageBox.ShowMessageBox(onConfirmRemove, message, titleLabel, StyledMessageBox.MessageType.OK);
			});
		}

		private PrintItemWrapper itemToRemove;

		private void onConfirmRemove(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				int index = QueueData.Instance.GetIndex(itemToRemove);
				UiThread.RunOnIdle(() => QueueData.Instance.RemoveAt(index));
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			if (this.isSelectedItem)
			{
				this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				this.partLabel.TextColor = RGBA_Bytes.White;
				this.selectionCheckBox.TextColor = RGBA_Bytes.White;

				//RectangleDouble Bounds = LocalBounds;
				//RoundedRect rectBorder = new RoundedRect(Bounds, 0);
				//graphics2D.Render(new Stroke(rectBorder, 3), RGBA_Bytes.White);
			}
			else if (this.isHoverItem)
			{
				RectangleDouble Bounds = LocalBounds;
				RoundedRect rectBorder = new RoundedRect(Bounds, 0);

				this.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
				this.partLabel.TextColor = RGBA_Bytes.White;
				this.selectionCheckBox.TextColor = RGBA_Bytes.White;

				graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.PrimaryAccentColor);
			}
			else
			{
				this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 255);
				this.partLabel.TextColor = RGBA_Bytes.Black;
			}
		}
	}
}