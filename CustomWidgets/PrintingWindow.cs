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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using static MatterHackers.MatterControl.JogControls;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class TemperatureStatusWidget : FlowLayoutWidget
	{
		int fontSize = 14;

		protected TextWidget targetTemp;
		protected TextWidget actualTemp;

		protected ProgressBar progressBar;

		public TemperatureStatusWidget(string dispalyName)
		{
			var extruderName = new TextWidget(dispalyName, pointSize: fontSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(right: 8)
			};

			this.AddChild(extruderName);

			progressBar = new ProgressBar(200, 6)
			{
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
				Margin = new BorderDouble(right: 10),
				BorderColor = RGBA_Bytes.Transparent,
				BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 50),
				VAnchor = VAnchor.ParentCenter,
			};
			this.AddChild(progressBar);

			actualTemp = new TextWidget("", pointSize: fontSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(right: 0),
				Width = 60
			};
			this.AddChild(actualTemp);

			this.AddChild(new VerticalLine()
			{
				BackgroundColor = new RGBA_Bytes(200, 200, 200),
				Margin = new BorderDouble(right: 8)
			});

			targetTemp = new TextWidget("", pointSize: fontSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(right: 8),
				Width = 60
			};
			this.AddChild(targetTemp);
		}
	}

	public class ExtruderStatusWidget : TemperatureStatusWidget
	{
		private int extruderIndex;

		public ExtruderStatusWidget(int extruderIndex)
			: base($"{"Extruder".Localize()} {extruderIndex + 1}")
		{
			this.extruderIndex = extruderIndex;

			UiThread.RunOnIdle(UpdateTemperatures);
		}

		public void UpdateTemperatures()
		{
			double targetValue = PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(extruderIndex);
			double actualValue = PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(extruderIndex);

			progressBar.RatioComplete = actualValue / targetValue;

			this.actualTemp.Text = $"{actualValue:0.#}°";
			this.targetTemp.Text = $"{targetValue:0.#}°";
		}
	}

	public class BedStatusWidget : TemperatureStatusWidget
	{
		public BedStatusWidget()
			: base("Bed Temperature".Localize())
		{
			UiThread.RunOnIdle(UpdateTemperatures);
		}

		public void UpdateTemperatures()
		{
			double targetValue = PrinterConnectionAndCommunication.Instance.TargetBedTemperature;
			double actualValue = PrinterConnectionAndCommunication.Instance.ActualBedTemperature;

			progressBar.RatioComplete = actualValue / targetValue;

			this.actualTemp.Text = $"{actualValue:0.#}°";
			this.targetTemp.Text = $"{targetValue:0.#}°";
		}
	}

	public class ProgressDial : GuiWidget
	{
		private double completedRatio = -1;
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

		private double layerCompletedRatio = 0;
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

		private RGBA_Bytes PrimaryAccentColor = ActiveTheme.Instance.PrimaryAccentColor;
		private RGBA_Bytes PrimaryAccentShade = ActiveTheme.Instance.PrimaryAccentColor.AdjustLightness(0.7).GetAsRGBA_Bytes();
		private double strokeWidth = 10;
		private double outerRingStrokeWidth = 7;

		private int layerCount = -1;
		public int LayerCount
		{
			get { return layerCount; }
			set
			{
				if (layerCount != value)
				{
					layerCount = value;
					layerCountWidget.Text = "Layer " + layerCount;
				}
			}
		}

		private TextWidget percentCompleteWidget;
		private TextWidget layerCountWidget;
		private Stroke borderStroke;
		private RGBA_Bytes borderColor;
		private double borderRadius;

		private int padding = 20;
		private double outerRingRadius = 100;
		private double innerRingRadius = 90;

		public ProgressDial()
		{
			percentCompleteWidget = new TextWidget("", pointSize: 22, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.ParentCenter,
				HAnchor = HAnchor.ParentCenter,
				Margin = new BorderDouble(bottom: 20)
			};

			CompletedRatio = 0;

			layerCountWidget = new TextWidget("", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.ParentCenter,
				HAnchor = HAnchor.ParentCenter,
				Margin = new BorderDouble(top: 32)
			};

			LayerCount = 0;

			this.AddChild(percentCompleteWidget);
			this.AddChild(layerCountWidget);

			borderColor = ActiveTheme.Instance.PrimaryTextColor;
			borderColor.Alpha0To1 = 0.3f;
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			borderRadius = this.LocalBounds.Width / 2 - padding;
			outerRingRadius = borderRadius - (outerRingStrokeWidth / 2) - 6; ;
			innerRingRadius = outerRingRadius - (outerRingStrokeWidth / 2) - (strokeWidth / 2);

			Console.WriteLine("width: {3} - border: {0}, outer: {1}, inner: {2}", borderRadius, outerRingRadius, innerRingRadius, this.LocalBounds.Width);

			borderStroke = new Stroke(new Ellipse(
				Vector2.Zero,
				borderRadius,
				borderRadius));

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
			arcStroke.width(strokeWidth);
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

		public ZAxisControls() :
			base(FlowDirection.TopToBottom)
		{
			buttonFactory.Colors.Fill.Normal = ActiveTheme.Instance.PrimaryAccentColor;
			buttonFactory.Colors.Fill.Hover = ActiveTheme.Instance.PrimaryAccentColor;
			buttonFactory.BorderWidth = 0;
			buttonFactory.Colors.Text.Normal = ActiveTheme.Instance.PrimaryTextColor;

			this.AddChild(new TextWidget("Z+", pointSize: 15, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				HAnchor = HAnchor.ParentCenter,
				Margin = new BorderDouble(bottom: 8)
			});


			this.AddChild(CreateZMoveButton(1));

			this.AddChild(CreateZMoveButton(.1));

			this.AddChild(CreateZMoveButton(.02));

			this.AddChild(new ZTuningWidget()
			{
				HAnchor = HAnchor.ParentCenter,
				Margin = 10
			});

			this.AddChild(CreateZMoveButton(-.02));

			this.AddChild(CreateZMoveButton(-.1));

			this.AddChild(CreateZMoveButton(-1));

			this.AddChild(new TextWidget("Z-", pointSize: 15, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				HAnchor = HAnchor.ParentCenter,
				Margin = new BorderDouble(top: 9),
			});

			//this.BackgroundColor = new RGBA_Bytes(200, 0, 0, 30);

			this.Margin = new BorderDouble(0);
			this.Margin = 0;
			this.Padding = 3;
			this.VAnchor = VAnchor.FitToChildren | VAnchor.ParentTop;
		}

		private Button CreateZMoveButton(double moveAmount, bool centerText = true)
		{
			var button = buttonFactory.GenerateMoveButton($"{Math.Abs(moveAmount):0.00} mm", PrinterConnectionAndCommunication.Axis.Z, MovementControls.ZSpeed);
			button.MoveAmount = moveAmount;
			button.HAnchor = HAnchor.Max_FitToChildren_ParentWidth;
			button.VAnchor = VAnchor.FitToChildren;
			button.Margin = new BorderDouble(0, 1);
			button.Padding = new BorderDouble(15, 7);
			button.Height = 55;
			button.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

			return button;
		}
	}

	public class PrintingWindow : SystemWindow
	{
		private static PrintingWindow instance;
		private ProgressDial progressDial;
		private TextWidget timeWidget;
		private TextWidget printerName;
		private TextWidget partName;

		private List<ExtruderStatusWidget> extruderStatusWidgets;

		private EventHandler unregisterEvents;

		AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();
		Stopwatch totalDrawTime = new Stopwatch();

		Action onCloseCallback;

		private TextImageButtonFactory buttonFactory = new TextImageButtonFactory()
		{
			fontSize = 15,
			invertImageLocation = false,
			normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
			hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
			disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
			pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
		};

		private Button CreateButton(string localizedText, bool centerText = true)
		{
			var button = buttonFactory.Generate(localizedText, centerText: centerText);
			button.Cursor = Cursors.Hand;
			button.Margin = new BorderDouble(40, 10);
			button.VAnchor = VAnchor.ParentCenter;

			return button;
		}

		public void MockProgress()
		{
			if (progressDial.CompletedRatio >= 1)
			{
				progressDial.CompletedRatio = 0;
				progressDial.LayerCount = 0;
			}
			else
			{
				progressDial.CompletedRatio = Math.Min(progressDial.CompletedRatio + 0.01, 1);
			}

			if (progressDial.LayerCompletedRatio >= 1)
			{
				progressDial.LayerCompletedRatio = 0;
				progressDial.LayerCount += 1;
			}
			else
			{
				progressDial.LayerCompletedRatio = Math.Min(progressDial.LayerCompletedRatio + 0.1, 1);
			}

			UiThread.RunOnIdle(MockProgress, .2);
		}

		private VerticalLine CreateVerticalLine()
		{
			return new VerticalLine()
			{
				BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 50)
			};
		}

		private GuiWidget CreateDropShadow()
		{
			var dropShadowWidget = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
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
		private HorizontalLine CreateHorizontalLine()
		{
			return new HorizontalLine()
			{
				BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 50)
			};
		}

		public PrintingWindow(Action onCloseCallback, bool mockMode = false)
			: base(1280, 750)
		{
			AlwaysOnTopOfMain = true;
			this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			this.onCloseCallback = onCloseCallback;
			this.Title = "Print Monitor".Localize();

			var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.ParentBottomTop,
				HAnchor = HAnchor.ParentLeftRight
			};
			this.AddChild(topToBottom);

			var actionBar = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				VAnchor = VAnchor.ParentTop | VAnchor.FitToChildren,
				HAnchor = HAnchor.ParentLeftRight,
				//BackgroundColor = new RGBA_Bytes(34, 38, 46),
			};
			topToBottom.AddChild(actionBar);

			var mcLogo = StaticData.Instance.LoadImage(Path.Combine("Images", "Screensaver", "logo.png"));
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				mcLogo.InvertLightness();
			}
			actionBar.AddChild(new ImageWidget(mcLogo));

			actionBar.AddChild(new HorizontalSpacer());

			var pauseButton = CreateButton("Pause".Localize().ToUpper());
			var resumeButton = CreateButton("Resume".Localize().ToUpper());

			pauseButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					PrinterConnectionAndCommunication.Instance.RequestPause();
					pauseButton.Visible = false;
					resumeButton.Visible = true;
				});
			};
			actionBar.AddChild(pauseButton);

			resumeButton.Visible = false;
			resumeButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					if (PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
					{
						PrinterConnectionAndCommunication.Instance.Resume();
					}

					resumeButton.Visible = false;
					pauseButton.Visible = true;
				});
			};
			actionBar.AddChild(resumeButton);

			actionBar.AddChild(CreateVerticalLine());

			var cancelButton = CreateButton("Cancel".Localize().ToUpper());
			cancelButton.Click += (s, e) =>
			{
				bool canceled = ApplicationController.Instance.ConditionalCancelPrint();
				if (canceled)
				{
					this.Close();
				}
			};
			actionBar.AddChild(cancelButton);

			actionBar.AddChild(CreateVerticalLine());

			var advancedButton = CreateButton("Advanced".Localize().ToUpper());
			actionBar.AddChild(advancedButton);

			topToBottom.AddChild(CreateHorizontalLine());

			topToBottom.AddChild(CreateDropShadow());

			var bodyContainer = new GuiWidget()
			{
				VAnchor = VAnchor.ParentBottomTop,
				HAnchor = HAnchor.ParentLeftRight,
				Padding = new BorderDouble(50, 30),
				//BackgroundColor = new RGBA_Bytes(35, 40, 49),
			};
			topToBottom.AddChild(bodyContainer);

			var bodyRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				VAnchor = VAnchor.ParentBottomTop,
				HAnchor = HAnchor.ParentLeftRight,
				//BackgroundColor = new RGBA_Bytes(125, 255, 46, 20),
			};
			bodyContainer.AddChild(bodyRow);

			// Thumbnail section
			{
				ImageBuffer imageBuffer = null;

				string stlHashCode = PrinterConnectionAndCommunication.Instance.ActivePrintItem?.FileHashCode.ToString();
				if (!string.IsNullOrEmpty(stlHashCode) && stlHashCode != "0")
				{
					imageBuffer = PartThumbnailWidget.LoadImageFromDisk(stlHashCode);
					if (imageBuffer != null)
					{
						imageBuffer = ImageBuffer.CreateScaledImage(imageBuffer, 500, 500);
					}
				}

				if (imageBuffer == null)
				{
					imageBuffer = StaticData.Instance.LoadImage(Path.Combine("Images", "Screensaver", "part_thumbnail.png"));
				}

				var partThumbnail = new ImageWidget(imageBuffer)
				{
					VAnchor = VAnchor.ParentCenter,
					Margin = new BorderDouble(right: 50)
				};
				bodyRow.AddChild(partThumbnail);
			}

			bodyRow.AddChild(CreateVerticalLine());

			// Progress section
			{
				var expandingContainer = new HorizontalSpacer()
				{
					VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter
				};
				bodyRow.AddChild(expandingContainer);

				var progressContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Margin = new BorderDouble(50, 0),
					VAnchor = VAnchor.ParentCenter | VAnchor.FitToChildren,
					HAnchor = HAnchor.ParentCenter | HAnchor.FitToChildren,
					//BackgroundColor = new RGBA_Bytes(125, 255, 46, 20),
				};
				expandingContainer.AddChild(progressContainer);

				progressDial = new ProgressDial()
				{
					HAnchor = HAnchor.ParentCenter,
					Height = 200,
					Width = 200
				};
				progressContainer.AddChild(progressDial);

				var timeContainer = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.ParentCenter | HAnchor.FitToChildren,
					Margin = 3
				};
				progressContainer.AddChild(timeContainer);

				var timeImage = StaticData.Instance.LoadImage(Path.Combine("Images", "Screensaver", "time.png"));
				if (!ActiveTheme.Instance.IsDarkTheme)
				{
					timeImage.InvertLightness();
				}

				timeContainer.AddChild(new ImageWidget(timeImage));

				timeWidget = new TextWidget("", pointSize: 22, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					AutoExpandBoundsToText = true,
					Margin = new BorderDouble(10, 0, 0, 0),
					VAnchor = VAnchor.ParentCenter,
				};

				timeContainer.AddChild(timeWidget);

				printerName = new TextWidget(ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name), pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					AutoExpandBoundsToText = true,
					HAnchor = HAnchor.ParentLeftRight,
					Margin = new BorderDouble(50, 3)
				};

				progressContainer.AddChild(printerName);

				partName = new TextWidget(PrinterConnectionAndCommunication.Instance.ActivePrintItem.GetFriendlyName(), pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					AutoExpandBoundsToText = true,
					HAnchor = HAnchor.ParentLeftRight,
					Margin = new BorderDouble(50, 3)
				};
				progressContainer.AddChild(partName);
			}

			bodyRow.AddChild(CreateVerticalLine());

			// ZControls
			{
				var widget = new ZAxisControls()
				{
					Margin = new BorderDouble(left: 50),
					VAnchor = VAnchor.ParentCenter,
					Width = 135
				};
				bodyRow.AddChild(widget);
			}

			var footerBar = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
				HAnchor = HAnchor.ParentCenter | HAnchor.FitToChildren,
				//BackgroundColor = new RGBA_Bytes(35, 40, 49),
				Margin = new BorderDouble(bottom: 30)
			};
			topToBottom.AddChild (footerBar);

			int extruderCount = mockMode ? 3 : ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);

			extruderStatusWidgets = Enumerable.Range(0, extruderCount).Select((i) => new ExtruderStatusWidget(i)).ToList();

			bool hasHeatedBed = ActiveSliceSettings.Instance.GetValue<bool>("has_heated_bed");
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

				footerBar.AddChild(new BedStatusWidget()
				{
					VAnchor = VAnchor.ParentCenter,
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
				if (mockMode)
				{
					MockProgress();
				}
				else
				{
					CheckOnPrinter();
				}
			});

			PrinterConnectionAndCommunication.Instance.ExtruderTemperatureRead.RegisterEvent((s, e) =>
			{
				var eventArgs = e as TemperatureEventArgs;
				if (eventArgs != null && eventArgs.Index0Based < extruderStatusWidgets.Count)
				{
					extruderStatusWidgets[eventArgs.Index0Based].UpdateTemperatures();
				}
			}, ref unregisterEvents);
		}

		void CheckOnPrinter()
		{
			GetProgressInfo();
			UiThread.RunOnIdle(CheckOnPrinter, 1);
		}

		private void GetProgressInfo()
		{
			int secondsPrinted = PrinterConnectionAndCommunication.Instance.SecondsPrinted;
			int hoursPrinted = (int)(secondsPrinted / (60 * 60));
			int minutesPrinted = (secondsPrinted / 60 - hoursPrinted * 60);
			secondsPrinted = secondsPrinted % 60;

			// TODO: Consider if the consistency of a common time format would look and feel better than changing formats based on elapsed duration 
			timeWidget.Text = (hoursPrinted <= 0) ? $"{minutesPrinted}:{secondsPrinted:00}" : $"{hoursPrinted}:{minutesPrinted:00}:{secondsPrinted:00}";

			progressDial.LayerCount = PrinterConnectionAndCommunication.Instance.CurrentlyPrintingLayer;
			progressDial.LayerCompletedRatio = PrinterConnectionAndCommunication.Instance.RatioIntoCurrentLayer;
			progressDial.CompletedRatio = PrinterConnectionAndCommunication.Instance.PercentComplete / 100;
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

		public static void Show(Action onCloseCallback)
		{
			if (instance == null)
			{
				instance = new PrintingWindow(onCloseCallback);
				instance.ShowAsSystemWindow();
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);

			instance = null;
			base.OnClosed(e);
			onCloseCallback();
		}
	}
}