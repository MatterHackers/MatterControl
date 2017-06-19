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
		private static ImageBuffer restoreNormal;
		private static ImageBuffer restoreHover;
		private static ImageBuffer restorePressed;

		private readonly int fizedHeightA = (int)(25 * GuiWidget.DeviceScale + .5);
		private readonly int fontSizeA = 11;

		private readonly double fizedHeightB = 52 * GuiWidget.DeviceScale;
		private readonly int fontSizeB = 14;

		private int shortButtonHeight = 25;
		private int sideBarButtonWidth;

		public TextImageButtonFactory textImageButtonFactory;
		private TextImageButtonFactory checkboxButtonFactory;
		public TextImageButtonFactory ExpandMenuOptionFactory;
		public TextImageButtonFactory WhiteButtonFactory;

		private readonly int borderWidth = 1;

		public TextImageButtonFactory ImageButtonFactory { get; private set; }
		public TextImageButtonFactory ActionRowButtonFactory { get; private set; }
		public TextImageButtonFactory PrinterConnectButtonFactory { get; private set; }
		public TextImageButtonFactory BreadCrumbButtonFactory { get; internal set; }
		public TextImageButtonFactory BreadCrumbButtonFactorySmallMargins { get; internal set; }
		public TextImageButtonFactory MenuButtonFactory { get; internal set; }

		public TextImageButtonFactory imageConverterExpandMenuOptionFactory;
		public TextImageButtonFactory imageConverterButtonFactory;

		public RGBA_Bytes TabBodyBackground => new RGBA_Bytes(ActiveTheme.Instance.TertiaryBackgroundColor, 175);

		public TextImageButtonFactory ViewControlsButtonFactory { get; internal set; }
		public RGBA_Bytes SplitterBackground { get; internal set; } = new RGBA_Bytes(0, 0, 0, 60);
		public int SplitterWidth => (int)(7 * (GuiWidget.DeviceScale <= 1 ? GuiWidget.DeviceScale : GuiWidget.DeviceScale * 1.4));

		public RGBA_Bytes SlightShade { get; } = new RGBA_Bytes(0, 0, 0, 40);

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
			this.ImageButtonFactory = new TextImageButtonFactory()
			{
				normalFillColor = RGBA_Bytes.Transparent,
				normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				normalTextColor = theme.SecondaryTextColor,
				pressedTextColor = theme.PrimaryTextColor,
				hoverTextColor = theme.PrimaryTextColor,
				hoverBorderColor = new RGBA_Bytes(theme.PrimaryTextColor, 200),
				disabledFillColor = RGBA_Bytes.Transparent,
				disabledBorderColor = new RGBA_Bytes(theme.PrimaryTextColor, 100),
				disabledTextColor = new RGBA_Bytes(theme.PrimaryTextColor, 100),
				FixedHeight = fizedHeightA,
				fontSize = fontSizeA,
				borderWidth = borderWidth
			};

			this.ActionRowButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = RGBA_Bytes.White,
				disabledTextColor = RGBA_Bytes.LightGray,
				hoverTextColor = RGBA_Bytes.White,
				pressedTextColor = RGBA_Bytes.White,
				AllowThemeToAdjustImage = false,
				borderWidth = borderWidth,
				FixedHeight = fizedHeightB,
				fontSize = fontSizeB,
				normalBorderColor = new RGBA_Bytes(255, 255, 255, 100),
				hoverBorderColor = new RGBA_Bytes(255, 255, 255, 100)
			};

			this.PrinterConnectButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = theme.PrimaryTextColor,
				normalBorderColor = (theme.IsDarkTheme) ? new RGBA_Bytes(77, 77, 77) : new RGBA_Bytes(190, 190, 190),
				hoverTextColor = theme.PrimaryTextColor,
				pressedTextColor = theme.PrimaryTextColor,
				disabledTextColor = theme.TabLabelUnselected,
				disabledFillColor = theme.PrimaryBackgroundColor,
				disabledBorderColor = theme.SecondaryBackgroundColor,
				hoverFillColor = theme.PrimaryBackgroundColor,
				hoverBorderColor = new RGBA_Bytes(128, 128, 128),
				invertImageLocation = false,
				borderWidth = 1
			};

			this.BreadCrumbButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = theme.PrimaryTextColor,
				hoverTextColor = theme.PrimaryTextColor,
				pressedTextColor = theme.PrimaryTextColor,
				disabledTextColor = theme.TertiaryBackgroundColor,
				Margin = new BorderDouble(16, 0),
				borderWidth = 0,
				FixedHeight = 32,
			};

			this.BreadCrumbButtonFactorySmallMargins = new TextImageButtonFactory()
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
				Margin = new BorderDouble(8, 0),
				borderWidth = 0,
				FixedHeight = 32,
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

			textImageButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.TabLabelUnselected,
				disabledFillColor = new RGBA_Bytes()
			};

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

			checkboxButtonFactory = new TextImageButtonFactory()
			{
				fontSize = 11,
				FixedWidth = sideBarButtonWidth,
				borderWidth = 3,

				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				normalBorderColor = new RGBA_Bytes(0, 0, 0, 0),
				normalFillColor = ActiveTheme.Instance.PrimaryBackgroundColor,

				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverBorderColor = new RGBA_Bytes(0, 0, 0, 50),
				hoverFillColor = new RGBA_Bytes(0, 0, 0, 50),

				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedBorderColor = new RGBA_Bytes(0, 0, 0, 50),

				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor
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
		}

	internal TabControl CreateTabControl()
		{
			var tabControl = new TabControl(separator: new HorizontalLine(alpha: 50));
			tabControl.TabBar.BorderColor = RGBA_Bytes.Transparent; // theme.SecondaryTextColor;
			tabControl.TabBar.Margin = 0;
			tabControl.TabBar.Padding = 0;
			tabControl.TextPointSize = 14;

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
	}
}