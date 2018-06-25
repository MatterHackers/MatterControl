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
	using MatterHackers.Localizations;
	using MatterHackers.MatterControl.PartPreviewWindow;
	using MatterHackers.MatterControl.PrinterCommunication;
	using MatterHackers.VectorMath;

	public class ThemeConfig
	{
		public static ImageBuffer RestoreNormal { get; private set; }
		public static ImageBuffer RestoreHover { get; private set; }
		private static ImageBuffer restorePressed;

		public int FontSize7 { get; } = 7;
		public int FontSize8 { get; } = 8;
		public int FontSize9 { get; } = 9;
		public int FontSize10 { get; } = 10;
		public int FontSize11 { get; } = 11;
		public int FontSize12 { get; } = 12;
		public int FontSize14 { get; } = 14;

		private readonly int defaultScrollBarWidth = 120;
		private readonly int sideBarButtonWidth = 138;

		public int DefaultFontSize { get; } = 11;
		public int DefaultContainerPadding { get; } = 10;
		public int H1PointSize { get; set; } = 11;

		public double ButtonHeight => 32 * GuiWidget.DeviceScale;
		public double MicroButtonHeight => 20 * GuiWidget.DeviceScale;
		public double MicroButtonWidth => 30 * GuiWidget.DeviceScale;
		public double TabButtonHeight => 30 * GuiWidget.DeviceScale;
		public double MenuGutterWidth => 35 * GuiWidget.DeviceScale;

		/// <summary>
		/// Indicates if icons should be inverted due to black source images on a dark theme
		/// </summary>
		public bool InvertIcons => this.Colors.IsDarkTheme;

		internal void ApplyPrimaryActionStyle(GuiWidget guiWidget)
		{
			guiWidget.BackgroundColor = this.AccentMimimalOverlay;

			Color hoverColor = new Color(this.AccentMimimalOverlay, 90);

			switch (guiWidget)
			{
				case PopupMenuButton menuButton:
					menuButton.HoverColor = hoverColor;
					break;
				case SimpleFlowButton flowButton:
					flowButton.HoverColor = hoverColor;
					break;
				case SimpleButton button:
					button.HoverColor = hoverColor;
					break;
			}
		}

		internal void RemovePrimaryActionStyle(GuiWidget guiWidget)
		{
			guiWidget.BackgroundColor = Color.Transparent;

			switch (guiWidget)
			{
				case PopupMenuButton menuButton:
					menuButton.HoverColor = Color.Transparent;
					break;
				case SimpleFlowButton flowButton:
					flowButton.HoverColor = Color.Transparent;
					break;
				case SimpleButton button:
					button.HoverColor = Color.Transparent;
					break;
			}
		}

		public BorderDouble TextButtonPadding { get; } = new BorderDouble(14, 0);

		public BorderDouble ButtonSpacing { get; }

		public BorderDouble ToolbarPadding { get; set; } = 3;

		public BorderDouble TabbarPadding { get; set; } = new BorderDouble(3, 1);

		public LinkButtonFactory LinkButtonFactory { get; private set; }

		public TextImageButtonFactory WhiteButtonFactory { get; private set; }
		public TextImageButtonFactory ButtonFactory { get; private set; }

		/// <summary>
		/// The height or width of a given vertical or horizontal splitter bar
		/// </summary>
		public int SplitterWidth
		{
			get
			{
				double splitterSize = 6 * GuiWidget.DeviceScale;

				if (GuiWidget.TouchScreenMode)
				{
					splitterSize *= 1.4;
				}

				return (int)splitterSize;
			}
		}

		public IThemeColors Colors { get; set; }
		public PresetColors PresetColors { get; set; } = new PresetColors();

		public Color SlightShade { get; } = new Color(0, 0, 0, 40);
		public Color MinimalShade { get; } = new Color(0, 0, 0, 15);
		public Color Shade { get; } = new Color(0, 0, 0, 120);
		public Color DarkShade { get; } = new Color(0, 0, 0, 190);

		public Color ActiveTabColor { get; set; }
		public Color TabBarBackground { get; set; }
		public Color InactiveTabColor { get; set; }
		public Color InteractionLayerOverlayColor { get; private set; }

		public TextWidget CreateHeading(string text)
		{
			return new TextWidget(text, pointSize: this.H1PointSize, textColor: this.Colors.PrimaryTextColor, bold: true)
			{
				Margin = new BorderDouble(0, 5)
			};
		}

		public Color SplitterBackground { get; private set; } = new Color(0, 0, 0, 60);
		public Color TabBodyBackground { get; private set; }
		public Color ToolbarButtonBackground { get; set; } = Color.Transparent;
		public Color ToolbarButtonHover => this.SlightShade;
		public Color ToolbarButtonDown => this.MinimalShade;

		public Color ThumbnailBackground { get; set; }
		public Color AccentMimimalOverlay { get; set; }
		public BorderDouble SeparatorMargin { get; }

		public GuiWidget CreateSearchButton()
		{
			return new IconButton(AggContext.StaticData.LoadIcon("icon_search_24x24.png", 16, 16, this.InvertIcons), this)
			{
				ToolTipText = "Search".Localize(),
			};
		}

		public ThemeConfig()
		{
			this.ButtonSpacing = new BorderDouble(right: 3);
			this.SeparatorMargin = (this.ButtonSpacing * 2).Clone(left: this.ButtonSpacing.Right);
		}

		public void RebuildTheme(IThemeColors colors)
		{
			int size = (int)(16 * GuiWidget.DeviceScale);

			if (AggContext.OperatingSystem == OSType.Android)
			{
				RestoreNormal = ColorCircle(size, new Color(200, 0, 0));
			}
			else
			{
				RestoreNormal = ColorCircle(size, Color.Transparent);
			}

			RestoreHover = ColorCircle(size, new Color("#DB4437"));
			restorePressed = ColorCircle(size, new Color(255, 0, 0));

			this.Colors = colors;

			DefaultThumbView.ThumbColor = new Color(colors.PrimaryTextColor, 30);

			var commonOptions = new ButtonFactoryOptions();
			commonOptions.NormalTextColor = colors.PrimaryTextColor;
			commonOptions.HoverTextColor = colors.PrimaryTextColor;
			commonOptions.PressedTextColor = colors.PrimaryTextColor;
			commonOptions.DisabledTextColor = colors.TertiaryBackgroundColor;
			commonOptions.Margin = this.TextButtonPadding;
			commonOptions.FontSize = this.DefaultFontSize;
			commonOptions.ImageSpacing = 8;
			commonOptions.BorderWidth = 0;
			commonOptions.FixedHeight = 32;

			this.TabBodyBackground = this.ResolveColor(
				colors.TertiaryBackgroundColor,
				new Color(
					Color.White,
					(colors.IsDarkTheme) ? 3 : 25));

			this.ActiveTabColor = this.TabBodyBackground;
			this.TabBarBackground = this.ActiveTabColor.AdjustLightness(0.85).ToColor();
			this.ThumbnailBackground = this.MinimalShade;
			this.AccentMimimalOverlay = new Color(this.Colors.PrimaryAccentColor, 50);

			// Active tab color with slight transparency
			this.InteractionLayerOverlayColor = new Color(this.ActiveTabColor, 240);

			float alpha0to1 = (colors.IsDarkTheme ? 20 : 60) / 255.0f;

			this.InactiveTabColor = ResolveColor(colors.PrimaryBackgroundColor, new Color(Color.White, this.SlightShade.alpha));

			this.SplitterBackground = this.ActiveTabColor.AdjustLightness(0.87).ToColor();

			this.ButtonFactory = new TextImageButtonFactory(commonOptions);

			var commonGray = new ButtonFactoryOptions(commonOptions)
			{
				NormalTextColor = Color.Black,
				NormalFillColor = Color.LightGray,
				HoverTextColor = Color.Black,
				PressedTextColor = Color.Black,
				PressedFillColor = Color.LightGray,
			};

#region PartPreviewWidget

			WhiteButtonFactory = new TextImageButtonFactory(new ButtonFactoryOptions(commonOptions)
			{
				FixedWidth = sideBarButtonWidth,
				FixedHeight = TabButtonHeight,

				NormalTextColor = Color.Black,
				NormalFillColor = Color.White,
				NormalBorderColor = new Color(colors.PrimaryTextColor, 200),

				HoverTextColor = Color.Black,
				HoverFillColor = new Color(255, 255, 255, 200),
				HoverBorderColor = new Color(colors.PrimaryTextColor, 200),

				BorderWidth = 1,
			});
#endregion

			this.LinkButtonFactory = new LinkButtonFactory()
			{
				fontSize = FontSize11,
				textColor = colors.PrimaryTextColor
			};
		}

		public JogControls.MoveButton CreateMoveButton(PrinterConfig printer, string label, PrinterConnection.Axis axis, double movementFeedRate, bool levelingButtons = false)
		{
			return new JogControls.MoveButton(label, printer, axis, movementFeedRate, this)
			{
				BackgroundColor = this.MinimalShade,
				Border = 1,
				BorderColor = this.GetBorderColor(40),
				VAnchor = VAnchor.Absolute,
				HAnchor = HAnchor.Absolute,
				Margin = 0,
				Padding = 0,
				Height = (levelingButtons ? 45 : 40) * GuiWidget.DeviceScale,
				Width = (levelingButtons ? 90 : 40) * GuiWidget.DeviceScale,
			};
		}

		public JogControls.ExtrudeButton CreateExtrudeButton(PrinterConfig printer, string label, double movementFeedRate, int extruderNumber, bool levelingButtons = false)
		{
			return new JogControls.ExtrudeButton(printer, label, movementFeedRate, extruderNumber, this)
			{
				BackgroundColor = this.MinimalShade,
				Border = 1,
				BorderColor = this.GetBorderColor(40),
				VAnchor = VAnchor.Absolute,
				HAnchor = HAnchor.Absolute,
				Margin = 0,
				Padding = 0,
				Height = (levelingButtons ? 45 : 40) * GuiWidget.DeviceScale,
				Width = (levelingButtons ? 90 : 40) * GuiWidget.DeviceScale,
			};
		}

		public RadioTextButton CreateMicroRadioButton(string text, IList<GuiWidget> siblingRadioButtonList = null)
		{
			var radioButton = new RadioTextButton(text, this, this.FontSize8)
			{
				SiblingRadioButtonList = siblingRadioButtonList,
				Padding = new BorderDouble(5, 0),
				SelectedBackgroundColor = this.SlightShade,
				UnselectedBackgroundColor = this.SlightShade,
				HoverColor = this.AccentMimimalOverlay,
				Margin = new BorderDouble(right: 1),
				HAnchor = HAnchor.Absolute,
				Height = this.MicroButtonHeight,
				Width = this.MicroButtonWidth
			};

			// Add to sibling list if supplied
			siblingRadioButtonList?.Add(radioButton);

			return radioButton;
		}

		public TextButton CreateDialogButton(string text)
		{
#if !__ANDROID__
			return new TextButton(text, this)
			{
				BackgroundColor = this.MinimalShade
			};
#else
			var button = new TextButton(text, this, this.FontSize14)
			{
				BackgroundColor = this.MinimalShade,
				// Enlarge button height and margin on Android
				Height = 34 * GuiWidget.DeviceScale,
			};
			button.Padding = button.Padding * 1.2;

			return button;
#endif
		}

		public Color GetBorderColor(int alpha)
		{
			return new Color(this.Colors.SecondaryTextColor, alpha);
		}

		// Compute a fixed color from a source and a target alpha
		public Color ResolveColor(Color background, Color overlay)
		{
			return new BlenderBGRA().Blend(background, overlay);
		}

		public FlowLayoutWidget CreateMenuItems(PopupMenu popupMenu, IEnumerable<NamedAction> menuActions)
		{
			// Create menu items in the DropList for each element in this.menuActions
			popupMenu.CloseAllChildren();
			foreach (var menuAction in menuActions)
			{
				if (menuAction.Title == "----")
				{
					popupMenu.CreateHorizontalLine();
				}
				else
				{
					PopupMenu.MenuItem menuItem;

					if (menuAction is NamedBoolAction boolAction)
					{
						menuItem = popupMenu.CreateBoolMenuItem(menuAction.Title, boolAction.GetIsActive, boolAction.SetIsActive);
					}
					else
					{
						menuItem = popupMenu.CreateMenuItem(menuAction.Title, menuAction.Icon, menuAction.Shortcut);
					}

					menuItem.Name = $"{menuAction.Title} Menu Item";

					menuItem.Enabled = menuAction.Action != null
						&& menuAction.IsEnabled?.Invoke() != false;

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
					new ImageWidget(RestoreNormal),
					new ImageWidget(RestoreHover),
					new ImageWidget(restorePressed),
					new ImageWidget(RestoreNormal)))
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(0, 0, 5, 0)
			};
		}

		public SolidSlider CreateSolidSlider(GuiWidget wordOptionContainer, string header, double min = 0, double max = .5)
		{
			double scrollBarWidth = 10;

			wordOptionContainer.AddChild(new TextWidget(header, textColor: this.Colors.PrimaryTextColor)
			{
				Margin = new BorderDouble(10, 3, 3, 5),
				HAnchor = HAnchor.Left
			});

			var namedSlider = new SolidSlider(new Vector2(), scrollBarWidth, 0, 1)
			{
				TotalWidthInPixels = defaultScrollBarWidth,
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

		public SectionWidget ApplyBoxStyle(SectionWidget sectionWidget)
		{
			return ApplyBoxStyle(
				sectionWidget,
				this.MinimalShade,
				margin: new BorderDouble(this.DefaultContainerPadding, 0, this.DefaultContainerPadding, this.DefaultContainerPadding));
		}

		public SectionWidget ApplyBoxStyle(SectionWidget sectionWidget, BorderDouble margin)
		{
			return ApplyBoxStyle(sectionWidget, this.MinimalShade, margin);
		}

		public SectionWidget ApplyBoxStyle(SectionWidget sectionWidget, Color backgroundColor, BorderDouble margin)
		{
			// Enforce panel padding
			// sectionWidget.ContentPanel.Padding = new BorderDouble(10, 0, 10, 2);
			//sectionWidget.ContentPanel.Padding = 0;

			sectionWidget.BorderColor = Color.Transparent;
			sectionWidget.BorderRadius = 5;
			sectionWidget.Margin = margin;
			sectionWidget.BackgroundColor = backgroundColor;

			return sectionWidget;
		}
	}

	public class PresetColors
	{
		public Color MaterialPreset { get; set; } = Color.Orange;
		public Color QualityPreset { get; set; } = Color.Yellow;
		public Color UserOverride { get; set; } = new Color(68, 95, 220, 150);
	}
}