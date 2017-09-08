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
using System.Diagnostics;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using static MatterHackers.MatterControl.JogControls;

namespace MatterHackers.MatterControl.CustomWidgets
{
	#region tempWidgets

	public class BedStatusWidget : TemperatureStatusWidget
	{
		public BedStatusWidget(PrinterConnection printerConnection, bool smallScreen)
			: base(printerConnection, smallScreen ? "Bed".Localize() : "Bed Temperature".Localize())
		{
			printerConnection.BedTemperatureRead.RegisterEvent((s, e) =>
			{
				UpdateTemperatures();
			}, ref unregisterEvents);
		}

		public override void UpdateTemperatures()
		{
			double targetValue = printerConnection.TargetBedTemperature;
			double actualValue = Math.Max(0, printerConnection.ActualBedTemperature);

			progressBar.RatioComplete = targetValue != 0 ? actualValue / targetValue : 1;

			this.actualTemp.Text = $"{actualValue:0}".PadLeft(3, (char)0x2007) + "°"; // put in padding spaces to make it at least 3 characters
			this.targetTemp.Text = $"{targetValue:0}".PadLeft(3, (char)0x2007) + "°"; // put in padding spaces to make it at least 3 characters
		}
	}

	public class ExtruderStatusWidget : TemperatureStatusWidget
	{
		private int extruderIndex;

		public ExtruderStatusWidget(PrinterConnection printerConnection, int extruderIndex)
			: base(printerConnection, $"{"Extruder".Localize()} {extruderIndex + 1}")
		{
			this.extruderIndex = extruderIndex;

			printerConnection.HotendTemperatureRead.RegisterEvent((s, e) =>
			{
				UpdateTemperatures();
			}, ref unregisterEvents);
		}

		public override void UpdateTemperatures()
		{
			double targetValue = printerConnection.GetTargetHotendTemperature(extruderIndex);
			double actualValue = Math.Max(0, printerConnection.GetActualHotendTemperature(extruderIndex));

			progressBar.RatioComplete = targetValue != 0 ? actualValue / targetValue : 1;

			this.actualTemp.Text = $"{actualValue:0}".PadLeft(3, (char)0x2007) + "°"; // put in padding spaces to make it at least 3 characters
			this.targetTemp.Text = $"{targetValue:0}".PadLeft(3, (char)0x2007) + "°"; // put in padding spaces to make it at least 3 characters
		}
	}

	public abstract class TemperatureStatusWidget : FlowLayoutWidget
	{
		protected TextWidget actualTemp;
		protected ProgressBar progressBar;
		protected TextWidget targetTemp;
		protected EventHandler unregisterEvents;
		private int fontSize = 14;
		protected PrinterConnection printerConnection;

		public TemperatureStatusWidget(PrinterConnection printerConnection, string dispalyName)
		{
			this.printerConnection = printerConnection;
			var extruderName = new TextWidget(dispalyName, pointSize: fontSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 8)
			};

			this.AddChild(extruderName);

			progressBar = new ProgressBar(200, 6)
			{
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
				Margin = new BorderDouble(right: 10),
				BorderColor = RGBA_Bytes.Transparent,
				BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 50),
				VAnchor = VAnchor.Center,
			};
			this.AddChild(progressBar);

			actualTemp = new TextWidget("", pointSize: fontSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 0),
				Width = 60
			};
			this.AddChild(actualTemp);

			this.AddChild(new VerticalLine()
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(8, 0)
			});

			targetTemp = new TextWidget("", pointSize: fontSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 8),
				Width = 60
			};
			this.AddChild(targetTemp);

			UiThread.RunOnIdle(UpdateTemperatures);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
		}

		public abstract void UpdateTemperatures();
	}

	#endregion tempWidgets

	public class PrintingWindow : SystemWindow
	{
		protected EventHandler unregisterEvents;
		private static PrintingWindow instance;

		private TextImageButtonFactory buttonFactory = new TextImageButtonFactory(new ButtonFactoryOptions()
		{
			FontSize = 15,
			InvertImageLocation = false,
			NormalTextColor = ActiveTheme.Instance.PrimaryTextColor,
			HoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
			DisabledTextColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 100),
			DisabledFillColor = RGBA_Bytes.Transparent,
			PressedTextColor = ActiveTheme.Instance.PrimaryTextColor
		});

		private AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();
		private Stopwatch totalDrawTime = new Stopwatch();
		GuiWidget bodyContainer;

		private BasicBody basicBody;
		PrinterConnection printerConnection;

		public PrintingWindow(PrinterConnection printerConnection)
			: base(1280, 750)
		{
			this.printerConnection = printerConnection;
		}

		public override void OnLoad(EventArgs args)
		{
			bool smallScreen = Parent.Width <= 1180;

			AlwaysOnTopOfMain = true;
			this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			this.Title = "Print Monitor".Localize();

			var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch
			};
			this.AddChild(topToBottom);

			topToBottom.AddChild(CreateActionBar(smallScreen));

			topToBottom.AddChild(CreateHorizontalLine());

			topToBottom.AddChild(CreateDropShadow());

			basicBody = new BasicBody(printerConnection);
			bodyContainer = new GuiWidget()
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch,
			};
			bodyContainer.AddChild(basicBody);
			topToBottom.AddChild(bodyContainer);

			base.OnLoad(args);
		}

		private GuiWidget CreateActionBar(bool smallScreen)
		{
			var actionBar = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				VAnchor = VAnchor.Top | VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor,
			};

			var mcLogo = AggContext.StaticData.LoadImage(Path.Combine("Images", "Screensaver", "logo.png"));
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				mcLogo.InvertLightness();
			}
			actionBar.AddChild(new ImageWidget(mcLogo));

			actionBar.AddChild(new HorizontalSpacer());

			// put in the pause button
			var pauseButton = CreateButton("Pause".Localize().ToUpper(), smallScreen);
			pauseButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					printerConnection.RequestPause();
				});
			};
			pauseButton.Enabled = printerConnection.PrinterIsPrinting
				&& !printerConnection.PrinterIsPaused;

			actionBar.AddChild(pauseButton);

			// put in the resume button
			var resumeButton = CreateButton("Resume".Localize().ToUpper(), smallScreen);
			resumeButton.Visible = false;
			resumeButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					if (printerConnection.PrinterIsPaused)
					{
						printerConnection.Resume();
					}
				});
			};
			actionBar.AddChild(resumeButton);

			actionBar.AddChild(CreateVerticalLine());

			// put in cancel button
			var cancelButton = CreateButton("Cancel".Localize().ToUpper(), smallScreen);
			cancelButton.Click += (s, e) =>
			{
				bool canceled = ApplicationController.Instance.ConditionalCancelPrint();
				if (canceled)
				{
					this.Close();
				}
			};
			cancelButton.Enabled = printerConnection.PrinterIsPrinting || printerConnection.PrinterIsPaused;
			actionBar.AddChild(cancelButton);

			actionBar.AddChild(CreateVerticalLine());

			// put in the reset button
			var resetButton = CreateButton("Reset".Localize().ToUpper(), smallScreen, true, AggContext.StaticData.LoadIcon("e_stop4.png", 32, 32));

			resetButton.Visible = printerConnection.PrinterSettings.GetValue<bool>(SettingsKey.show_reset_connection);
			resetButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(printerConnection.RebootBoard);
			};
			actionBar.AddChild(resetButton);

			actionBar.AddChild(CreateVerticalLine());

			var advancedButton = CreateButton("Advanced".Localize().ToUpper(), smallScreen);
			actionBar.AddChild(advancedButton);
			advancedButton.Click += (s, e) =>
			{
				bool inBasicMode = bodyContainer.Children[0] is BasicBody;
				if (inBasicMode)
				{
					bodyContainer.RemoveChild(basicBody);

					bodyContainer.AddChild(new ManualPrinterControls(printerConnection)
					{
						VAnchor = VAnchor.Stretch,
						HAnchor = HAnchor.Stretch
					});

					advancedButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				}
				else
				{
					bodyContainer.CloseAllChildren();

					basicBody.ClearRemovedFlag();
					bodyContainer.AddChild(basicBody);

					advancedButton.BackgroundColor = RGBA_Bytes.Transparent;
				}
			};

			printerConnection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				pauseButton.Enabled = printerConnection.PrinterIsPrinting
					&& !printerConnection.PrinterIsPaused;

				if(printerConnection.PrinterIsPaused)
				{
					resumeButton.Visible = true;
					pauseButton.Visible = false;
				}
				else
				{
					resumeButton.Visible = false;
					pauseButton.Visible = true;
				}

				// Close if not Preparing, Printing or Paused
				switch (printerConnection.CommunicationState)
				{
					case CommunicationStates.PreparingToPrint:
					case CommunicationStates.Printing:
					case CommunicationStates.Paused:
						break;

					default:
						this.CloseOnIdle();
						break;
				}
			}, ref unregisterEvents);

			printerConnection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				cancelButton.Enabled = printerConnection.PrinterIsPrinting || printerConnection.PrinterIsPaused;
			}, ref unregisterEvents);

			return actionBar;
		}

		public static bool IsShowing
		{
			get
			{
				if (instance != null)
				{
					return true;
				}

				return false;
			}
		}

		public static void Show(PrinterConnection printerConnection)
		{
			if (instance == null)
			{
				instance = new PrintingWindow(printerConnection);
				instance.ShowAsSystemWindow();
			}
			else
			{
				instance.BringToFront();
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			basicBody?.Close();
			unregisterEvents?.Invoke(this, null);
			instance = null;
			base.OnClosed(e);
		}

		private Button CreateButton(string localizedText, bool smallScreen, bool centerText = true, ImageBuffer icon = null)
		{
			Button button = null;
			if(icon == null)
			{
				button = buttonFactory.Generate(localizedText);
			}
			else
			{
				button = buttonFactory.Generate(localizedText, icon);
			}

			var bounds = button.LocalBounds;
			if (smallScreen)
			{
				bounds.Inflate(new BorderDouble(10, 10));
			}
			else
			{
				bounds.Inflate(new BorderDouble(40, 10));
			}
			button.LocalBounds = bounds;
			button.Cursor = Cursors.Hand;
			button.Margin = new BorderDouble(0);
			foreach(var child in button.Children)
			{
				child.VAnchor = VAnchor.Center;
			}
			button.VAnchor = VAnchor.Stretch;

			return button;
		}

		private GuiWidget CreateDropShadow()
		{
			var dropShadowWidget = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				Height = 12 * GuiWidget.DeviceScale,
				DoubleBuffer = true,
			};

			dropShadowWidget.AfterDraw += (s, e) =>
			{
				Byte[] buffer = dropShadowWidget.BackBuffer.GetBuffer();
				for (int y = 0; y < dropShadowWidget.Height; y++)
				{
					int yOffset = dropShadowWidget.BackBuffer.GetBufferOffsetY(y);
					byte alpha = (byte)((y / dropShadowWidget.Height) * 100);
					for (int x = 0; x < dropShadowWidget.Width; x++)
					{
						buffer[yOffset + x * 4 + 0] = 0;
						buffer[yOffset + x * 4 + 1] = 0;
						buffer[yOffset + x * 4 + 2] = 0;
						buffer[yOffset + x * 4 + 3] = alpha;
					}
				}
			};

			return dropShadowWidget;
		}

		public static HorizontalLine CreateHorizontalLine()
		{
			return new HorizontalLine()
			{
				BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 50)
			};
		}

		public static VerticalLine CreateVerticalLine()
		{
			return new VerticalLine()
			{
				BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 50)
			};
		}
	}

	public class BasicBody : GuiWidget
	{
		private TextWidget partName;
		private TextWidget printerName;
		private ProgressDial progressDial;
		private TextWidget timeWidget;
		private List<ExtruderStatusWidget> extruderStatusWidgets;
		PrinterConnection printerConnection;

		private void CheckOnPrinter()
		{
			if (!HasBeenClosed)
			{
				GetProgressInfo();

				// Here for safety
				switch (printerConnection.CommunicationState)
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
			int secondsPrinted = printerConnection.SecondsPrinted;
			int hoursPrinted = (int)(secondsPrinted / (60 * 60));
			int minutesPrinted = (secondsPrinted / 60 - hoursPrinted * 60);
			secondsPrinted = secondsPrinted % 60;

			// TODO: Consider if the consistency of a common time format would look and feel better than changing formats based on elapsed duration
			timeWidget.Text = (hoursPrinted <= 0) ? $"{minutesPrinted}:{secondsPrinted:00}" : $"{hoursPrinted}:{minutesPrinted:00}:{secondsPrinted:00}";

			progressDial.LayerCount = printerConnection.CurrentlyPrintingLayer;
			progressDial.LayerCompletedRatio = printerConnection.RatioIntoCurrentLayer;
			progressDial.CompletedRatio = printerConnection.PercentComplete / 100;
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
				printerName = new TextWidget(printerConnection.PrinterSettings.GetValue(SettingsKey.printer_name), pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					HAnchor = HAnchor.Center,
					MinimumSize = new Vector2(maxTextWidth, MinimumSize.y),
					Width = maxTextWidth,
					Margin = new BorderDouble(0, 3),
				};

				progressContainer.AddChild(printerName);

				partName = new TextWidget(ApplicationController.Instance.ActivePrintItem.GetFriendlyName(), pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					HAnchor = HAnchor.Center,
					MinimumSize = new Vector2(maxTextWidth, MinimumSize.y),
					Width = maxTextWidth,
					Margin = new BorderDouble(0, 3)
				};
				progressContainer.AddChild(partName);
			}

			bodyRow.AddChild(PrintingWindow.CreateVerticalLine());

			// ZControls
			{
				var widget = new ZAxisControls(printerConnection, smallScreen)
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

			int extruderCount = printerConnection.PrinterSettings.GetValue<int>(SettingsKey.extruder_count);

			extruderStatusWidgets = Enumerable.Range(0, extruderCount).Select((i) => new ExtruderStatusWidget(printerConnection, i)).ToList();

			bool hasHeatedBed = printerConnection.PrinterSettings.GetValue<bool>("has_heated_bed");
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

				footerBar.AddChild(new BedStatusWidget(printerConnection, smallScreen)
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

		public BasicBody(PrinterConnection printerConnection)
		{
			VAnchor = VAnchor.Stretch;
			HAnchor = HAnchor.Stretch;
		}
	}

	public class ProgressDial : GuiWidget
	{
		private RGBA_Bytes borderColor;
		private Stroke borderStroke;
		private double completedRatio = -1;
		private double innerRingRadius;

		private double layerCompletedRatio = 0;

		private int layerCount = -1;

		private TextWidget layerCountWidget;

		private double outerRingRadius;

		private double outerRingStrokeWidth = 7 * DeviceScale;

		private TextWidget percentCompleteWidget;

		private RGBA_Bytes PrimaryAccentColor = ActiveTheme.Instance.PrimaryAccentColor;

		private RGBA_Bytes PrimaryAccentShade = ActiveTheme.Instance.PrimaryAccentColor.AdjustLightness(0.7).GetAsRGBA_Bytes();

		private double innerRingStrokeWidth = 10 * GuiWidget.DeviceScale;

		public ProgressDial()
		{
			percentCompleteWidget = new TextWidget("", pointSize: 22, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(bottom: 20)
			};

			CompletedRatio = 0;

			layerCountWidget = new TextWidget("", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(top: 32)
			};

			LayerCount = 0;

			this.AddChild(percentCompleteWidget);
			this.AddChild(layerCountWidget);

			borderColor = ActiveTheme.Instance.PrimaryTextColor;
			borderColor.Alpha0To1 = 0.3f;
		}

		public double CompletedRatio
		{
			get { return completedRatio; }
			set
			{
				if (completedRatio != value)
				{
					completedRatio = Math.Min(value, 1);

					// Flag for redraw
					this.Invalidate();

					percentCompleteWidget.Text = $"{CompletedRatio * 100:0}%";
				}
			}
		}

		public double LayerCompletedRatio
		{
			get { return layerCompletedRatio; }
			set
			{
				if (layerCompletedRatio != value)
				{
					layerCompletedRatio = value;
					this.Invalidate();
				}
			}
		}

		public int LayerCount
		{
			get { return layerCount; }
			set
			{
				if (layerCount != value)
				{
					layerCount = value;
					if (layerCount == 0)
					{
						layerCountWidget.Text = "Printing".Localize() + "...";
					}
					else
					{
						layerCountWidget.Text = "Layer".Localize() + " " + layerCount;
					}
				}
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			double borderRadius = this.LocalBounds.Width / 2 - 20 * DeviceScale;
			outerRingRadius = borderRadius - (outerRingStrokeWidth / 2) - 6 * DeviceScale;
			innerRingRadius = outerRingRadius - (outerRingStrokeWidth / 2) - (innerRingStrokeWidth / 2);

			borderStroke = new Stroke(new Ellipse(Vector2.Zero, borderRadius, borderRadius));

			base.OnBoundsChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var bounds = this.LocalBounds;

			// Draw border ring
			graphics2D.Render(
				borderStroke.Translate(bounds.Center),
				borderColor);

			// Draw outer progress ring
			var ringArc = new Arc(
				Vector2.Zero,
				new Vector2(outerRingRadius, outerRingRadius),
				0,
				MathHelper.DegreesToRadians(360) * LayerCompletedRatio, // percentCompletedInRadians
				Arc.Direction.ClockWise);

			var arcStroke = new Stroke(ringArc);
			arcStroke.width(outerRingStrokeWidth);
			graphics2D.Render(
				arcStroke.Rotate(90, AngleType.Degrees).Translate(bounds.Center),
				PrimaryAccentShade);

			// Draw inner progress ring
			ringArc = new Arc(
				Vector2.Zero,
				new Vector2(innerRingRadius, innerRingRadius),
				0,
				MathHelper.DegreesToRadians(360) * CompletedRatio, // percentCompletedInRadians
				Arc.Direction.ClockWise);
			arcStroke = new Stroke(ringArc);
			arcStroke.width(innerRingStrokeWidth);
			graphics2D.Render(
				arcStroke.Rotate(90, AngleType.Degrees).Translate(bounds.Center),
				PrimaryAccentColor);

			// Draw child controls
			base.OnDraw(graphics2D);
		}
	}

	public class ZAxisControls : FlowLayoutWidget
	{
		/*
		private static TextImageButtonFactory buttonFactory = new TextImageButtonFactory()
		{
			fontSize = 13,
			invertImageLocation = false,
			hoverFillColor = ActiveTheme.Instance.PrimaryAccentColor,
			//pressedFillColor = ActiveTheme.Instance.PrimaryAccentColor.AdjustLightness(0.8).GetAsRGBA_Bytes()
		};
		*/

		private MoveButtonFactory buttonFactory = new MoveButtonFactory()
		{
			FontSize = 13,
		};

		public ZAxisControls(PrinterConnection printerConnection, bool smallScreen) :
			base(FlowDirection.TopToBottom)
		{
			buttonFactory.Colors.Fill.Normal = ActiveTheme.Instance.PrimaryAccentColor;
			buttonFactory.Colors.Fill.Hover = ActiveTheme.Instance.PrimaryAccentColor;
			buttonFactory.BorderWidth = 0;
			buttonFactory.Colors.Text.Normal = RGBA_Bytes.White;

			this.AddChild(new TextWidget("Z+", pointSize: smallScreen ? 12 : 15, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(bottom: 8)
			});

			this.AddChild(CreateZMoveButton(printerConnection, .1, smallScreen));

			this.AddChild(CreateZMoveButton(printerConnection, .02, smallScreen));

			this.AddChild(new ZTuningWidget(printerConnection.PrinterSettings, false)
			{
				HAnchor = HAnchor.Center | HAnchor.Fit,
				Margin = 10
			});

			this.AddChild(CreateZMoveButton(printerConnection, -.02, smallScreen));

			this.AddChild(CreateZMoveButton(printerConnection, -.1, smallScreen));

			this.AddChild(new TextWidget("Z-", pointSize: smallScreen ? 12 : 15, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(top: 9),
			});

			//this.BackgroundColor = new RGBA_Bytes(200, 0, 0, 30);

			this.Margin = new BorderDouble(0);
			this.Margin = 0;
			this.Padding = 3;
			this.VAnchor = VAnchor.Fit | VAnchor.Top;
		}

		private Button CreateZMoveButton(PrinterConnection printerConnection, double moveAmount, bool smallScreen)
		{
			var button = buttonFactory.GenerateMoveButton(printerConnection, $"{Math.Abs(moveAmount):0.00} mm", PrinterConnection.Axis.Z, printerConnection.PrinterSettings.ZSpeed());
			button.MoveAmount = moveAmount;
			button.HAnchor = HAnchor.MaxFitOrStretch;
			button.VAnchor = VAnchor.Fit;
			button.Margin = new BorderDouble(0, 1);
			button.Padding = new BorderDouble(15, 7);
			if (smallScreen) button.Height = 45; else button.Height = 55;
			button.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

			return button;
		}
	}
}