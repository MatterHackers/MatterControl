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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	using System.Collections.Generic;
	using Agg.Image;
	using CustomWidgets;
	using MatterHackers.Agg.Platform;
	using MatterHackers.MatterControl.PartPreviewWindow;
	using MatterHackers.VectorMath;

	public class ThemeConfig
	{
		protected static readonly int DefaultScrollBarWidth = 120;

		private static ImageBuffer restoreNormal;
		private static ImageBuffer restoreHover;
		private static ImageBuffer restorePressed;

		private readonly int fizedHeightA = (int)(25 * GuiWidget.DeviceScale + .5);
		private readonly double fizedHeightB = 34 * GuiWidget.DeviceScale;

		private readonly int fontSize10 = 10;
		private readonly int fontSize11 = 11;
		private readonly int fontSize12 = 12;
		private readonly int fontSize14 = 14;

		private int shortButtonHeight = 25;
		private int sideBarButtonWidth;

		public int H1PointSize { get; set; } = 16;

		public LinkButtonFactory LinkButtonFactory { get; private set; }
		public LinkButtonFactory HelpLinkFactory { get; private set; }

		public TextImageButtonFactory ExpandMenuOptionFactory;
		public TextImageButtonFactory WhiteButtonFactory;

		private readonly int borderWidth = 1;

		public TextImageButtonFactory ButtonFactory { get; private set; }
		public TextImageButtonFactory SmallMarginButtonFactory { get; private set; }
		public TextImageButtonFactory RadioButtons { get; private set; }
		public TextImageButtonFactory WizardButtons { get; private set; }

		/// <summary>
		/// Used to make buttons in menu rows where the background color is consistently white
		/// </summary>
		public TextImageButtonFactory MenuButtonFactory { get; private set; }

		/// <summary>
		/// Used in the Update wizard to show high contrast, primary action buttons
		/// </summary>
		public TextImageButtonFactory GrayButtonFactory { get; private set; }

		public TextImageButtonFactory imageConverterExpandMenuOptionFactory;

		internal void SetPrinterTabStyles(PrinterTab printerTab)
		{
			printerTab.Margin = new BorderDouble(10, 0, 0, 5);
			printerTab.Padding = new BorderDouble(8, 4, 12, 6);
		}

		public TextImageButtonFactory imageConverterButtonFactory;

		public RGBA_Bytes TabBodyBackground => new RGBA_Bytes(ActiveTheme.Instance.TertiaryBackgroundColor, 175);

		public TextImageButtonFactory ViewControlsButtonFactory { get; private set; }
		public RGBA_Bytes SplitterBackground { get; private set; } = new RGBA_Bytes(0, 0, 0, 60);
		public int SplitterWidth => (int)(7 * (GuiWidget.DeviceScale <= 1 ? GuiWidget.DeviceScale : GuiWidget.DeviceScale * 1.4));

		public RGBA_Bytes SlightShade { get; } = new RGBA_Bytes(0, 0, 0, 40);
		public RGBA_Bytes MinimalShade { get; } = new RGBA_Bytes(0, 0, 0, 15);

		public TextImageButtonFactory DisableableControlBase { get; private set; }
		public TextImageButtonFactory HomingButtons { get; private set; }
		public TextImageButtonFactory MicroButton { get; private set; }
		public TextImageButtonFactory MicroButtonMenu { get; private set; }

		public BorderDouble ButtonSpacing { get; set; } = new BorderDouble(3, 0, 0, 0);
		public TextImageButtonFactory NoMarginWhite { get; private set; }
		public BorderDouble ToolbarPadding { get; set; } = 3;
		public RGBA_Bytes PrimaryTabFillColor { get; internal set; }

		private EventHandler unregisterEvents;

		static ThemeConfig()
		{
			// EnsureRestoreButtonImages
			int size = (int)(16 * GuiWidget.DeviceScale);

			if (AggContext.OperatingSystem == OSType.Android)
			{
				restoreNormal = ColorCircle(size, new RGBA_Bytes(200, 0, 0));
			}
			else
			{
				restoreNormal = ColorCircle(size, new RGBA_Bytes(128, 128, 128));
			}
			restoreHover = ColorCircle(size, new RGBA_Bytes(200, 0, 0));
			restorePressed = ColorCircle(size, new RGBA_Bytes(255, 0, 0));
		}

		public ThemeConfig()
		{
			ActiveTheme.ThemeChanged.RegisterEvent((s, e) => RebuildTheme(), ref unregisterEvents);
			RebuildTheme();
		}

		public void RebuildTheme()
		{
			var theme = ActiveTheme.Instance;

			DefaultThumbView.ThumbColor = new RGBA_Bytes(theme.PrimaryTextColor, 30);

			var commonOptions = new ButtonFactoryOptions();
			commonOptions.NormalTextColor = theme.PrimaryTextColor;
			commonOptions.HoverTextColor = theme.PrimaryTextColor;
			commonOptions.PressedTextColor = theme.PrimaryTextColor;
			commonOptions.DisabledTextColor = theme.TertiaryBackgroundColor;
			commonOptions.Margin = new BorderDouble(14, 0);
			commonOptions.FontSize = 11;
			commonOptions.ImageSpacing = 8;
			commonOptions.BorderWidth = 0;
			commonOptions.FixedHeight = 32;

			this.ButtonFactory = new TextImageButtonFactory(commonOptions);

			this.NoMarginWhite = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				Margin = 0,
				AllowThemeToAdjustImage = false
			});

			this.SmallMarginButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				Margin = new BorderDouble(8, 0),
				ImageSpacing = 6
			});

			this.WizardButtons = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
#if __ANDROID__
				FontSize = fontSize14,
				FixedHeight = fizedHeightB,
				Margin = commonOptions.Margin * 1.2
#endif
			});

			this.RadioButtons = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				BorderWidth = 1,
				CheckedBorderColor = RGBA_Bytes.White,
				AllowThemeToAdjustImage = false
			});

			var commonGray = new ButtonFactoryOptions(commonOptions)
			{
				NormalTextColor = RGBA_Bytes.Black,
				NormalFillColor = RGBA_Bytes.LightGray,
				HoverTextColor = RGBA_Bytes.Black,
				PressedTextColor = RGBA_Bytes.Black,
				PressedFillColor = RGBA_Bytes.LightGray,
			};

			this.MenuButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonGray)
			{
				Margin = new BorderDouble(8, 0)
			});

			this.GrayButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				NormalTextColor = theme.PrimaryTextColor,
				NormalFillColor = RGBA_Bytes.Gray
			});

			int viewControlsButtonHeight = (UserSettings.Instance.IsTouchScreen) ? 40 : 0;

			this.ViewControlsButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				DisabledTextColor = theme.PrimaryTextColor,
				FixedHeight = viewControlsButtonHeight,
				FixedWidth = viewControlsButtonHeight,
				AllowThemeToAdjustImage = false,
				CheckedBorderColor = RGBA_Bytes.White
			});

			this.MicroButton = new TextImageButtonFactory(new ButtonFactoryOptions()
			{
				FixedHeight = 20 * GuiWidget.DeviceScale,
				FixedWidth = 30 * GuiWidget.DeviceScale,
				FontSize = 8,
				Margin = 0,
				CheckedBorderColor = ActiveTheme.Instance.PrimaryTextColor
			});

			this.MicroButtonMenu = new TextImageButtonFactory(new ButtonFactoryOptions(commonGray)
			{
				FixedHeight = 20 * GuiWidget.DeviceScale,
				FixedWidth = 30 * GuiWidget.DeviceScale,
				FontSize = 8,
				Margin = 0,
				BorderWidth = 1,
				CheckedBorderColor = RGBA_Bytes.Black
			});

#region PartPreviewWidget
			if (UserSettings.Instance.IsTouchScreen)
			{
				sideBarButtonWidth = 180;
				shortButtonHeight = 40;
			}
			else
			{
				sideBarButtonWidth = 138;
				shortButtonHeight = 30;
			}

			WhiteButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				FixedWidth = sideBarButtonWidth,
				FixedHeight = shortButtonHeight,

				NormalTextColor = RGBA_Bytes.Black,
				NormalFillColor = RGBA_Bytes.White,
				NormalBorderColor = new RGBA_Bytes(theme.PrimaryTextColor, 200),

				HoverTextColor = RGBA_Bytes.Black,
				HoverFillColor = new RGBA_Bytes(255, 255, 255, 200),
				HoverBorderColor = new RGBA_Bytes(theme.PrimaryTextColor, 200),

				BorderWidth = 1,
			});

			ExpandMenuOptionFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				HoverTextColor = theme.PrimaryTextColor,
				HoverFillColor = new RGBA_Bytes(255, 255, 255, 50),

				PressedTextColor = theme.PrimaryTextColor,
				PressedFillColor = new RGBA_Bytes(255, 255, 255, 50),

				DisabledTextColor = theme.PrimaryTextColor,
				DisabledFillColor = new RGBA_Bytes(255, 255, 255, 50),
				FixedWidth = sideBarButtonWidth,
			});

#endregion

#region ImageConverter
			imageConverterButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				FixedWidth = 185,
				FixedHeight = 30,

				NormalFillColor = RGBA_Bytes.White,
				NormalTextColor = RGBA_Bytes.Black,
				NormalBorderColor = new RGBA_Bytes(theme.PrimaryTextColor, 200),

				HoverFillColor = new RGBA_Bytes(255, 255, 255, 200),
				HoverTextColor = RGBA_Bytes.Black,
				HoverBorderColor = new RGBA_Bytes(theme.PrimaryTextColor, 200),

				BorderWidth = 1,
			});

			imageConverterExpandMenuOptionFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				FixedWidth = 200,

				NormalTextColor = theme.PrimaryTextColor,

				HoverTextColor = theme.PrimaryTextColor,
				HoverFillColor = new RGBA_Bytes(255, 255, 255, 50),

				DisabledTextColor = theme.PrimaryTextColor,
				DisabledFillColor = new RGBA_Bytes(255, 255, 255, 50),

				PressedTextColor = theme.PrimaryTextColor,
				PressedFillColor = new RGBA_Bytes(255, 255, 255, 50),
			});

			// TODO: Need to remain based default ButtonFactionOptions constructor until reviewed for styling issues
			var disableableControlOptions = new ButtonFactoryOptions()
			{
				NormalFillColor = RGBA_Bytes.White,
				NormalTextColor = RGBA_Bytes.Black,
				HoverTextColor = theme.PrimaryTextColor,
				DisabledFillColor = RGBA_Bytes.White,
				DisabledTextColor = RGBA_Bytes.DarkGray,
				PressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				FixedHeight = 25 * GuiWidget.DeviceScale,
				FontSize = 11
			};

			this.DisableableControlBase = new TextImageButtonFactory(disableableControlOptions);
			this.HomingButtons = new TextImageButtonFactory(new ButtonFactoryOptions(disableableControlOptions)
			{
				BorderWidth = 1,
				NormalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				HoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				NormalFillColor = new RGBA_Bytes(180, 180, 180),
			});

#endregion

			this.LinkButtonFactory = new LinkButtonFactory()
			{
				fontSize = fontSize11,
				textColor = theme.PrimaryTextColor
			};

			this.HelpLinkFactory = new LinkButtonFactory()
			{
				fontSize = fontSize10,
				textColor = theme.SecondaryAccentColor
			};
			this.PrimaryTabFillColor = new RGBA_Bytes(RGBA_Bytes.White, ActiveTheme.Instance.IsDarkTheme ?  20 : 60);
		}

		public FlowLayoutWidget CreatePopupMenu(IEnumerable<NamedAction> menuActions)
		{
			var widgetToPop = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = RGBA_Bytes.White
			};

			// Create menu items in the DropList for each element in this.menuActions
			foreach (var menuAction in menuActions)
			{
				MenuItem menuItem;

				if (menuAction.Title == "----")
				{
					menuItem = PartPreviewWindow.OverflowDropdown.CreateHorizontalLine();
				}
				else
				{
					menuItem = PartPreviewWindow.OverflowDropdown.CreateMenuItem((string)menuAction.Title);
					menuItem.Name = $"{menuAction.Title} Menu Item";
				}

				menuItem.Enabled = menuAction.Action != null;
				menuItem.ClearRemovedFlag();

				if (menuItem.Enabled)
				{
					menuItem.Click += (s, e) =>
					{
						menuAction.Action();
					};
				}

				widgetToPop.AddChild(menuItem);
			}

			return widgetToPop;
		}


		internal TabControl CreateTabControl(int height = 1)
		{
			var tabControl = new TabControl(separator: new HorizontalLine(alpha: 50, height: height));
			tabControl.TabBar.BorderColor = RGBA_Bytes.Transparent; // theme.SecondaryTextColor;
			tabControl.TabBar.Margin = 0;
			tabControl.TabBar.Padding = 0;
			tabControl.TextPointSize = fontSize12;

			return tabControl;
		}

		private static ImageBuffer ColorCircle(int size, RGBA_Bytes color)
		{
			ImageBuffer imageBuffer = new ImageBuffer(size, size);
			Graphics2D normalGraphics = imageBuffer.NewGraphics2D();
			Vector2 center = new Vector2(size / 2.0, size / 2.0);
			normalGraphics.Circle(center, size / 2.0, color);
			normalGraphics.Line(center + new Vector2(-size / 4.0, -size / 4.0), center + new Vector2(size / 4.0, size / 4.0), RGBA_Bytes.White, 2 * GuiWidget.DeviceScale);
			normalGraphics.Line(center + new Vector2(-size / 4.0, size / 4.0), center + new Vector2(size / 4.0, -size / 4.0), RGBA_Bytes.White, 2 * GuiWidget.DeviceScale);

			return imageBuffer;
		}

		internal Button CreateSmallResetButton()
		{
			return new Button(
				new ButtonViewStates(
					new ImageWidget(restoreNormal),
					new ImageWidget(restoreHover),
					new ImageWidget(restorePressed),
					new ImageWidget(restoreNormal)))
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(0, 0, 5, 0)
			};
		}

		public SolidSlider CreateSolidSlider(GuiWidget wordOptionContainer, string header, double min = 0, double max = .5)
		{
			double scrollBarWidth = 10;
			if (UserSettings.Instance.IsTouchScreen)
			{
				scrollBarWidth = 20;
			}

			TextWidget spacingText = new TextWidget(header, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(10, 3, 3, 5),
				HAnchor = HAnchor.Left
			};
			wordOptionContainer.AddChild(spacingText);

			SolidSlider namedSlider = new SolidSlider(new Vector2(), scrollBarWidth, 0, 1)
			{
				TotalWidthInPixels = DefaultScrollBarWidth,
				Minimum = min,
				Maximum = max,
				Margin = new BorderDouble(3, 5, 3, 3),
				HAnchor = HAnchor.Center,
			};
			namedSlider.View.BackgroundColor = new RGBA_Bytes();

			wordOptionContainer.AddChild(namedSlider);

			return namedSlider;
		}

		public MenuItem CreateCheckboxMenuItem(string text, string itemValue, bool itemChecked, BorderDouble padding, EventHandler eventHandler)
		{
			var checkbox = new CheckBox(text)
			{
				Checked = itemChecked
			};
			checkbox.CheckedStateChanged += eventHandler;

			return new MenuItem(checkbox, itemValue)
			{
				Padding = padding,
			};
		}
	}
}