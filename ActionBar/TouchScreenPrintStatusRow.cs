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
using System.IO;
using MatterHackers.Agg;
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
	public class TouchScreenPrintStatusRow : FlowLayoutWidget
	{
		private TextWidget activePrintLabel;
		private TextWidget activePrintName;
		private PartThumbnailWidget activePrintPreviewImage;
		private TextWidget activePrintStatus;
		private TemperatureWidgetBase bedTemperatureWidget;
		private TemperatureWidgetBase extruderTemperatureWidget;
		private EventHandler unregisterEvents;
		private Button setupButton;

		public TouchScreenPrintStatusRow()
		{
			UiThread.RunOnIdle(OnIdle);

			// Use top and right padding rather than margin to position controls but still
			// ensure corner click events can be caught in this control
			this.Padding = new BorderDouble(0, 0, 6, 6);
			this.Margin = new BorderDouble(6, 3, 0, 0);
			this.HAnchor = HAnchor.ParentLeftRight;

			AddChildElements();

			ApplicationController.Instance.ActivePrintItemChanged.RegisterEvent((s, e) =>
			{
				UpdatePrintItemName();
				UpdatePrintStatus();
			}, ref unregisterEvents);

			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				UpdatePrintStatus();
			}, ref unregisterEvents);

			PrinterConnection.Instance.WroteLine.RegisterEvent((s, e) =>
			{
				UpdatePrintStatus();
			}, ref unregisterEvents);

			ApplicationController.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);

			onActivePrintItemChanged(null, null);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			int boxSize = 20;

			// Handle errors in the touch panel that push touch event positions to the screen edge by
			// proxying all clicks in the target region back into the desired control
			RectangleDouble topRightHitbox = new RectangleDouble(this.Width - boxSize, this.Height - boxSize, this.Width, this.Height);
			if (topRightHitbox.Contains(mouseEvent.Position) && this.MouseCaptured)
			{
				setupButton.ClickButton(null);
				return;
			}

			base.OnMouseUp(mouseEvent);
		}

		private void AddChildElements()
		{
			FlowLayoutWidget tempWidgets = new FlowLayoutWidget();
			tempWidgets.VAnchor = VAnchor.ParentBottomTop;

			tempWidgets.Width = 120;

			extruderTemperatureWidget = new TemperatureWidgetExtruder();
			//extruderTemperatureWidget.Margin = new BorderDouble(right: 6);
			extruderTemperatureWidget.VAnchor = VAnchor.ParentTop;

			bedTemperatureWidget = new TemperatureWidgetBed();
			bedTemperatureWidget.VAnchor = VAnchor.ParentTop;

			tempWidgets.AddChild(extruderTemperatureWidget);
			tempWidgets.AddChild(new GuiWidget(6, 6));
			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_heated_bed))
			{
				tempWidgets.AddChild(bedTemperatureWidget);
			}
			tempWidgets.AddChild(new GuiWidget(6, 6));

			FlowLayoutWidget printStatusContainer = CreateActivePrinterInfoWidget();

			var printActionRow = new PrintActionRow(ApplicationController.Instance.Theme.BreadCrumbButtonFactory, this)
			{
				VAnchor = VAnchor.ParentTop
			};

			ImageButtonFactory factory = new ImageButtonFactory();
			factory.InvertImageColor = false;

			setupButton = factory.Generate(StaticData.Instance.LoadIcon("icon_gear_dot.png").InvertLightness(), null);
			setupButton.Margin = new BorderDouble(left: 6);
			setupButton.VAnchor = VAnchor.ParentCenter;
			setupButton.Click += (sender, e) =>
			{
				WizardWindow.Show<SetupOptionsPage>("/SetupOptions", "Setup Wizard");
				//WizardWindow.Show(true);
			};

			this.AddChild(printStatusContainer);
			this.AddChild(printActionRow);
			this.AddChild(tempWidgets);
			this.AddChild(setupButton);
			this.Height = 80;

			UpdatePrintStatus();
			UpdatePrintItemName();
		}

		private FlowLayoutWidget CreateActivePrinterInfoWidget()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(6, 0, 6, 0);
			container.HAnchor = HAnchor.ParentLeftRight;
			container.VAnchor = VAnchor.ParentCenter;
			container.Height = 80;

			FlowLayoutWidget topRow = new FlowLayoutWidget();
			topRow.Name = "PrintStatusRow.ActivePrinterInfo.TopRow";
			topRow.HAnchor = HAnchor.ParentLeftRight;

			activePrintLabel = getPrintStatusLabel("Next Print".Localize() + ":", pointSize: 11);
			activePrintLabel.VAnchor = VAnchor.ParentTop;

			topRow.AddChild(activePrintLabel);

			FlowLayoutWidget bottomRow = new FlowLayoutWidget();

			activePrintPreviewImage = new PartThumbnailWidget(null, "part_icon_transparent_100x100.png", "building_thumbnail_100x100.png", PartThumbnailWidget.ImageSizes.Size50x50);
			activePrintPreviewImage.VAnchor = VAnchor.ParentTop;
			activePrintPreviewImage.Padding = new BorderDouble(0);
			activePrintPreviewImage.HoverBackgroundColor = new RGBA_Bytes();
			activePrintPreviewImage.BorderWidth = 3;

			FlowLayoutWidget labelContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			labelContainer.VAnchor |= VAnchor.ParentTop;
			labelContainer.Margin = new BorderDouble(8, 0, 0, 4);
			{
				activePrintName = getPrintStatusLabel("this is the biggest name we will allow", pointSize: 14);
				activePrintName.AutoExpandBoundsToText = false;

				activePrintStatus = getPrintStatusLabel("this is the biggest label we will allow - bigger", pointSize: 11);
				activePrintStatus.AutoExpandBoundsToText = false;
				activePrintStatus.Text = "";
				activePrintStatus.Margin = new BorderDouble(top: 3);

				labelContainer.AddChild(activePrintName);
				labelContainer.AddChild(activePrintStatus);
			}

			bottomRow.AddChild(activePrintPreviewImage);
			bottomRow.AddChild(labelContainer);

			container.AddChild(topRow);
			container.AddChild(bottomRow);

			return container;
		}

		private Button GetAutoLevelIndicator()
		{
			ImageButtonFactory imageButtonFactory = new ImageButtonFactory();
			imageButtonFactory.InvertImageColor = false;
			string notifyIconPath = Path.Combine("PrintStatusControls", "leveling-16x16.png");
			string notifyHoverIconPath = Path.Combine("PrintStatusControls", "leveling-16x16.png");
			Button autoLevelButton = imageButtonFactory.Generate(notifyIconPath, notifyHoverIconPath);
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

			activePrintPreviewImage.ItemWrapper = ApplicationController.Instance.ActivePrintItem;

			// then hook up our new part
			if (activePrintPreviewImage.ItemWrapper != null)
			{
				activePrintPreviewImage.ItemWrapper.SlicingOutputMessage += PrintItem_SlicingOutputMessage;
			}

			activePrintPreviewImage.Invalidate();
		}

		private void OnIdle()
		{
			if (PrinterConnection.Instance.PrinterIsPrinting)
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
			if (ActiveSliceSettings.Instance != null)
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
			if (ApplicationController.Instance.ActivePrintItem != null)
			{
				this.activePrintName.Text = ApplicationController.Instance.ActivePrintItem.GetFriendlyName();
			}
			else
			{
				this.activePrintName.Text = "No items in the print queue".Localize();
			}
		}

		private void UpdatePrintStatus()
		{
			if (ApplicationController.Instance.ActivePrintItem != null)
			{
				int totalSecondsInPrint = PrinterConnection.Instance.TotalSecondsInPrint;

				int totalHoursInPrint = (int)(totalSecondsInPrint / (60 * 60));
				int totalMinutesInPrint = (int)(totalSecondsInPrint / 60 - totalHoursInPrint * 60);
				totalSecondsInPrint = totalSecondsInPrint % 60;

				string estimatedTimeLabel = "Est. Print Time".Localize();
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

				activePrintLabel.Text = "Next Print".Localize() + ":";

				switch (PrinterConnection.Instance.CommunicationState)
				{
					case CommunicationStates.PreparingToPrint:
						activePrintLabel.Text = "Preparing To Print".Localize() + ":";
						break;

					case CommunicationStates.Printing:
						activePrintLabel.Text = PrinterConnection.Instance.PrintingStateString;
						activePrintStatus.Text = totalPrintTimeText;
						break;

					case CommunicationStates.Paused:
						activePrintLabel.Text = "Printing Paused".Localize() + ":";
						activePrintStatus.Text = totalPrintTimeText;
						break;

					case CommunicationStates.FinishedPrint:
						activePrintLabel.Text = "Done Printing".Localize() + ":";
						activePrintStatus.Text = totalPrintTimeText;
						break;

					case CommunicationStates.Disconnected:
						activePrintStatus.Text = "Not connected. Press 'Connect' to enable printing.".Localize();
						break;

					case CommunicationStates.AttemptingToConnect:
						activePrintStatus.Text = "Attempting to Connect".Localize() + "...";
						break;

					case CommunicationStates.ConnectionLost:
					case CommunicationStates.FailedToConnect:
						activePrintStatus.Text = "Connection Failed".Localize() + ": " + PrinterConnection.Instance.ConnectionFailureMessage;
						break;

					default:
						activePrintStatus.Text = ActiveSliceSettings.Instance.PrinterSelected ? "" : "Select a Printer.".Localize();
						break;
				}
			}
			else
			{
				activePrintLabel.Text = "Next Print".Localize() + ":";
				activePrintStatus.Text = "Press 'Add' to choose an item to print".Localize();
			}
		}
	}
}