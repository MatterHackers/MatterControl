/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class BasicBody : GuiWidget
	{
		private TextWidget partName;
		private TextWidget printerName;
		private ProgressDial progressDial;
		private TextWidget timeWidget;
		private List<ExtruderStatusWidget> extruderStatusWidgets;

		private PrinterConfig printer;

		public BasicBody(PrinterConfig printer)
		{
			this.printer = printer;
			VAnchor = VAnchor.Stretch;
			HAnchor = HAnchor.Stretch;
		}

		private void CheckOnPrinter()
		{
			if (!HasBeenClosed)
			{
				GetProgressInfo();

				// Here for safety
				switch (printer.Connection.CommunicationState)
				{
					case CommunicationStates.PreparingToPrint:
					case CommunicationStates.Printing:
					case CommunicationStates.Paused:
						break;

					default:
						this.CloseOnIdle();
						break;
				}

				UiThread.RunOnIdle(CheckOnPrinter, 1);
			}
		}

		private void GetProgressInfo()
		{
			int secondsPrinted = printer.Connection.SecondsPrinted;
			int hoursPrinted = (int)(secondsPrinted / (60 * 60));
			int minutesPrinted = (secondsPrinted / 60 - hoursPrinted * 60);
			secondsPrinted = secondsPrinted % 60;

			// TODO: Consider if the consistency of a common time format would look and feel better than changing formats based on elapsed duration
			timeWidget.Text = (hoursPrinted <= 0) ? $"{minutesPrinted}:{secondsPrinted:00}" : $"{hoursPrinted}:{minutesPrinted:00}:{secondsPrinted:00}";

			progressDial.LayerCount = printer.Connection.CurrentlyPrintingLayer;
			progressDial.LayerCompletedRatio = printer.Connection.RatioIntoCurrentLayer;
			progressDial.CompletedRatio = printer.Connection.PercentComplete / 100;
		}

		public override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);

			bool smallScreen = Parent.Width <= 1180;

			Padding = smallScreen ? new BorderDouble(20, 5) : new BorderDouble(50, 30);

			var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch
			};
			AddChild(topToBottom);

			var bodyRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch,
				Margin = smallScreen ? new BorderDouble(30, 5, 30, 0) : new BorderDouble(30,20, 30, 0), // the -12 is to take out the top bar
			};
			topToBottom.AddChild(bodyRow);

			// Thumbnail section
			{
				int imageSize = smallScreen ? 300 : 500;

				// TODO: Undo this!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
				//ImageBuffer imageBuffer = PartThumbnailWidget.GetImageForItem(PrinterConnectionAndCommunication.Instance.ActivePrintItem, imageSize, imageSize);
				ImageBuffer imageBuffer = null;

				if (imageBuffer == null)
				{
					imageBuffer = AggContext.StaticData.LoadImage(Path.Combine("Images", "Screensaver", "part_thumbnail.png"));
				}

				WhiteToColor.DoWhiteToColor(imageBuffer, ActiveTheme.Instance.PrimaryAccentColor);

				var partThumbnail = new ImageWidget(imageBuffer)
				{
					VAnchor = VAnchor.Center,
					Margin = smallScreen ? new BorderDouble(right: 20) : new BorderDouble(right: 50),
				};
				bodyRow.AddChild(partThumbnail);
			}

			bodyRow.AddChild(PrintingWindow.CreateVerticalLine());

			// Progress section
			{
				var expandingContainer = new HorizontalSpacer()
				{
					VAnchor = VAnchor.Fit | VAnchor.Center
				};
				bodyRow.AddChild(expandingContainer);

				var progressContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Margin = new BorderDouble(50, 0),
					VAnchor = VAnchor.Center | VAnchor.Fit,
					HAnchor = HAnchor.Center | HAnchor.Fit,
				};
				expandingContainer.AddChild(progressContainer);

				progressDial = new ProgressDial()
				{
					HAnchor = HAnchor.Center,
					Height = 200 * DeviceScale,
					Width = 200 * DeviceScale
				};
				progressContainer.AddChild(progressDial);

				var timeContainer = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Center | HAnchor.Fit,
					Margin = 3
				};
				progressContainer.AddChild(timeContainer);

				var timeImage = AggContext.StaticData.LoadImage(Path.Combine("Images", "Screensaver", "time.png"));
				if (!ActiveTheme.Instance.IsDarkTheme)
				{
					timeImage.InvertLightness();
				}

				timeContainer.AddChild(new ImageWidget(timeImage));

				timeWidget = new TextWidget("", pointSize: 22, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					AutoExpandBoundsToText = true,
					Margin = new BorderDouble(10, 0, 0, 0),
					VAnchor = VAnchor.Center,
				};

				timeContainer.AddChild(timeWidget);

				int maxTextWidth = 350;
				printerName = new TextWidget(printer.Settings.GetValue(SettingsKey.printer_name), pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					HAnchor = HAnchor.Center,
					MinimumSize = new Vector2(maxTextWidth, MinimumSize.Y),
					Width = maxTextWidth,
					Margin = new BorderDouble(0, 3),
				};

				progressContainer.AddChild(printerName);

				partName = new TextWidget(printer.Bed.EditContext.SourceItem.Name, pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					HAnchor = HAnchor.Center,
					MinimumSize = new Vector2(maxTextWidth, MinimumSize.Y),
					Width = maxTextWidth,
					Margin = new BorderDouble(0, 3)
				};
				progressContainer.AddChild(partName);
			}

			bodyRow.AddChild(PrintingWindow.CreateVerticalLine());

			// ZControls
			{
				var widget = new ZAxisControls(printer, smallScreen)
				{
					Margin = new BorderDouble(left: 50),
					VAnchor = VAnchor.Center,
					Width = 135
				};
				bodyRow.AddChild(widget);
			}

			var footerBar = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				VAnchor = VAnchor.Bottom | VAnchor.Fit,
				HAnchor = HAnchor.Center | HAnchor.Fit,
				Margin = new BorderDouble(bottom: 0),
			};
			topToBottom.AddChild(footerBar);

			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			extruderStatusWidgets = Enumerable.Range(0, extruderCount).Select((i) => new ExtruderStatusWidget(printer, i)).ToList();

			bool hasHeatedBed = printer.Settings.GetValue<bool>("has_heated_bed");
			if (hasHeatedBed)
			{
				var extruderColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
				footerBar.AddChild(extruderColumn);

				// Add each status widget into the scene, placing into the appropriate column
				for (var i = 0; i < extruderCount; i++)
				{
					var widget = extruderStatusWidgets[i];
					widget.Margin = new BorderDouble(right: 20);
					extruderColumn.AddChild(widget);
				}

				footerBar.AddChild(new BedStatusWidget(printer, smallScreen)
				{
					VAnchor = VAnchor.Center,
				});
			}
			else
			{
				if (extruderCount == 1)
				{
					footerBar.AddChild(extruderStatusWidgets[0]);
				}
				else
				{
					var columnA = new FlowLayoutWidget(FlowDirection.TopToBottom);
					footerBar.AddChild(columnA);

					var columnB = new FlowLayoutWidget(FlowDirection.TopToBottom);
					footerBar.AddChild(columnB);

					// Add each status widget into the scene, placing into the appropriate column
					for (var i = 0; i < extruderCount; i++)
					{
						var widget = extruderStatusWidgets[i];
						if (i % 2 == 0)
						{
							widget.Margin = new BorderDouble(right: 20);
							columnA.AddChild(widget);
						}
						else
						{
							columnB.AddChild(widget);
						}
					}
				}
			}

			UiThread.RunOnIdle(() =>
			{
				CheckOnPrinter();
			});
		}
	}
}