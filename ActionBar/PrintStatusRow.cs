/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ActionBar
{
	public class PrintStatusRow : FlowLayoutWidget
	{
		private TextWidget activePrintLabel;
		private TextWidget activePrintName;
		private PartThumbnailWidget activePrintPreviewImage;
		private TextWidget activePrintStatus;
		private TemperatureWidgetBase bedTemperatureWidget;
		private TemperatureWidgetBase extruderTemperatureWidget;
		private EventHandler unregisterEvents;

		public PrintStatusRow()
		{
			UiThread.RunOnIdle(OnIdle);

			this.Margin = new BorderDouble(6, 3, 6, 6);
			this.HAnchor = HAnchor.ParentLeftRight;

			AddChildElements();

			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent((s, e) =>
			{
				UpdatePrintItemName();
				UpdatePrintStatus();
			}, ref unregisterEvents);

			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				UpdatePrintStatus();
			}, ref unregisterEvents);

			PrinterConnectionAndCommunication.Instance.WroteLine.RegisterEvent((s, e) =>
			{
				UpdatePrintStatus();
			}, ref unregisterEvents);

			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);

			onActivePrintItemChanged(null, null);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if (activePrintPreviewImage.ItemWrapper != null)
			{
				activePrintPreviewImage.ItemWrapper.SlicingOutputMessage -= PrintItem_SlicingOutputMessage;
			}

			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void AddChildElements()
		{
			activePrintPreviewImage = new PartThumbnailWidget(null, "part_icon_transparent_100x100.png", "building_thumbnail_100x100.png", PartThumbnailWidget.ImageSizes.Size115x115);
			activePrintPreviewImage.VAnchor = VAnchor.ParentTop;
			activePrintPreviewImage.Padding = new BorderDouble(0);
			activePrintPreviewImage.HoverBackgroundColor = new RGBA_Bytes();
			activePrintPreviewImage.BorderWidth = 3;

			FlowLayoutWidget temperatureWidgets = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				extruderTemperatureWidget = new TemperatureWidgetExtruder();
				temperatureWidgets.AddChild(extruderTemperatureWidget);

				bedTemperatureWidget = new TemperatureWidgetBed();
				if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_heated_bed))
				{
					temperatureWidgets.AddChild(bedTemperatureWidget);
				}
			}
			temperatureWidgets.VAnchor |= VAnchor.ParentTop;
			temperatureWidgets.Margin = new BorderDouble(left: 6);

			FlowLayoutWidget printStatusContainer = CreateActivePrinterInfoWidget();
			printStatusContainer.VAnchor |= VAnchor.ParentTop;

			var iconContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			iconContainer.Name = "PrintStatusRow.IconContainer";
			iconContainer.VAnchor |= VAnchor.ParentTop;
			iconContainer.Margin = new BorderDouble(top: 3);
			iconContainer.AddChild(GetAutoLevelIndicator());

			this.AddChild(activePrintPreviewImage);
			this.AddChild(printStatusContainer);
			this.AddChild(iconContainer);
			this.AddChild(temperatureWidgets);

			UpdatePrintStatus();
			UpdatePrintItemName();
		}

		private FlowLayoutWidget CreateActivePrinterInfoWidget()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(6, 0, 6, 0);
			container.HAnchor = HAnchor.ParentLeftRight;
			container.VAnchor |= VAnchor.ParentTop;

			FlowLayoutWidget topRow = new FlowLayoutWidget();
			topRow.Name = "PrintStatusRow.ActivePrinterInfo.TopRow";
			topRow.HAnchor = HAnchor.ParentLeftRight;

			activePrintLabel = getPrintStatusLabel("Next Print".Localize() + ":", pointSize: 11);
			activePrintLabel.VAnchor = VAnchor.ParentTop;

			topRow.AddChild(activePrintLabel);

			activePrintName = getPrintStatusLabel("this is the biggest name we will allow", pointSize: 14);
			activePrintName.AutoExpandBoundsToText = false;

			activePrintStatus = getPrintStatusLabel("this is the biggest label we will allow - bigger", pointSize: 11);
			activePrintStatus.AutoExpandBoundsToText = false;
			activePrintStatus.Text = "";
			activePrintStatus.Margin = new BorderDouble(top: 3);

			container.AddChild(topRow);
			container.AddChild(activePrintName);
			container.AddChild(activePrintStatus);

			return container;
		}

		private Button GetAutoLevelIndicator()
		{
			ImageButtonFactory imageButtonFactory = new ImageButtonFactory();
			imageButtonFactory.InvertImageColor = false;
			ImageBuffer levelingImage = StaticData.Instance.LoadIcon("leveling_32x32.png", 16, 16).InvertLightness();
			Button autoLevelButton = imageButtonFactory.Generate(levelingImage, levelingImage);
			autoLevelButton.Margin = new Agg.BorderDouble(top: 3);
			autoLevelButton.ToolTipText = "Print leveling is enabled.".Localize();
			autoLevelButton.Cursor = Cursors.Hand;
			autoLevelButton.Visible = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled);

			PrinterSettings.PrintLevelingEnabledChanged.RegisterEvent((sender, e) =>
			{
				autoLevelButton.Visible = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled);
			}, ref unregisterEvents);

			return autoLevelButton;
		}

		private TextWidget getPrintStatusLabel(string text, int pointSize)
		{
			TextWidget widget = new TextWidget(text, pointSize: pointSize);
			widget.TextColor = RGBA_Bytes.White;
			widget.AutoExpandBoundsToText = true;
			widget.MinimumSize = new Vector2(widget.Width, widget.Height);
			return widget;
		}

		private void onActivePrintItemChanged(object sender, EventArgs e)
		{
			// first we have to remove any link to an old part (the part currently in the view)
			if (activePrintPreviewImage.ItemWrapper != null)
			{
				activePrintPreviewImage.ItemWrapper.SlicingOutputMessage -= PrintItem_SlicingOutputMessage;
			}

			activePrintPreviewImage.ItemWrapper = PrinterConnectionAndCommunication.Instance.ActivePrintItem;

			// then hook up our new part
			if (activePrintPreviewImage.ItemWrapper != null)
			{
				activePrintPreviewImage.ItemWrapper.SlicingOutputMessage += PrintItem_SlicingOutputMessage;
			}

			activePrintPreviewImage.Invalidate();
		}

		private void OnIdle()
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
			{
				UpdatePrintStatus();
			}

			if (!HasBeenClosed)
			{
				UiThread.RunOnIdle(OnIdle, 1);
			}
		}

		private void PrintItem_SlicingOutputMessage(object sender, StringEventArgs message)
		{
			activePrintStatus.Text = message.Data;
		}

		private void SetVisibleStatus()
		{
			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_heated_bed))
				{
					bedTemperatureWidget.Visible = true;
				}
				else
				{
					bedTemperatureWidget.Visible = false;
				}
			}
		}

		private void UpdatePrintItemName()
		{
			if (PrinterConnectionAndCommunication.Instance.ActivePrintItem != null)
			{
				this.activePrintName.Text = PrinterConnectionAndCommunication.Instance.ActivePrintItem.GetFriendlyName();
			}
			else
			{
				this.activePrintName.Text = "No items in the print queue".Localize();
			}
		}

		private void UpdatePrintStatus()
		{
			string printLabel = "Next Print".Localize() + ":";
			string printerStatus = activePrintStatus.Text;

			if (PrinterConnectionAndCommunication.Instance.ActivePrintItem != null)
			{
				int totalSecondsInPrint = PrinterConnectionAndCommunication.Instance.TotalSecondsInPrint;

				int totalHoursInPrint = (int)(totalSecondsInPrint / (60 * 60));
				int totalMinutesInPrint = (int)(totalSecondsInPrint / 60 - totalHoursInPrint * 60);
				totalSecondsInPrint = totalSecondsInPrint % 60;

				string estimatedTimeLabel = "Estimated Print Time".Localize();
				string calculatingLabel = "Calculating...".Localize();
				string totalPrintTimeText;

				if (totalSecondsInPrint > 0)
				{
					if (totalHoursInPrint > 0)
					{
						totalPrintTimeText = $"{estimatedTimeLabel}: {totalHoursInPrint}h {totalMinutesInPrint:00}m {totalSecondsInPrint:00}s";
					}
					else
					{
						totalPrintTimeText = $"{estimatedTimeLabel}: {totalMinutesInPrint}m {totalSecondsInPrint:00}s";
					}
				}
				else
				{
					if (totalSecondsInPrint < 0)
					{
						totalPrintTimeText = "Streaming GCode...".Localize();
					}
					else
					{
						totalPrintTimeText = $"{estimatedTimeLabel}: {calculatingLabel}";
					}
				}

				switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
				{
					case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint:
						printLabel = "Preparing To Print".Localize() + ":";
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.Printing:
						printLabel = PrinterConnectionAndCommunication.Instance.PrintingStateString;
						printerStatus = totalPrintTimeText;
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.Paused:
						printLabel = "Printing Paused".Localize() + ":";
						printerStatus = totalPrintTimeText;
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.FinishedPrint:
						printLabel = "Done Printing".Localize() + ":";
						printerStatus = totalPrintTimeText;
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.Disconnected:
						printerStatus = "Not connected. Press 'Connect' to enable printing.".Localize();
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect:
						printerStatus = "Attempting to Connect".Localize() + "...";
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.ConnectionLost:
					case PrinterConnectionAndCommunication.CommunicationStates.FailedToConnect:
						printerStatus = "Connection Failed".Localize() + ": " + PrinterConnectionAndCommunication.Instance.ConnectionFailureMessage;
						break;

					default:
						printerStatus = ActiveSliceSettings.Instance.PrinterSelected ? "" : "Select a Printer.".Localize();
						break;
				}
			}
			else
			{
				printLabel = "Next Print".Localize() + ":";
				printerStatus = "Press 'Add' to choose an item to print".Localize();
			}

			activePrintLabel.Text = printLabel;
			activePrintStatus.Text = printerStatus;
		}
	}
}