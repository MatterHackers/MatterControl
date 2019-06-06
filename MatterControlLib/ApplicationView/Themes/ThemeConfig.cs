/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
		private ImageBuffer restoreNormal;
		private ImageBuffer restoreHover;
		private ImageBuffer restorePressed;

		public int FontSize7 { get; } = 7;
		public int FontSize8 { get; } = 8;
		public int FontSize9 { get; } = 9;
		public int FontSize10 { get; } = 10;
		public int FontSize11 { get; } = 11;
		public int FontSize12 { get; } = 12;
		public int FontSize14 { get; } = 14;

		public int DefaultFontSize { get; set; } = 11;
		public int DefaultContainerPadding { get; } = 10;
		public int H1PointSize { get; } = 11;

		public double ButtonHeight => 32 * GuiWidget.DeviceScale;
		public double TabButtonHeight => 30 * GuiWidget.DeviceScale;
		public double MenuGutterWidth => 35 * GuiWidget.DeviceScale;

		public double MicroButtonHeight => 20 * GuiWidget.DeviceScale;
		private double microButtonWidth => 30 * GuiWidget.DeviceScale;

		private readonly int defaultScrollBarWidth = 120;

		/// <summary>
		/// Indicates if icons should be inverted due to black source images on a dark theme
		/// </summary>
		public bool InvertIcons => this?.IsDarkTheme ?? false;

		internal void ApplyPrimaryActionStyle(GuiWidget guiWidget)
		{
			guiWidget.BackgroundColor = new Color(this.AccentMimimalOverlay, 50);

			Color hoverColor = this.AccentMimimalOverlay;

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

			// Buttons in toolbars should revert to ToolbarButtonHover when reset
			bool parentIsToolbar = guiWidget.Parent?.Parent is Toolbar;

			switch (guiWidget)
			{
				case SimpleFlowButton flowButton:
					flowButton.HoverColor = (parentIsToolbar) ? this.ToolbarButtonHover : Color.Transparent;
					break;
				case SimpleButton button:
					button.HoverColor = (parentIsToolbar) ? this.ToolbarButtonHover : Color.Transparent;
					break;
			}
		}

		public BorderDouble TextButtonPadding { get; } = new BorderDouble(14, 0);

		public BorderDouble ButtonSpacing { get; } = new BorderDouble(right: 3);

		public BorderDouble ToolbarPadding { get; } = 3;

		public BorderDouble TabbarPadding { get; } = new BorderDouble(3, 1);

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

		public PresetColors PresetColors { get; set; } = new PresetColors();

		public bool IsDarkTheme { get; set; }

		public Color SlightShade { get; set; }
		public Color MinimalShade { get; set; }
		public Color Shade { get; set; }
		public Color DarkShade { get; set; }

		public Color BackgroundColor { get; set; }
		public Color TextColor { get; set; } = Color.Black;

		public Color TabBarBackground { get; set; }
		public Color InactiveTabColor { get; set; }
		public Color InteractionLayerOverlayColor { get; set; }

		public TextWidget CreateHeading(string text)
		{
			return new TextWidget(text, pointSize: this.H1PointSize, textColor: this.TextColor, bold: true)
			{
				Margin = new BorderDouble(0, 5)
			};
		}

		public Color SplitterBackground { get; set; } = new Color(0, 0, 0, 60);
		public Color TabBodyBackground { get; set; }
		public Color ToolbarButtonBackground { get; set; } = Color.Transparent;
		public Color ToolbarButtonHover => this.SlightShade;
		public Color ToolbarButtonDown => this.MinimalShade;

		public Color ThumbnailBackground { get; set; }
		public Color AccentMimimalOverlay { get; set; }
		public BorderDouble SeparatorMargin { get; }

		public ImageBuffer GeneratingThumbnailIcon { get; private set; }

		public class StateColor
		{
			public Color BackgroundColor { get; set; }
			public Color ForegroundColor { get; set; }
			public Color BorderColor { get; set; }
			public Color TextColor { get; set; }
			public Color LightTextColor { get; set; }
		}

		public class ThreeStateColor
		{
			public StateColor Focused { get; set; } = new StateColor();
			public StateColor Hovered { get; set; } = new StateColor();
			public StateColor Inactive { get; set; } = new StateColor();
		}

		public class DropListStyle : ThreeStateColor
		{
			public StateColor Open { get; set; } = new StateColor();
		}

		public ThreeStateColor EditFieldColors { get; set; } = new ThreeStateColor();

		public Color LightTextColor { get; set; }

		public Color BorderColor { get; set; }
		public Color BorderColor40 { get; set; }
		public Color BorderColor20 { get; set; }

		internal void EnsureDefaults()
		{
			//if (this.BedColor == Color.Transparent)
			//{
			//	this.BedColor = this.ResolveColor(this.BackgroundColor, Color.Gray.WithAlpha(60));
			//}
		}

		public Color RowBorder { get; set; }

		public DropListStyle DropList { get; set; } = new DropListStyle();

		public Color DisabledColor { get; set; }
		public Color SplashAccentColor { get; set; }
		public Color BedBackgroundColor { get; set; }
		public Color PrimaryAccentColor { get; set; }
		public Color SectionBackgroundColor { get; set; }
		public Color PopupBorderColor { get; set; }

		public Color BedColor { get; set; }

		public Color UnderBedColor { get; set; }

		public Color PrinterBedTextColor { get; set; }

		public GridColors BedGridColors { get; set; } = new GridColors();

		public GuiWidget CreateSearchButton()
		{
			return new IconButton(AggContext.StaticData.LoadIcon("icon_search_24x24.png", 16, 16, this.InvertIcons), this)
			{
				ToolTipText = "Search".Localize(),
			};
		}

		public ThemeConfig()
		{
			this.SeparatorMargin = (this.ButtonSpacing * 2).Clone(left: this.ButtonSpacing.Right);
			this.RebuildTheme();
		}

		public void SetDefaults()
		{
			this.DisabledColor = new Color(this.LightTextColor, 50);
			this.SplashAccentColor = new Color(this.PrimaryAccentColor, 185).OverlayOn(Color.White).ToColor();
		}

		public void RebuildTheme()
		{
			int size = (int)(16 * GuiWidget.DeviceScale);

			// On Android, use red icon as no hover events, otherwise transparent and red on hover
			restoreNormal = ColorCircle(size, (AggContext.OperatingSystem == OSType.Android) ? new Color(200, 0, 0) : Color.Transparent);
			restoreHover = ColorCircle(size, new Color("#DB4437"));
			restorePressed = ColorCircle(size, new Color(255, 0, 0));

			this.GeneratingThumbnailIcon = AggContext.StaticData.LoadIcon("building_thumbnail_40x40.png", 40, 40, this.InvertIcons);

			DefaultThumbView.ThumbColor = new Color(this.TextColor, 30);
		}

		public JogControls.MoveButton CreateMoveButton(PrinterConfig printer, string label, PrinterConnection.Axis axis, double movementFeedRate, bool levelingButtons = false)
		{
			return new JogControls.MoveButton(label, printer, axis, movementFeedRate, this)
			{
				BackgroundColor = this.MinimalShade,
				Border = 1,
				BorderColor = this.BorderColor40,
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
				BorderColor = this.BorderColor40,
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
				Width = this.microButtonWidth
			};

			// Add to sibling list if supplied
			siblingRadioButtonList?.Add(radioButton);

			return radioButton;
		}

		public TextButton CreateLightDialogButton(string text)
		{
			return CreateDialogButton(text, new Color(Color.White, 15), new Color(Color.White, 25));
		}

		public TextButton CreateDialogButton(string text)
		{
			return CreateDialogButton(text, this.SlightShade, this.SlightShade.WithAlpha(75));
		}

		public TextButton CreateDialogButton(string text, Color backgroundColor, Color hoverColor)
		{
#if !__ANDROID__
			return new TextButton(text, this)
			{
				BackgroundColor = backgroundColor,
				HoverColor = hoverColor,
				MinimumSize = new Vector2(75, 0),
				Margin = this.ButtonSpacing
			};
#else
			var button = new TextButton(text, this, this.FontSize14)
			{
				BackgroundColor = backgroundColor,
				HoverColor = hoverColor,
				// Enlarge button height and margin on Android
				Height = 34 * GuiWidget.DeviceScale,
			};
			button.Padding = button.Padding * 1.2;

			return button;
#endif
		}

		public Color GetBorderColor(int alpha)
		{
			return new Color(this.BorderColor, alpha);
		}

		// Compute an opaque color from a source and a target with alpha
		public Color ResolveColor(Color background, Color overlay)
		{
			return ResolveColor2(background, overlay);
		}

		// Compute an opaque color from a source and a target with alpha
		public static Color ResolveColor2(Color background, Color overlay)
		{
			return new BlenderBGRA().Blend(background, overlay);
		}

		public FlowLayoutWidget CreateMenuItems(PopupMenu popupMenu, IEnumerable<NamedAction> menuActions, bool emptyMenu = true)
		{
			// Retain past behavior, where menu is cleared each call. More recent callers many pass in a newly populated menu and
			// not require the clear
			if (emptyMenu)
			{
				popupMenu.CloseAllChildren();
			}

			// Create menu items in the DropList for each element in this.menuActions
			foreach (var menuAction in menuActions)
			{
				if (menuAction is ActionSeparator)
				{
					popupMenu.CreateSeparator();
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

		public GuiWidget CreateSmallResetButton()
		{
			return new HoverImageWidget(restoreNormal, restoreHover)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(0, 0, 5, 0)
			};
		}

		public SolidSlider CreateSolidSlider(GuiWidget wordOptionContainer, string header, ThemeConfig theme, double min = 0, double max = .5)
		{
			double scrollBarWidth = 10;

			wordOptionContainer.AddChild(new TextWidget(header, textColor: this.TextColor)
			{
				Margin = new BorderDouble(10, 3, 3, 5),
				HAnchor = HAnchor.Left
			});

			var namedSlider = new SolidSlider(new Vector2(), scrollBarWidth, theme, 0, 1)
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

		public void ApplyBottomBorder(GuiWidget widget, bool shadedBorder = false)
		{
			widget.BorderColor = shadedBorder ? this.MinimalShade : this.BorderColor20;

			this.ApplyBorder(widget, new BorderDouble(bottom: 1), shadedBorder);
		}

		public void ApplyBorder(GuiWidget widget, BorderDouble border, bool shadedBorder = false)
		{
			widget.BorderColor = shadedBorder ? this.MinimalShade : this.BorderColor20;
			widget.Border = border;
		}

		public SectionWidget ApplyBoxStyle(SectionWidget sectionWidget)
		{
			return ApplyBoxStyle(
				sectionWidget,
				this.SectionBackgroundColor,
				margin: new BorderDouble(this.DefaultContainerPadding, 0, this.DefaultContainerPadding, this.DefaultContainerPadding));
		}

		public SolidSlider ApplySliderStyle(SolidSlider solidSlider)
		{
			solidSlider.View.TrackColor = this.SlightShade;
			solidSlider.View.TrackRadius = 4;

			return solidSlider;
		}

		public DoubleSolidSlider ApplySliderStyle(DoubleSolidSlider solidSlider)
		{
			solidSlider.View.TrackColor = this.SlightShade;
			solidSlider.View.TrackRadius = 4;

			return solidSlider;
		}

		// ApplySquareBoxStyle
		public SectionWidget ApplyBoxStyle(SectionWidget sectionWidget, BorderDouble margin)
		{
			sectionWidget.BackgroundColor = this.SectionBackgroundColor;
			sectionWidget.Margin = 0;
			sectionWidget.Border = new BorderDouble(bottom: 1);
			sectionWidget.BorderColor = this.RowBorder;

			return sectionWidget;
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

	public class GridColors
	{
		public Color Red { get; set; }
		public Color Green { get; set; }
		public Color Blue { get; set; }
		public Color Line { get; set; }
	}
}