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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace MatterHackers.MatterControl.ActionBar
{
	public class PrintStatusRow : FlowLayoutWidget
	{
		public static GuiWidget Create(QueueDataView queueDataView)
		{
			if (UserSettings.Instance.IsTouchScreen)
			{
				return new TouchScreenPrintStatusRow(queueDataView);
			}
			else
			{
				return new DesktopPrintStatusRow(queueDataView);
			}
		}
	}

	public class DesktopPrintStatusRow : PrintStatusRow
	{
		private TextWidget activePrintInfo;
		private TextWidget activePrintLabel;
		private TextWidget activePrintName;
		private PartThumbnailWidget activePrintPreviewImage;
		private TextWidget activePrintStatus;
		private TemperatureWidgetBase bedTemperatureWidget;
		private TemperatureWidgetBase extruderTemperatureWidget;
		private QueueDataView queueDataView;

		public DesktopPrintStatusRow(QueueDataView queueDataView)
		{
			Initialize();

			this.HAnchor = HAnchor.ParentLeftRight;

			this.queueDataView = queueDataView;

			AddChildElements();
			AddHandlers();

			onActivePrintItemChanged(null, null);
		}

		private EventHandler unregisterEvents;

		private string ActivePrintStatusText
		{
			set
			{
				if (activePrintStatus.Text != value)
				{
					activePrintStatus.Text = value;
				}
			}
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

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
		}

		protected void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onPrintItemChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.WroteLine.RegisterEvent(Instance_WroteLine, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);
		}

		protected void Initialize()
		{
			UiThread.RunOnIdle(OnIdle);
			this.Margin = new BorderDouble(6, 3, 6, 6);
		}

		protected void onPrintItemChanged(object sender, EventArgs e)
		{
			UpdatePrintItemName();
			UpdatePrintStatus();
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

			string nextPrintLabel = "Next Print".Localize();
			string nextPrintLabelFull = string.Format("{0}:", nextPrintLabel);
			activePrintLabel = getPrintStatusLabel(nextPrintLabelFull, pointSize: 11);
			activePrintLabel.VAnchor = VAnchor.ParentTop;

			topRow.AddChild(activePrintLabel);

			activePrintName = getPrintStatusLabel("this is the biggest name we will allow", pointSize: 14);
			activePrintName.AutoExpandBoundsToText = false;
			activePrintStatus = getPrintStatusLabel("this is the biggest label we will allow - bigger", pointSize: 11);
			activePrintStatus.AutoExpandBoundsToText = false;
			activePrintStatus.Text = "";
			activePrintStatus.Margin = new BorderDouble(top: 3);

			activePrintInfo = getPrintStatusLabel("", pointSize: 11);
			activePrintInfo.AutoExpandBoundsToText = true;

			PrintActionRow printActionRow = new PrintActionRow(queueDataView);

			container.AddChild(topRow);
			container.AddChild(activePrintName);
			container.AddChild(activePrintStatus);
			//container.AddChild(activePrintInfo);
			container.AddChild(printActionRow);

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

		private string getConnectionMessage()
		{
			if (!ActiveSliceSettings.Instance.PrinterSelected)
			{
				return "Select a Printer.".Localize();
			}
			else
			{
				switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
				{
					case PrinterConnectionAndCommunication.CommunicationStates.Disconnected:
						return "Not connected. Press 'Connect' to enable printing.".Localize();

					case PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect:
						string attemptToConnect = "Attempting to Connect".Localize();
						string attemptToConnectFull = string.Format("{0}...", attemptToConnect);
						return attemptToConnectFull;

					case PrinterConnectionAndCommunication.CommunicationStates.ConnectionLost:
					case PrinterConnectionAndCommunication.CommunicationStates.FailedToConnect:
						return "Unable to communicate with printer.".Localize();

					default:
						return "";
				}
			}
		}

		private TextWidget getPrintStatusLabel(string text, int pointSize)
		{
			TextWidget widget = new TextWidget(text, pointSize: pointSize);
			widget.TextColor = RGBA_Bytes.White;
			widget.AutoExpandBoundsToText = true;
			widget.MinimumSize = new Vector2(widget.Width, widget.Height);
			return widget;
		}

		private void Instance_WroteLine(object sender, EventArgs e)
		{
			UpdatePrintStatus();
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

		private void onStateChanged(object sender, EventArgs e)
		{
			UpdatePrintStatus();
		}

		private void PrintItem_SlicingOutputMessage(object sender, EventArgs e)
		{
			StringEventArgs message = e as StringEventArgs;
			ActivePrintStatusText = message.Data;
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
						totalPrintTimeText = string.Format("{0}", "Streaming GCode...".Localize());
					}
					else
					{
						totalPrintTimeText = $"{estimatedTimeLabel}: {calculatingLabel}";
					}
				}

				//GC.WaitForFullGCComplete();

				string printPercentRemainingText;
				string printPercentCompleteText = "complete".Localize();
				printPercentRemainingText = string.Format("{0:0.0}% {1}", PrinterConnectionAndCommunication.Instance.PercentComplete, printPercentCompleteText);

				switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
				{
					case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint:
						string preparingPrintLabel = "Preparing To Print".Localize();
						string preparingPrintLabelFull = string.Format("{0}:", preparingPrintLabel);
						activePrintLabel.Text = preparingPrintLabelFull;
						//ActivePrintStatusText = ""; // set by slicer
						activePrintInfo.Text = "";
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.Printing:
						{
							activePrintLabel.Text = PrinterConnectionAndCommunication.Instance.PrintingStateString;
							ActivePrintStatusText = totalPrintTimeText;
						}
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.Paused:
						{
							string activePrintLabelText = "Printing Paused".Localize();
							string activePrintLabelTextFull = string.Format("{0}:", activePrintLabelText);
							activePrintLabel.Text = activePrintLabelTextFull;
							ActivePrintStatusText = totalPrintTimeText;
						}
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.FinishedPrint:
						string donePrintingText = "Done Printing".Localize();
						string donePrintingTextFull = string.Format("{0}:", donePrintingText);
						activePrintLabel.Text = donePrintingTextFull;
						ActivePrintStatusText = totalPrintTimeText;
						break;

					default:
						string nextPrintLabelActive = "Next Print".Localize();
						string nextPrintLabelActiveFull = string.Format("{0}: ", nextPrintLabelActive);

						activePrintLabel.Text = nextPrintLabelActiveFull;
						ActivePrintStatusText = getConnectionMessage();
						break;
				}
			}
			else
			{
				string nextPrintLabel = "Next Print".Localize();
				string nextPrintLabelFull = string.Format("{0}:", nextPrintLabel);

				activePrintLabel.Text = nextPrintLabelFull;
				ActivePrintStatusText = string.Format("Press 'Add' to choose an item to print".Localize());
			}
		}
	}

	public class TouchScreenPrintStatusRow : PrintStatusRow
	{
		private TextWidget activePrintInfo;
		private TextWidget activePrintLabel;
		private TextWidget activePrintName;
		private PartThumbnailWidget activePrintPreviewImage;
		private TextWidget activePrintStatus;
		private TemperatureWidgetBase bedTemperatureWidget;
		private TemperatureWidgetBase extruderTemperatureWidget;
		private QueueDataView queueDataView;
		private Button setupButton;
		private Stopwatch timeSinceLastDrawTime = new Stopwatch();

		public TouchScreenPrintStatusRow(QueueDataView queueDataView)
		{
			Initialize();

			this.HAnchor = HAnchor.ParentLeftRight;

			this.queueDataView = queueDataView;

			AddChildElements();
			AddHandlers();

			onActivePrintItemChanged(null, null);
		}

		public delegate void AddIconToPrintStatusRowDelegate(GuiWidget iconContainer);

		private EventHandler unregisterEvents;
		private string ActivePrintStatusText
		{
			set
			{
				if (activePrintStatus.Text != value)
				{
					activePrintStatus.Text = value;
				}
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			timeSinceLastDrawTime.Restart();
			base.OnDraw(graphics2D);
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

		protected void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onPrintItemChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onStateChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.WroteLine.RegisterEvent((s, e) => UpdatePrintStatus(), ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);
		}

		protected void Initialize()
		{
			UiThread.RunOnIdle(OnIdle);
			this.Margin = new BorderDouble(6, 3, 0, 0);

			// Use top and right padding rather than margin to position controls but still
			// ensure corner click events can be caught in this control
			this.Padding = new BorderDouble(0, 0, 6, 6);
		}

		protected void onPrintItemChanged(object sender, EventArgs e)
		{
			UpdatePrintItemName();
			UpdatePrintStatus();
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

			PrintActionRow printActionRow = new PrintActionRow(queueDataView);
			printActionRow.VAnchor = VAnchor.ParentTop;

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

			string nextPrintLabel = "Next Print".Localize();
			string nextPrintLabelFull = string.Format("{0}:", nextPrintLabel);
			activePrintLabel = getPrintStatusLabel(nextPrintLabelFull, pointSize: 11);
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

				activePrintInfo = getPrintStatusLabel("", pointSize: 11);
				activePrintInfo.AutoExpandBoundsToText = true;

				labelContainer.AddChild(activePrintName);
				labelContainer.AddChild(activePrintStatus);
			}

			bottomRow.AddChild(activePrintPreviewImage);
			bottomRow.AddChild(labelContainer);

			//PrintActionRow printActionRow = new PrintActionRow(queueDataView);

			container.AddChild(topRow);
			container.AddChild(bottomRow);
			//container.AddChild(activePrintInfo);
			//container.AddChild(printActionRow);
			//container.AddChild(new VerticalSpacer());
			//container.AddChild(new MessageActionRow());

			return container;
		}

		private Button GetAutoLevelIndicator()
		{
			ImageButtonFactory imageButtonFactory = new ImageButtonFactory();
			imageButtonFactory.InvertImageColor = false;
			string notifyIconPath = Path.Combine("PrintStatusControls", "leveling-16x16.png");
			string notifyHoverIconPath = Path.Combine("PrintStatusControls", "leveling-16x16.png");
			Button autoLevelButton = imageButtonFactory.Generate(notifyIconPath, notifyHoverIconPath);
			autoLevelButton.Cursor = Cursors.Hand;
			autoLevelButton.Margin = new Agg.BorderDouble(top: 3);
			autoLevelButton.ToolTipText = "Print leveling is enabled.".Localize();
			autoLevelButton.Visible = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled);

			PrinterSettings.PrintLevelingEnabledChanged.RegisterEvent((sender, e) =>
			{
				autoLevelButton.Visible = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled);
			}, ref unregisterEvents);

			return autoLevelButton;
		}

		private string getConnectionMessage()
		{
			if (!ActiveSliceSettings.Instance.PrinterSelected)
			{
				return "Select a printer.".Localize();
			}
			else
			{
				switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
				{
					case PrinterConnectionAndCommunication.CommunicationStates.Disconnected:
						return "Not connected. Press 'Connect' to enable printing.".Localize();

					case PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect:
						return "Attempting to Connect".Localize() + "...";

					case PrinterConnectionAndCommunication.CommunicationStates.ConnectionLost:
					case PrinterConnectionAndCommunication.CommunicationStates.FailedToConnect:
						return "Unable to communicate with printer.".Localize();

					default:
						return "";
				}
			}
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
				if (!timeSinceLastDrawTime.IsRunning)
				{
					timeSinceLastDrawTime.Start();
				}
				else if (timeSinceLastDrawTime.ElapsedMilliseconds > 999)
				{
					UpdatePrintStatus();
					timeSinceLastDrawTime.Restart();
				}
			}

			if (!HasBeenClosed)
			{
				UiThread.RunOnIdle(OnIdle);
			}
		}

		private void onStateChanged(object sender, EventArgs e)
		{
			UpdatePrintStatus();
		}

		private void PrintItem_SlicingOutputMessage(object sender, EventArgs e)
		{
			StringEventArgs message = e as StringEventArgs;
			ActivePrintStatusText = message.Data;
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
			if (PrinterConnectionAndCommunication.Instance.ActivePrintItem != null)
			{
				int totalSecondsInPrint = PrinterConnectionAndCommunication.Instance.TotalSecondsInPrint;

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
						totalPrintTimeText = string.Format("{0}", "Streaming GCode...".Localize());
					}
					else
					{
						totalPrintTimeText = $"{estimatedTimeLabel}: {calculatingLabel}";
					}
				}

				//GC.WaitForFullGCComplete();

				string printPercentRemainingText;
				string printPercentCompleteText = "complete".Localize();
				printPercentRemainingText = string.Format("{0:0.0}% {1}", PrinterConnectionAndCommunication.Instance.PercentComplete, printPercentCompleteText);

				switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
				{
					case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint:
						string preparingPrintLabel = "Preparing To Print".Localize();
						string preparingPrintLabelFull = string.Format("{0}:", preparingPrintLabel);
						activePrintLabel.Text = preparingPrintLabelFull;
						//ActivePrintStatusText = ""; // set by slicer
						activePrintInfo.Text = "";
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.Printing:
						{
							activePrintLabel.Text = PrinterConnectionAndCommunication.Instance.PrintingStateString;
							ActivePrintStatusText = totalPrintTimeText;
						}
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.Paused:
						{
							string activePrintLabelText = "Printing Paused".Localize();
							string activePrintLabelTextFull = string.Format("{0}:", activePrintLabelText);
							activePrintLabel.Text = activePrintLabelTextFull;
							ActivePrintStatusText = totalPrintTimeText;
						}
						break;

					case PrinterConnectionAndCommunication.CommunicationStates.FinishedPrint:
						string donePrintingText = "Done Printing".Localize();
						string donePrintingTextFull = string.Format("{0}:", donePrintingText);
						activePrintLabel.Text = donePrintingTextFull;
						ActivePrintStatusText = totalPrintTimeText;
						break;

					default:
						string nextPrintLabelActive = "Next Print".Localize();
						string nextPrintLabelActiveFull = string.Format("{0}: ", nextPrintLabelActive);

						activePrintLabel.Text = nextPrintLabelActiveFull;
						ActivePrintStatusText = getConnectionMessage();
						break;
				}
			}
			else
			{
				string nextPrintLabel = "Next Print".Localize();
				string nextPrintLabelFull = string.Format("{0}:", nextPrintLabel);

				activePrintLabel.Text = nextPrintLabelFull;
				ActivePrintStatusText = string.Format("Press 'Add' to choose an item to print".Localize());
			}
		}
	}
}