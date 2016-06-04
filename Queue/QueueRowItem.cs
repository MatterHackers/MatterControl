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
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using System;
using System.Globalization;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class QueueRowItem : GuiWidget
	{
		public bool isActivePrint = false;

		public bool isHoverItem = false;

		public bool isSelectedItem = false;

		public CheckBox selectionCheckBox;

		public RGBA_Bytes WidgetBackgroundColor;

		//public PrintItemWrapper printItemWrapper;
		public RGBA_Bytes WidgetTextColor;

		private static PrintItemWrapper itemToRemove;

		private SlideWidget actionButtonContainer;

		private Button addToLibraryLink;

		private string alsoRemoveFromSdCardMessage = "Would you also like to remove this file from the Printer's SD Card?".Localize();

		private string alsoRemoveFromSdCardTitle = "Remove From Printer's SD Card?";

		private ConditionalClickWidget conditionalClickContainer;

		private ExportPrintItemWindow exportingWindow;

		private bool exportingWindowIsOpen = false;

		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

		private TextWidget partLabel;

		private TextWidget partStatus;

		private QueueDataView queueDataView;

		private GuiWidget selectionCheckBoxContainer;

		private FatFlatClickWidget viewButton;

		private TextWidget viewButtonLabel;

		private PartPreviewMainWindow viewingWindow;

		private bool viewWindowIsOpen = false;

		public QueueRowItem(PrintItemWrapper printItemWrapper, QueueDataView queueDataView)
		{
			this.queueDataView = queueDataView;
			this.PrintItemWrapper = printItemWrapper;
			this.Name = "Queue Item " + printItemWrapper.Name;

			// Ensure queue items do not overwrite existing AMF files on disk when converting
			// from STL or similar to AMF
			this.PrintItemWrapper.UseIncrementedNameDuringTypeChange = true;

			ConstructPrintQueueItem();
		}

		private event EventHandler unregisterEvents;

		public bool IsHoverItem
		{
			get { return isHoverItem; }
			set
			{
				if (this.isHoverItem != value)
				{
					this.isHoverItem = value;
					if (value == true && !this.queueDataView.EditMode)
					{
						this.actionButtonContainer.SlideIn();
					}
					else
					{
						this.actionButtonContainer.SlideOut();
					}
				}
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			switch (UnderMouseState)
			{
				case UnderMouseState.NotUnderMouse:
					IsHoverItem = false;
					break;

				case UnderMouseState.FirstUnderMouse:
					IsHoverItem = true;
					break;

				case UnderMouseState.UnderMouseNotFirst:
					if (ContainsFirstUnderMouseRecursive())
					{
						IsHoverItem = true;
					}
					else
					{
						IsHoverItem = false;
					}
					break;
			}

			base.OnMouseMove(mouseEvent);
		}

		public PrintItemWrapper PrintItemWrapper { get; set; }

		public static void ShowCantFindFileMessage(PrintItemWrapper printItemWrapper)
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
				string notFoundMessage = LocalizedString.Get("Oops! Could not find this file");
				string notFoundMessageEnd = LocalizedString.Get("Would you like to remove it from the queue");
				string message = "{0}:\n'{1}'\n\n{2}?".FormatWith(notFoundMessage, maxLengthName, notFoundMessageEnd);
				string titleLabel = LocalizedString.Get("Item not Found");
				StyledMessageBox.ShowMessageBox(onConfirmRemove, message, titleLabel, StyledMessageBox.MessageType.YES_NO);
			});
		}

		public void ConstructPrintQueueItem()
		{
			linkButtonFactory.fontSize = 10;
			linkButtonFactory.textColor = RGBA_Bytes.Black;

			WidgetTextColor = RGBA_Bytes.Black;
			WidgetBackgroundColor = RGBA_Bytes.White;

			TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

			SetDisplayAttributes();

			FlowLayoutWidget topToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomLayout.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

			FlowLayoutWidget topContentsFlowLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
			topContentsFlowLayout.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			{
				FlowLayoutWidget leftColumn = new FlowLayoutWidget(FlowDirection.LeftToRight);
				leftColumn.VAnchor = VAnchor.ParentTop | Agg.UI.VAnchor.FitToChildren;
				{
					selectionCheckBoxContainer = new GuiWidget();
					selectionCheckBoxContainer.VAnchor = VAnchor.ParentBottomTop;
					selectionCheckBoxContainer.Width = 40;
					selectionCheckBoxContainer.Visible = false;
					selectionCheckBoxContainer.Margin = new BorderDouble(left: 6);
					selectionCheckBox = new CheckBox("");
					selectionCheckBox.Name = "Queue Item Checkbox";
					selectionCheckBox.VAnchor = VAnchor.ParentCenter;
					selectionCheckBox.HAnchor = HAnchor.ParentCenter;
					selectionCheckBoxContainer.AddChild(selectionCheckBox);

					PartThumbnailWidget thumbnailWidget = new PartThumbnailWidget(PrintItemWrapper, "part_icon_transparent_40x40.png", "building_thumbnail_40x40.png", PartThumbnailWidget.ImageSizes.Size50x50);
					thumbnailWidget.Name = "Queue Item Thumbnail";
					leftColumn.AddChild(selectionCheckBoxContainer);

					leftColumn.AddChild(thumbnailWidget);
				}

				FlowLayoutWidget middleColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
				middleColumn.VAnchor = VAnchor.ParentTop | Agg.UI.VAnchor.FitToChildren;
				middleColumn.HAnchor = HAnchor.ParentLeftRight;// | Agg.UI.HAnchor.FitToChildren;
				middleColumn.Padding = new BorderDouble(8);
				middleColumn.Margin = new BorderDouble(10, 0);
				{
					string labelName = textInfo.ToTitleCase(PrintItemWrapper.Name);
					labelName = labelName.Replace('_', ' ');
					partLabel = new TextWidget(labelName, pointSize: 14);
					partLabel.TextColor = WidgetTextColor;
					partLabel.MinimumSize = new Vector2(1, 16);

					string partStatusLabelTxt = LocalizedString.Get("Status").ToUpper();
					string partStatusLabelTxtTest = LocalizedString.Get("Queued to Print");
					string partStatusLabelTxtFull = "{0}: {1}".FormatWith(partStatusLabelTxt, partStatusLabelTxtTest);

					partStatus = new TextWidget(partStatusLabelTxtFull, pointSize: 10);
					partStatus.AutoExpandBoundsToText = true;
					partStatus.TextColor = WidgetTextColor;
					partStatus.MinimumSize = new Vector2(50, 12);

					middleColumn.AddChild(partLabel);
					middleColumn.AddChild(partStatus);
				}

				topContentsFlowLayout.AddChild(leftColumn);
				topContentsFlowLayout.AddChild(middleColumn);
			}

			// The ConditionalClickWidget supplies a user driven Enabled property based on a delegate of your choosing
			conditionalClickContainer = new ConditionalClickWidget(() => queueDataView.EditMode);
			conditionalClickContainer.HAnchor = HAnchor.ParentLeftRight;
			conditionalClickContainer.VAnchor = VAnchor.ParentBottomTop;
			conditionalClickContainer.Click += onQueueItemClick;

			topToBottomLayout.AddChild(topContentsFlowLayout);
			this.AddChild(topToBottomLayout);

			actionButtonContainer = getItemActionButtons();
			actionButtonContainer.Visible = false;
			this.AddChild(conditionalClickContainer);

			this.AddChild(actionButtonContainer);

			AddHandlers();
		}

		public override void OnClosed(EventArgs e)
		{
			PrintItemWrapper.SlicingOutputMessage -= PrintItem_SlicingOutputMessage;
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.queueDataView.EditMode)
			{
				selectionCheckBoxContainer.Visible = true;
				actionButtonContainer.Visible = false;
			}
			else
			{
				selectionCheckBoxContainer.Visible = false;
			}

			base.OnDraw(graphics2D);

			RectangleDouble Bounds = LocalBounds;
			RoundedRect rectBorder = new RoundedRect(Bounds, 0);

			if (this.isActivePrint && !this.queueDataView.EditMode)
			{
				this.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
				SetTextColors(RGBA_Bytes.White);
				this.viewButton.BackgroundColor = RGBA_Bytes.White;
				this.viewButtonLabel.TextColor = ActiveTheme.Instance.SecondaryAccentColor;

				//Draw interior border
				graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.SecondaryAccentColor);
			}
			else if (this.isSelectedItem)
			{
				this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				this.partLabel.TextColor = RGBA_Bytes.White;
				this.partStatus.TextColor = RGBA_Bytes.White;
				this.selectionCheckBox.TextColor = RGBA_Bytes.White;
				this.viewButton.BackgroundColor = RGBA_Bytes.White;
				this.viewButtonLabel.TextColor = ActiveTheme.Instance.SecondaryAccentColor;
			}
			else if (this.IsHoverItem)
			{
				this.BackgroundColor = RGBA_Bytes.White;
				this.partLabel.TextColor = RGBA_Bytes.Black;
				this.selectionCheckBox.TextColor = RGBA_Bytes.Black;
				this.partStatus.TextColor = RGBA_Bytes.Black;
				this.viewButton.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
				this.viewButtonLabel.TextColor = RGBA_Bytes.White;

				//Draw interior border
				graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.SecondaryAccentColor);
			}
			else
			{
				this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 255);
				SetTextColors(RGBA_Bytes.Black);
				this.selectionCheckBox.TextColor = RGBA_Bytes.Black;
				this.partStatus.TextColor = RGBA_Bytes.Black;
				this.viewButton.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
				this.viewButtonLabel.TextColor = RGBA_Bytes.White;
			}
		}

		public void OpenPartViewWindow(View3DWidget.OpenMode openMode = View3DWidget.OpenMode.Viewing)
		{
			if (viewWindowIsOpen == false)
			{
				viewingWindow = new PartPreviewMainWindow(this.PrintItemWrapper, View3DWidget.AutoRotate.Enabled, openMode);
				viewingWindow.Name = "Queue Item " + PrintItemWrapper.Name + " Part Preview";
				this.viewWindowIsOpen = true;
				viewingWindow.Closed += new EventHandler(PartPreviewWindow_Closed);
			}
			else
			{
				if (viewingWindow != null)
				{
					viewingWindow.BringToFront();
				}
			}
		}

		public void SetTextColors(RGBA_Bytes color)
		{
			if (this.partLabel.TextColor != color)
			{
				this.partLabel.TextColor = color;
				this.partStatus.TextColor = color;
			}
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			if (this.isActivePrint)
			{
				//Set background and text color to new theme
				this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				this.partLabel.TextColor = RGBA_Bytes.White;
				this.partStatus.TextColor = RGBA_Bytes.White;
				this.Invalidate();
			}
		}

		internal void DeletePartFromQueue()
		{
			if (PrintItemWrapper.PrintItem.FileLocation == QueueData.SdCardFileName)
			{
				StyledMessageBox.ShowMessageBox(onDeleteFileConfirm, alsoRemoveFromSdCardMessage, alsoRemoveFromSdCardTitle, StyledMessageBox.MessageType.YES_NO);
			}

			int thisIndexInQueue = QueueData.Instance.GetIndex(PrintItemWrapper);
			QueueData.Instance.RemoveIndexOnIdle(thisIndexInQueue);
		}

		private static void onConfirmRemove(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				QueueData.Instance.RemoveIndexOnIdle(QueueData.Instance.GetIndex(itemToRemove));
			}
		}

		private void AddHandlers()
		{
			ActiveTheme.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
			PrintItemWrapper.SlicingOutputMessage += PrintItem_SlicingOutputMessage;
		}

		private void ExportQueueItemWindow_Closed(object sender, EventArgs e)
		{
			this.exportingWindowIsOpen = false;
		}

		private SlideWidget getItemActionButtons()
		{
			SlideWidget buttonContainer = new SlideWidget();
			buttonContainer.VAnchor = VAnchor.ParentBottomTop;
			buttonContainer.HAnchor = HAnchor.ParentRight;

			FlowLayoutWidget buttonFlowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonFlowContainer.VAnchor = VAnchor.ParentBottomTop;

			TextWidget removeLabel = new TextWidget("Remove".Localize());
			removeLabel.Name = "Queue Item " + PrintItemWrapper.Name + " Remove";
			removeLabel.TextColor = RGBA_Bytes.White;
			removeLabel.VAnchor = VAnchor.ParentCenter;
			removeLabel.HAnchor = HAnchor.ParentCenter;

			FatFlatClickWidget removeButton = new FatFlatClickWidget(removeLabel);
			removeButton.VAnchor = VAnchor.ParentBottomTop;
			removeButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			removeButton.Width = 100;

			removeButton.Click += onRemovePartClick;

			viewButtonLabel = new TextWidget("View".Localize());
			viewButtonLabel.Name =  "Queue Item " + PrintItemWrapper.Name + " View";
			viewButtonLabel.TextColor = RGBA_Bytes.White;
			viewButtonLabel.VAnchor = VAnchor.ParentCenter;
			viewButtonLabel.HAnchor = HAnchor.ParentCenter;

			viewButton = new FatFlatClickWidget(viewButtonLabel);
			viewButton.VAnchor = VAnchor.ParentBottomTop;
			viewButton.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
			viewButton.Width = 100;

			viewButton.Click += onViewPartClick;

			buttonFlowContainer.AddChild(viewButton);
			buttonFlowContainer.AddChild(removeButton);

			buttonContainer.AddChild(buttonFlowContainer);
			buttonContainer.Width = 200;
			//buttonContainer.Width = 100;

			return buttonContainer;
		}

		private void onDeleteFileConfirm(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				// The firmware only understands the names when lowercase.
				PrinterConnectionAndCommunication.Instance.DeleteFileFromSdCard(PrintItemWrapper.PrintItem.Name);
			}
		}

		private void onQueueItemClick(object sender, EventArgs e)
		{
			if (queueDataView.EditMode)
			{
				if (this.isSelectedItem)
				{
					this.isSelectedItem = false;
					this.selectionCheckBox.Checked = false;
					queueDataView.SelectedItems.Remove(this);
				}
				else
				{
					this.isSelectedItem = true;
					this.selectionCheckBox.Checked = true;
					queueDataView.SelectedItems.Add(this);
				}
			}
		}

		private void onRemovePartClick(object sender, EventArgs e)
		{
			this.actionButtonContainer.SlideOut();
			UiThread.RunOnIdle(DeletePartFromQueue);
		}

		private void onViewPartClick(object sender, EventArgs e)
		{
			this.actionButtonContainer.SlideOut();
			UiThread.RunOnIdle(() =>
			{
				OpenPartViewWindow(View3DWidget.OpenMode.Viewing);
			});
		}

		private void OpenExportWindow()
		{
			if (exportingWindowIsOpen == false)
			{
				exportingWindow = new ExportPrintItemWindow(this.PrintItemWrapper);
				this.exportingWindowIsOpen = true;
				exportingWindow.Closed += new EventHandler(ExportQueueItemWindow_Closed);
				exportingWindow.ShowAsSystemWindow();
			}
			else
			{
				if (exportingWindow != null)
				{
					exportingWindow.BringToFront();
				}
			}
		}

		private void PartPreviewWindow_Closed(object sender, EventArgs e)
		{
			this.viewWindowIsOpen = false;
		}

		private void PrintItem_SlicingOutputMessage(object sender, EventArgs e)
		{
			StringEventArgs message = e as StringEventArgs;
			partStatus.Text = "Status: {0}".FormatWith(message.Data);
		}

		private void SetDisplayAttributes()
		{
			this.VAnchor = Agg.UI.VAnchor.FitToChildren;
			this.HAnchor = Agg.UI.HAnchor.ParentLeftRight | Agg.UI.HAnchor.FitToChildren;
			this.Height = 50;
			this.BackgroundColor = this.WidgetBackgroundColor;
			this.Padding = new BorderDouble(0);
			this.Margin = new BorderDouble(6, 0, 6, 6);
		}

		private class PartToAddToQueue
		{
			internal string FileLocation;
			internal int insertAfterIndex;
			internal string Name;

			internal PartToAddToQueue(string name, string fileLocation, int insertAfterIndex)
			{
				this.Name = name;
				this.FileLocation = fileLocation;
				this.insertAfterIndex = insertAfterIndex;
			}
		}
	}
}