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

		public int FontSize7 { get; } = 7;
		public int FontSize9 { get; } = 9;
		public int FontSize10 { get; } = 10;
		public int FontSize11 { get; } = 11;
		public int FontSize12 { get; } = 12;
		public int FontSize14 { get; } = 14;

		public int DefaultFontSize { get; } = 12;

		internal int shortButtonHeight = 25;
		private int sideBarButtonWidth;

		public int H1PointSize { get; set; } = 13;

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

		public TextImageButtonFactory imageConverterButtonFactory;

		public Color TabBodyBackground => new Color(ActiveTheme.Instance.TertiaryBackgroundColor, 175);

		public TextImageButtonFactory ViewControlsButtonFactory { get; private set; }

		public Color SplitterBackground { get; private set; } = new Color(0, 0, 0, 60);

		public int SplitterWidth => (int)(6 * (GuiWidget.DeviceScale <= 1 ? GuiWidget.DeviceScale : GuiWidget.DeviceScale * 1.4));

		public Color SlightShade { get; } = new Color(0, 0, 0, 40);
		public Color MinimalShade { get; } = new Color(0, 0, 0, 15);
		public Color Shade { get; } = new Color(0, 0, 0, 120);
		public Color DarkShade { get; } = new Color(0, 0, 0, 190);

		public Color ActiveTabColor { get; set; }
		public Color InactiveTabColor { get; set; }

		public TextImageButtonFactory DisableableControlBase { get; private set; }
		public TextImageButtonFactory HomingButtons { get; private set; }
		public TextImageButtonFactory MicroButton { get; private set; }
		public TextImageButtonFactory MicroButtonMenu { get; private set; }

		public BorderDouble ButtonSpacing { get; set; } = new BorderDouble(3, 0, 0, 0);
		public TextImageButtonFactory NoMarginWhite { get; private set; }
		public BorderDouble ToolbarPadding { get; set; } = 3;
		public double ButtonHeight { get; internal set; } = 32;

		public int OverlayAlpha { get; set; } = 50;

		public Color InteractionLayerOverlayColor { get; private set; }

		private EventHandler unregisterEvents;

		static ThemeConfig()
		{
			// EnsureRestoreButtonImages
			int size = (int)(16 * GuiWidget.DeviceScale);

			if (AggContext.OperatingSystem == OSType.Android)
			{
				restoreNormal = ColorCircle(size, new Color(200, 0, 0));
			}
			else
			{
				restoreNormal = ColorCircle(size, Color.Transparent);
			}
			restoreHover = ColorCircle(size, new Color("#DB4437"));
			restorePressed = ColorCircle(size, new Color(255, 0, 0));
		}

		public ThemeConfig()
		{
			ActiveTheme.ThemeChanged.RegisterEvent((s, e) => RebuildTheme(), ref unregisterEvents);
			RebuildTheme();
		}

		public void RebuildTheme()
		{
			var theme = ActiveTheme.Instance;

			DefaultThumbView.ThumbColor = new Color(theme.PrimaryTextColor, 30);

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

			this.ActiveTabColor = ResolveColor(theme.PrimaryBackgroundColor, new Color(Color.Black, this.SlightShade.alpha));

			// Active tab color with slight transparency
			this.InteractionLayerOverlayColor = new Color(this.ActiveTabColor, 200);

			float alpha0to1 = (ActiveTheme.Instance.IsDarkTheme ? 20 : 60) / 255.0f;

			this.InactiveTabColor = ResolveColor(theme.PrimaryBackgroundColor, new Color(Color.White, this.SlightShade.alpha));

			this.SplitterBackground = this.ActiveTabColor;

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
				FontSize = this.FontSize14,
				FixedHeight = fizedHeightB,
				Margin = commonOptions.Margin * 1.2
#endif
			});

			this.RadioButtons = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				BorderWidth = 1,
				CheckedBorderColor = Color.White,
				AllowThemeToAdjustImage = false
			});

			var commonGray = new ButtonFactoryOptions(commonOptions)
			{
				NormalTextColor = Color.Black,
				NormalFillColor = Color.LightGray,
				HoverTextColor = Color.Black,
				PressedTextColor = Color.Black,
				PressedFillColor = Color.LightGray,
			};

			this.MenuButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonGray)
			{
				Margin = new BorderDouble(8, 0)
			});

			this.GrayButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				NormalTextColor = theme.PrimaryTextColor,
				NormalFillColor = Color.Gray
			});

			int viewControlsButtonHeight = (UserSettings.Instance.IsTouchScreen) ? 40 : 0;

			this.ViewControlsButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				DisabledTextColor = theme.PrimaryTextColor,
				FixedHeight = viewControlsButtonHeight,
				FixedWidth = viewControlsButtonHeight,
				AllowThemeToAdjustImage = false,
				CheckedBorderColor = Color.White
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
				CheckedBorderColor = Color.Black
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

				NormalTextColor = Color.Black,
				NormalFillColor = Color.White,
				NormalBorderColor = new Color(theme.PrimaryTextColor, 200),

				HoverTextColor = Color.Black,
				HoverFillColor = new Color(255, 255, 255, 200),
				HoverBorderColor = new Color(theme.PrimaryTextColor, 200),

				BorderWidth = 1,
			});

			ExpandMenuOptionFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				HoverTextColor = theme.PrimaryTextColor,
				HoverFillColor = new Color(255, 255, 255, 50),

				PressedTextColor = theme.PrimaryTextColor,
				PressedFillColor = new Color(255, 255, 255, 50),

				DisabledTextColor = theme.PrimaryTextColor,
				DisabledFillColor = new Color(255, 255, 255, 50),
				FixedWidth = sideBarButtonWidth,
			});

#endregion

#region ImageConverter
			imageConverterButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				FixedWidth = 185,
				FixedHeight = 30,

				NormalFillColor = Color.White,
				NormalTextColor = Color.Black,
				NormalBorderColor = new Color(theme.PrimaryTextColor, 200),

				HoverFillColor = new Color(255, 255, 255, 200),
				HoverTextColor = Color.Black,
				HoverBorderColor = new Color(theme.PrimaryTextColor, 200),

				BorderWidth = 1,
			});

			imageConverterExpandMenuOptionFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				FixedWidth = 200,

				NormalTextColor = theme.PrimaryTextColor,

				HoverTextColor = theme.PrimaryTextColor,
				HoverFillColor = new Color(255, 255, 255, 50),

				DisabledTextColor = theme.PrimaryTextColor,
				DisabledFillColor = new Color(255, 255, 255, 50),

				PressedTextColor = theme.PrimaryTextColor,
				PressedFillColor = new Color(255, 255, 255, 50),
			});

			// TODO: Need to remain based default ButtonFactionOptions constructor until reviewed for styling issues
			var disableableControlOptions = new ButtonFactoryOptions()
			{
				NormalFillColor = Color.White,
				NormalTextColor = Color.Black,
				HoverTextColor = theme.PrimaryTextColor,
				DisabledFillColor = Color.White,
				DisabledTextColor = Color.DarkGray,
				PressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				FixedHeight = 25 * GuiWidget.DeviceScale,
				FontSize = 11
			};

			this.DisableableControlBase = new TextImageButtonFactory(disableableControlOptions);
			this.HomingButtons = new TextImageButtonFactory(new ButtonFactoryOptions(disableableControlOptions)
			{
				BorderWidth = 1,
				NormalBorderColor = new Color(ActiveTheme.Instance.PrimaryTextColor, 200),
				HoverBorderColor = new Color(ActiveTheme.Instance.PrimaryTextColor, 200),
				NormalFillColor = new Color(180, 180, 180),
			});

#endregion

			this.LinkButtonFactory = new LinkButtonFactory()
			{
				fontSize = FontSize11,
				textColor = theme.PrimaryTextColor
			};

			this.HelpLinkFactory = new LinkButtonFactory()
			{
				fontSize = FontSize10,
				textColor = theme.SecondaryAccentColor
			};
		}

		// Compute a fixed color from a source and a target alpha
		public Color ResolveColor(Color background, Color overlay)
		{
			return new BlenderRGBA().Blend(background, overlay);
		}

		public FlowLayoutWidget CreatePopupMenu(IEnumerable<NamedAction> menuActions)
		{
			var popupMenu = new PopupMenu(this)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = Color.White
			};

			// Create menu items in the DropList for each element in this.menuActions
			foreach (var menuAction in menuActions)
			{
				if (menuAction.Title == "----")
				{
					popupMenu.CreateHorizontalLine();
				}
				else
				{
					var menuItem = popupMenu.CreateMenuItem(menuAction.Title, menuAction.Icon);
					menuItem.Name = $"{menuAction.Title} Menu Item";

					menuItem.Enabled = menuAction.Action != null;
					menuItem.ClearRemovedFlag();

					if (menuItem.Enabled)
					{
						menuItem.Click += (s, e) =>
						{
							menuAction.Action();
						};
					}
				}
			}

			return popupMenu;
		}


		internal TabControl CreateTabControl(int height = 1)
		{
			var tabControl = new TabControl(separator: new HorizontalLine(alpha: 50, height: height));
			tabControl.TabBar.BorderColor = Color.Transparent; // theme.SecondaryTextColor;
			tabControl.TabBar.Margin = 0;
			tabControl.TabBar.Padding = 0;
			tabControl.TextPointSize = FontSize12;

			return tabControl;
		}

		private static ImageBuffer ColorCircle(int size, Color color)
		{
			ImageBuffer imageBuffer = new ImageBuffer(size, size);
			Graphics2D normalGraphics = imageBuffer.NewGraphics2D();
			Vector2 center = new Vector2(size / 2.0, size / 2.0);

			Color barColor;
			if (color != Color.Transparent)
			{
				normalGraphics.Circle(center, size / 2.0, color);
				barColor = Color.White;
			}
			else
			{
				barColor = new Color("#999");
			}

			normalGraphics.Line(center + new Vector2(-size / 4.0, -size / 4.0), center + new Vector2(size / 4.0, size / 4.0), barColor, 2 * GuiWidget.DeviceScale);
			normalGraphics.Line(center + new Vector2(-size / 4.0, size / 4.0), center + new Vector2(size / 4.0, -size / 4.0), barColor, 2 * GuiWidget.DeviceScale);

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

			wordOptionContainer.AddChild(new TextWidget(header, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(10, 3, 3, 5),
				HAnchor = HAnchor.Left
			});

			var namedSlider = new SolidSlider(new Vector2(), scrollBarWidth, 0, 1)
			{
				TotalWidthInPixels = DefaultScrollBarWidth,
				Minimum = min,
				Maximum = max,
				Margin = new BorderDouble(12, 4),
				HAnchor = HAnchor.Stretch,
			};

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