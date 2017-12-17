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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class PrintingWindow : SystemWindow
	{
		protected EventHandler unregisterEvents;
		private static PrintingWindow instance;

		private TextImageButtonFactory buttonFactory = new TextImageButtonFactory(new ButtonFactoryOptions()
		{
			FontSize = 15,
			NormalTextColor = ActiveTheme.Instance.PrimaryTextColor,
			HoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
			DisabledTextColor = new Color(ActiveTheme.Instance.PrimaryTextColor, 100),
			DisabledFillColor = Color.Transparent,
			PressedTextColor = ActiveTheme.Instance.PrimaryTextColor
		});

		private AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();
		private Stopwatch totalDrawTime = new Stopwatch();
		private GuiWidget bodyContainer;

		private BasicBody basicBody;

		private PrinterConfig printer;

		public PrintingWindow(PrinterConfig printer)
			: base(1280, 750)
		{
			this.printer = printer;
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

			basicBody = new BasicBody(printer);
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
			var pauseButton = CreateButton("Pause".Localize(), smallScreen);
			pauseButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					printer.Connection.RequestPause();
				});
			};
			pauseButton.Enabled = printer.Connection.PrinterIsPrinting
				&& !printer.Connection.PrinterIsPaused;

			actionBar.AddChild(pauseButton);

			// put in the resume button
			var resumeButton = CreateButton("Resume".Localize(), smallScreen);
			resumeButton.Visible = false;
			resumeButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					if (printer.Connection.PrinterIsPaused)
					{
						printer.Connection.Resume();
					}
				});
			};
			actionBar.AddChild(resumeButton);

			actionBar.AddChild(CreateVerticalLine());

			// put in cancel button
			var cancelButton = CreateButton("Cancel".Localize(), smallScreen);
			cancelButton.Click += (s, e) =>
			{
				bool canceled = ApplicationController.Instance.ConditionalCancelPrint();
				if (canceled)
				{
					this.Close();
				}
			};
			cancelButton.Enabled = printer.Connection.PrinterIsPrinting || printer.Connection.PrinterIsPaused;
			actionBar.AddChild(cancelButton);

			actionBar.AddChild(CreateVerticalLine());

			// put in the reset button
			var resetButton = CreateButton("Reset".Localize(), smallScreen, true, AggContext.StaticData.LoadIcon("e_stop4.png", 32, 32));

			resetButton.Visible = printer.Settings.GetValue<bool>(SettingsKey.show_reset_connection);
			resetButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(printer.Connection.RebootBoard);
			};
			actionBar.AddChild(resetButton);

			actionBar.AddChild(CreateVerticalLine());

			var advancedButton = CreateButton("Advanced".Localize(), smallScreen);
			actionBar.AddChild(advancedButton);
			advancedButton.Click += (s, e) =>
			{
				bool inBasicMode = bodyContainer.Children[0] is BasicBody;
				if (inBasicMode)
				{
					bodyContainer.RemoveChild(basicBody);

					bodyContainer.AddChild(new ManualPrinterControls(printer)
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

					advancedButton.BackgroundColor = Color.Transparent;
				}
			};

			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				pauseButton.Enabled = printer.Connection.PrinterIsPrinting
					&& !printer.Connection.PrinterIsPaused;

				if(printer.Connection.PrinterIsPaused)
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
			}, ref unregisterEvents);

			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				cancelButton.Enabled = printer.Connection.PrinterIsPrinting || printer.Connection.PrinterIsPaused;
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

		public static void Show(PrinterConfig printer)
		{
			if (instance == null)
			{
				instance = new PrintingWindow(printer);
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
				BackgroundColor = new Color(ActiveTheme.Instance.PrimaryTextColor, 50)
			};
		}

		public static VerticalLine CreateVerticalLine()
		{
			return new VerticalLine()
			{
				BackgroundColor = new Color(ActiveTheme.Instance.PrimaryTextColor, 50)
			};
		}
	}
}