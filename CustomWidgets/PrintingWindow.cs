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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using MatterHackers.Agg;
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
	public class ProgressDial : GuiWidget
	{
		private double completedRatio = 0;
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

		public string ExtruderTemp { get; internal set; }
		public string BedTemp { get; internal set; }

		private RGBA_Bytes PrimaryAccentColor = ActiveTheme.Instance.PrimaryAccentColor;
		private RGBA_Bytes PrimaryAccentShade = ActiveTheme.Instance.PrimaryAccentColor.AdjustLightness(0.7).GetAsRGBA_Bytes();
		private double strokeWidth = 10;
		private double outerRingStrokeWidth = 7;

		private int layerCount = 0;
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
			this.Width = 230;
			this.Height = 230;

			percentCompleteWidget = new TextWidget("", pointSize: 22, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.ParentCenter,
				HAnchor = HAnchor.ParentCenter,
				Margin = new BorderDouble(bottom: 20)
			};

			layerCountWidget = new TextWidget("", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.ParentCenter,
				HAnchor = HAnchor.ParentCenter,
				Margin = new BorderDouble(top: 32)
			};

			this.AddChild(percentCompleteWidget);
			this.AddChild(layerCountWidget);

			borderStroke = new Stroke(new Ellipse(
				Vector2.Zero,
				borderRadius,
				borderRadius));

			borderColor = ActiveTheme.Instance.PrimaryTextColor;
			borderColor.Alpha0To1 = 0.3f;
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			borderRadius = this.LocalBounds.Width / 2 - padding;
			outerRingRadius = borderRadius - (outerRingStrokeWidth / 2) - 6; ;
			innerRingRadius = outerRingRadius - (outerRingStrokeWidth / 2) - (strokeWidth / 2);

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

		private static MoveButtonFactory buttonFactory = new MoveButtonFactory()
		{
			FontSize = 13,
		};

		static ZAxisControls()
		{
			buttonFactory.Colors.Fill.Normal = ActiveTheme.Instance.PrimaryAccentColor;
			buttonFactory.Colors.Fill.Hover = ActiveTheme.Instance.PrimaryAccentColor;
			buttonFactory.BorderWidth = 0;
			buttonFactory.Colors.Text.Normal = ActiveTheme.Instance.PrimaryTextColor;
		}

		public ZAxisControls() :
			base(FlowDirection.TopToBottom)
		{
			this.AddChild(new TextWidget("Z+", pointSize: 15, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				HAnchor = HAnchor.ParentCenter,
				Margin = new BorderDouble(bottom: 8)
			});

			Button button;

			button = CreateButton(1);
			button.Click += (s, e) => MoveZAxis(1.0);
			this.AddChild(button);

			button = CreateButton(.1);
			button.Click += (s, e) => MoveZAxis(0.1);
			this.AddChild(button);

			button = CreateButton(.02);
			button.Click += (s, e) => MoveZAxis(0.02);
			this.AddChild(button);

			this.AddChild(new ZTuningWidget()
			{
				HAnchor = HAnchor.ParentCenter,
				Margin = 10
			});

			button = CreateButton(-.02);
			button.Click += (s, e) => MoveZAxis(-0.02);
			this.AddChild(button);

			button = CreateButton(-.1);
			button.Click += (s, e) => MoveZAxis(-0.1);
			this.AddChild(button);

			button = CreateButton(-1);
			button.Click += (s, e) => MoveZAxis(1.0);
			this.AddChild(button);

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

		public void MoveZAxis(double moveAmount)
		{
			// Move by (moveAmount);
		}

		private Button CreateButton(double moveAmount, bool centerText = true)
		{
			var button = buttonFactory.GenerateMoveButton($"{Math.Abs(moveAmount):0.00} mm", PrinterConnectionAndCommunication.Axis.Z, MovementControls.ZSpeed);
			button.MoveAmount = moveAmount;
			button.HAnchor = HAnchor.Max_FitToChildren_ParentWidth;
			button.VAnchor = VAnchor.FitToChildren;
			button.Margin = new BorderDouble(0, 1);
			button.Padding = new BorderDouble(15, 7);
			button.Height = 35;
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

		AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();
		Stopwatch totalDrawTime = new Stopwatch();

		Action onCloseCallback;

		private static TextImageButtonFactory buttonFactory = new TextImageButtonFactory()
		{
			fontSize = 15,
			invertImageLocation = false,
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
				BackgroundColor = new RGBA_Bytes(200, 200, 200, 30)
			};
		}
	
		public PrintingWindow(Action onCloseCallback, bool mockMode = false)
			: base(1024, 600)
		{
			this.BackgroundColor = new RGBA_Bytes(35, 40, 49);
			this.onCloseCallback = onCloseCallback;
			Title = LocalizedString.Get("Print Monitor");

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
				BackgroundColor = new RGBA_Bytes(34, 38, 46),
				//DebugShowBounds = true
			};
			topToBottom.AddChild(actionBar);

			var logo = new ImageWidget(StaticData.Instance.LoadIcon(Path.Combine("Screensaver", "logo.png")));
			actionBar.AddChild(logo);

			actionBar.AddChild(new HorizontalSpacer());

			var pauseButton = CreateButton("Pause".Localize().ToUpper());
			var resumeButton = CreateButton("Resume".Localize().ToUpper());

			pauseButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					//PrinterConnectionAndCommunication.Instance.RequestPause();
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
					//PrinterConnectionAndCommunication.Instance.Resume();
					resumeButton.Visible = false;
					pauseButton.Visible = true;
				});
			};
			actionBar.AddChild(resumeButton);

			actionBar.AddChild(CreateVerticalLine());

			var cancelButton = CreateButton("Cancel".Localize().ToUpper());
			//cancelButton.Click += (sender, e) => UiThread.RunOnIdle(CancelButton_Click);
			actionBar.AddChild(cancelButton);

			actionBar.AddChild(CreateVerticalLine());
			
			var advancedButton = CreateButton("Advanced".Localize().ToUpper());
			actionBar.AddChild(advancedButton);

			var bodyContainer = new GuiWidget()
			{
				VAnchor = VAnchor.ParentBottomTop,
				HAnchor = HAnchor.ParentLeftRight,
				Padding = new BorderDouble(50),
				BackgroundColor = new RGBA_Bytes(35, 40, 49)
			};
			topToBottom.AddChild(bodyContainer);

			var bodyRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter,
				HAnchor = HAnchor.ParentLeftRight,
				//BackgroundColor = new RGBA_Bytes(125, 255, 46, 20),
			};
			bodyContainer.AddChild(bodyRow);

			var partThumbnail = new ImageWidget(StaticData.Instance.LoadIcon(Path.Combine("Screensaver", "part_thumbnail.png")))
			{
				Margin = new BorderDouble(50, 0)
			};
			bodyRow.AddChild(partThumbnail);

			bodyRow.AddChild(CreateVerticalLine());

			var progressContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(50, 0),
				VAnchor = VAnchor.ParentTop | VAnchor.FitToChildren,
				//BackgroundColor = new RGBA_Bytes(125, 255, 46, 20),
			};
			bodyRow.AddChild(progressContainer);

			progressDial = new ProgressDial()
			{
				HAnchor = HAnchor.ParentCenter,
				Height = 200
			};
			progressContainer.AddChild(progressDial);


			var timeContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(50, 3)
			};
			progressContainer.AddChild(timeContainer);

			var timeImage = new ImageWidget(StaticData.Instance.LoadIcon(Path.Combine("Screensaver", "time.png")));
			timeContainer.AddChild(timeImage);

			timeWidget = new TextWidget("", pointSize: 16)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(10, 0)
			};

			timeContainer.AddChild(timeWidget);

			printerName = new TextWidget(ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name), pointSize: 16)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(50, 3)
			};

			progressContainer.AddChild(printerName);

			partName = new TextWidget(PrinterConnectionAndCommunication.Instance.ActivePrintItem.GetFriendlyName(), pointSize: 16)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(50, 3)
			};
			progressContainer.AddChild(partName);

			bodyRow.AddChild(CreateVerticalLine());

			var widget = new ZAxisControls()
			{
				Margin = new BorderDouble(50, 0),
				HAnchor = HAnchor.AbsolutePosition,
				Width = 100
			};

			bodyRow.AddChild(widget);

			var footerBar = new FlowLayoutWidget (FlowDirection.LeftToRight) 
			{
				VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
				HAnchor = HAnchor.ParentCenter | HAnchor.FitToChildren,
				BackgroundColor = new RGBA_Bytes(35, 40, 49)
			};
			topToBottom.AddChild (footerBar);

			int extruderCount = 3;

			if (extruderCount == 1)
			{
				footerBar.AddChild(new ImageWidget(StaticData.Instance.LoadIcon(Path.Combine("Screensaver", "Extruder.png"))));
			}
			else
			{
				var columnA = new FlowLayoutWidget(FlowDirection.TopToBottom);
				footerBar.AddChild(columnA);

				var columnB = new FlowLayoutWidget(FlowDirection.TopToBottom);
				footerBar.AddChild(columnB);

				for (var i = 0; i < extruderCount; i++)
				{
					if (i % 2 == 0)
					{
						columnA.AddChild(new ImageWidget(StaticData.Instance.LoadIcon(Path.Combine("Screensaver", "Extruder.png"))));
					}
					else
					{
						columnB.AddChild(new ImageWidget(StaticData.Instance.LoadIcon(Path.Combine("Screensaver", "Extruder.png"))));
					}
				}
			}

			AnchorAll();

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
		}

		void CheckOnPrinter()
		{
			//if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
			{
				GetProgressInfo();
				UiThread.RunOnIdle(CheckOnPrinter, 1);
			}
			/*
			else
			{
				UiThread.RunOnIdle(Close);
			}
			*/
		}

		private void GetProgressInfo()
		{
			string timeString = "";
			int secondsPrinted = PrinterConnectionAndCommunication.Instance.SecondsPrinted;
			int hoursPrinted = (int)(secondsPrinted / (60 * 60));
			int minutesPrinted = (int)(secondsPrinted / 60 - hoursPrinted * 60);
			secondsPrinted = secondsPrinted % 60;

			if (hoursPrinted > 0)
			{
				timeString = string.Format("{0}:{1:00}:{2:00}",
					hoursPrinted,
					minutesPrinted,
					secondsPrinted);
			}
			else
			{
				timeString = string.Format("{0}:{1:00}",
					minutesPrinted,
					secondsPrinted);
			}

			timeWidget.Text = timeString;

			//progressDial.SomeText = "{0}: {1}    {2}: {3:.0}% {4}".FormatWith(timeTextString, timeString, progressString, PrinterConnectionAndCommunication.Instance.PercentComplete, completeString);

			bool hasHeatedBed = ActiveSliceSettings.Instance.GetValue<bool>("has_heated_bed");

			progressDial.ExtruderTemp = "Extruder: {0:0.0}° | {1}°".FormatWith(PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(0), PrinterConnectionAndCommunication.Instance.GetTargetExtruderTemperature(0));
			progressDial.BedTemp = !hasHeatedBed ?  "" : "Bed: {0:0.0}° | {1}°".FormatWith(PrinterConnectionAndCommunication.Instance.ActualBedTemperature, PrinterConnectionAndCommunication.Instance.TargetBedTemperature);

			progressDial.LayerCount = PrinterConnectionAndCommunication.Instance.CurrentlyPrintingLayer;
			progressDial.LayerCompletedRatio = PrinterConnectionAndCommunication.Instance.RatioIntoCurrentLayer;
			progressDial.CompletedRatio = PrinterConnectionAndCommunication.Instance.PercentComplete / 100;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			//totalDrawTime.Restart();
			base.OnDraw(graphics2D);

			//Vector2 center = new Vector2(Width/2, Height/2);
			//RectangleDouble thermometerRect = new RectangleDouble(center.x - Width/6, center.y - Height/32, center.x + Width/6, center.y + Height/32);
			//thermometerRect.Offset(0, -Height/4);
			//graphics2D.Rectangle(thermometerRect, ActiveTheme.Instance.PrimaryAccentColor);
			//RectangleDouble thermometerFill = new RectangleDouble(thermometerRect.Left, thermometerRect.Bottom, thermometerRect.Left + thermometerRect.Width * PrinterConnectionAndCommunication.Instance.PercentComplete / 100, thermometerRect.Top);
			//graphics2D.FillRectangle(thermometerFill, ActiveTheme.Instance.PrimaryAccentColor);

			//totalDrawTime.Stop();

			//millisecondTimer.Update((int)totalDrawTime.ElapsedMilliseconds);

			//millisecondTimer.Draw(graphics2D, this.Width * 3 / 4 - 15, this.Height - 120);
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

		public override void OnClosed(EventArgs e)
		{
			instance = null;
			base.OnClosed(e);
			onCloseCallback();
		}
	}
}