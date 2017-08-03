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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	using Agg.Image;
	using CustomWidgets;
	using MatterHackers.VectorMath;

	public class ThemeConfig
	{
		protected static readonly int DefaultScrollBarWidth = 120;

		private static ImageBuffer restoreNormal;
		private static ImageBuffer restoreHover;
		private static ImageBuffer restorePressed;

		private readonly int fizedHeightA = (int)(25 * GuiWidget.DeviceScale + .5);
		private readonly double fizedHeightB = 52 * GuiWidget.DeviceScale;

		private readonly int fontSize10 = 10;
		private readonly int fontSize11 = 11;
		private readonly int fontSize12 = 12;

		private int shortButtonHeight = 25;
		private int sideBarButtonWidth;

		public LinkButtonFactory LinkButtonFactory { get; private set; }
		public LinkButtonFactory HelpLinkFactory { get; private set; }

		public TextImageButtonFactory ExpandMenuOptionFactory;
		public TextImageButtonFactory WhiteButtonFactory;

		private readonly int borderWidth = 1;

		public TextImageButtonFactory ButtonFactory { get; private set; }
		public TextImageButtonFactory SmallMarginButtonFactory { get; private set; }

		/// <summary>
		/// Used to make buttons in menu rows where the background color is consistently white
		/// </summary>
		public TextImageButtonFactory MenuButtonFactory { get; private set; }

		/// <summary>
		/// Used in the Update wizard to show high contrast, primary action buttons
		/// </summary>
		public TextImageButtonFactory GrayButtonFactory { get; private set; }

		public TextImageButtonFactory imageConverterExpandMenuOptionFactory;
		public TextImageButtonFactory imageConverterButtonFactory;

		public RGBA_Bytes TabBodyBackground => new RGBA_Bytes(ActiveTheme.Instance.TertiaryBackgroundColor, 175);

		public TextImageButtonFactory ViewControlsButtonFactory { get; private set; }
		public RGBA_Bytes SplitterBackground { get; private set; } = new RGBA_Bytes(0, 0, 0, 60);
		public int SplitterWidth => (int)(7 * (GuiWidget.DeviceScale <= 1 ? GuiWidget.DeviceScale : GuiWidget.DeviceScale * 1.4));

		public RGBA_Bytes SlightShade { get; } = new RGBA_Bytes(0, 0, 0, 40);
		public TextImageButtonFactory GCodeLayerButtons { get; private set; }

		private EventHandler unregisterEvents;

		static ThemeConfig()
		{
			// EnsureRestoreButtonImages
			int size = (int)(16 * GuiWidget.DeviceScale);

			if (OsInformation.OperatingSystem == OSType.Android)
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

			DefaultThumbView.ThumbColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 30);

			this.ButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = theme.PrimaryTextColor,
				hoverTextColor = theme.PrimaryTextColor,
				pressedTextColor = theme.PrimaryTextColor,
				disabledTextColor = theme.TertiaryBackgroundColor,
				Margin = new BorderDouble(16, 0),
				borderWidth = 0,
				FixedHeight = 32,
			};

			this.SmallMarginButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = theme.PrimaryTextColor,
				hoverTextColor = theme.PrimaryTextColor,
				pressedTextColor = theme.PrimaryTextColor,
				disabledTextColor = theme.TertiaryBackgroundColor,
				Margin = new BorderDouble(8, 0),
				borderWidth = 0,
				FixedHeight = 32,
			};

			this.MenuButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = RGBA_Bytes.Black,
				hoverTextColor = RGBA_Bytes.Black,
				pressedTextColor = RGBA_Bytes.Black,
				disabledTextColor = theme.TertiaryBackgroundColor,
				normalFillColor = RGBA_Bytes.LightGray,
				pressedFillColor = RGBA_Bytes.LightGray,
				Margin = new BorderDouble(8, 0),
				borderWidth = 0,
				FixedHeight = 32,
			};

			this.GrayButtonFactory = new TextImageButtonFactory()
			{
				normalFillColor = RGBA_Bytes.Gray,
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor
			};

			int buttonHeight;
			if (UserSettings.Instance.IsTouchScreen)
			{
				buttonHeight = 40;
			}
			else
			{
				buttonHeight = 0;
			}

			this.ViewControlsButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				FixedHeight = buttonHeight,
				FixedWidth = buttonHeight,
				AllowThemeToAdjustImage = false,
				checkedBorderColor = RGBA_Bytes.White
			};

			this.GCodeLayerButtons = new TextImageButtonFactory()
			{
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor
			};

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

			WhiteButtonFactory = new TextImageButtonFactory()
			{
				FixedWidth = sideBarButtonWidth,
				FixedHeight = shortButtonHeight,

				normalFillColor = RGBA_Bytes.White,
				normalTextColor = RGBA_Bytes.Black,
				hoverTextColor = RGBA_Bytes.Black,

				hoverFillColor = new RGBA_Bytes(255, 255, 255, 200),
				borderWidth = 1,

				normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200)
			};

			ExpandMenuOptionFactory = new TextImageButtonFactory()
			{
				FixedWidth = sideBarButtonWidth,
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,

				hoverFillColor = new RGBA_Bytes(255, 255, 255, 50),
				pressedFillColor = new RGBA_Bytes(255, 255, 255, 50),
				disabledFillColor = new RGBA_Bytes(255, 255, 255, 50)
			};

			#endregion

			#region ImageConverter
			imageConverterButtonFactory = new TextImageButtonFactory()
			{
				FixedWidth = 185,
				FixedHeight = 30,

				normalFillColor = RGBA_Bytes.White,
				normalTextColor = RGBA_Bytes.Black,
				hoverTextColor = RGBA_Bytes.Black,

				hoverFillColor = new RGBA_Bytes(255, 255, 255, 200),
				borderWidth = 1,

				normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200)
			};

			imageConverterExpandMenuOptionFactory = new TextImageButtonFactory()
			{
				FixedWidth = 200,
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,

				hoverFillColor = new RGBA_Bytes(255, 255, 255, 50),
				pressedFillColor = new RGBA_Bytes(255, 255, 255, 50),
				disabledFillColor = new RGBA_Bytes(255, 255, 255, 50)
			};
			#endregion

			this.LinkButtonFactory = new LinkButtonFactory()
			{
				fontSize = fontSize11,
				textColor = ActiveTheme.Instance.PrimaryTextColor
			};

			this.HelpLinkFactory = new LinkButtonFactory()
			{
				fontSize = fontSize10,
				textColor = ActiveTheme.Instance.SecondaryAccentColor
			};
		}

		internal TabControl CreateTabControl()
		{
			var tabControl = new TabControl(separator: new HorizontalLine(alpha: 50));
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
				VAnchor = VAnchor.ParentCenter,
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
				HAnchor = HAnchor.ParentLeft
			};
			wordOptionContainer.AddChild(spacingText);

			SolidSlider namedSlider = new SolidSlider(new Vector2(), scrollBarWidth, 0, 1)
			{
				TotalWidthInPixels = DefaultScrollBarWidth,
				Minimum = min,
				Maximum = max,
				Margin = new BorderDouble(3, 5, 3, 3),
				HAnchor = HAnchor.ParentCenter,
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