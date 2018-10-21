/*
Copyright (c) 2018, Kevin Pope, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class ThemeColorPanel : FlowLayoutWidget
	{
		private Color lastColor;
		private AccentColorsWidget colorSelector;

		private IColorTheme _themeProvider;
		private GuiWidget previewButtonPanel;

		public ThemeColorPanel(ThemeConfig activeTheme, AccentColorsWidget colorSelector)
			: base (FlowDirection.TopToBottom)
		{
			this.colorSelector = colorSelector;
			string currentProviderName = UserSettings.Instance.get(UserSettingsKey.ThemeName) ?? "";

			if (AppContext.ThemeProviders.TryGetValue(currentProviderName, out IColorTheme currentProvider))
			{
				_themeProvider = currentProvider;
			}
			else
			{
				_themeProvider = AppContext.ThemeProviders.Values.First();
			}

			accentPanelColor = activeTheme.ResolveColor(activeTheme.SectionBackgroundColor, activeTheme.SlightShade);

			this.SelectionColor = activeTheme.MinimalShade;

			this.AddChild(previewButtonPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				//BackgroundColor = activeTheme.MinimalShade
			});

			this.CreateThemeModeButtons();
		}

		public ImageBuffer CheckMark { get; } = AggContext.StaticData.LoadIcon("426.png", 16, 16, invertImage: true);

		private Color accentPanelColor;

		public Color SelectionColor { get; private set; }

		public IColorTheme ThemeProvider
		{
			get => _themeProvider;
			set
			{
				_themeProvider = value;

				var previewColor = _themeProvider.Colors.First();

				colorSelector.RebuildColorButtons();

				this.CreateThemeModeButtons();

				this.PreviewTheme(previewColor);
			}
		}
		private void CreateThemeModeButtons()
		{
			previewButtonPanel.CloseAllChildren();

			var theme = AppContext.Theme;

			var accentColor = theme.PrimaryAccentColor;

			if (!_themeProvider.Colors.Contains(accentColor))
			{
				accentColor = _themeProvider.DefaultColor;
			}

			int providerIndex = 0;

			foreach (var provider in AppContext.ThemeProviders.Values)
			{
				if (providerIndex > 0)
				{
					previewButtonPanel.AddChild(new VerticalLine()
					{
						BackgroundColor = theme.MinimalShade,
						Margin = new BorderDouble(0, theme.DefaultContainerPadding),
					});
				}

				foreach (var mode in provider.Modes)
				{
					var themeset = provider.GetTheme(mode, accentColor);

					var previewContainer = new GuiWidget()
					{
						HAnchor = HAnchor.Fit | HAnchor.Left,
						VAnchor = VAnchor.Fit | VAnchor.Center,
					};
					previewButtonPanel.AddChild(previewContainer);

					if (themeset.ThemeName == AppContext.ThemeSet.ThemeName)
					{
						previewContainer.BackgroundColor = theme.MinimalShade;
					}

					previewContainer.AddChild(new ThemePreviewButton(themeset, this)
					{
						HAnchor = HAnchor.Absolute,
						VAnchor = VAnchor.Absolute,
						Width = 80,
						Height = 65,
						Mode = mode,
						Border = 1,
						IsActive = AppContext.ThemeSet.ThemeName == themeset.ThemeName,
						BorderColor = theme.BorderColor20,
						Margin = theme.DefaultContainerPadding
					});

					if (AppContext.ThemeSet.ThemeName == themeset.ThemeName)
					{
						var imageBuffer = new ImageBuffer(20, 20);
						var graphics = imageBuffer.NewGraphics2D();

						var arrowHeight = 8;

						var upArrow = new VertexStorage();
						upArrow.MoveTo(-arrowHeight, -arrowHeight);
						upArrow.LineTo(arrowHeight, -arrowHeight);
						upArrow.LineTo(0, 0);

						var dropArrowBounds = new RectangleDouble(0, 0, 20, arrowHeight +2);

						var center = dropArrowBounds.Center;
						center.Y += 3;

						graphics.Render(upArrow, center, accentPanelColor);

						imageBuffer.SetPreMultiply();

						previewContainer.AddChild(new ImageWidget(imageBuffer, false)
						{
							HAnchor = HAnchor.Center,
							VAnchor = VAnchor.Bottom,
						});
					}
				}

				providerIndex++;
			}
		}

		public void PreviewTheme(Color sourceAccentColor)
		{
			var previewButton = previewButtonPanel.Descendants<ThemePreviewButton>().FirstOrDefault(t => t.ThemeSet.ThemeName == AppContext.ThemeSet.ThemeName);
			if (previewButton != null)
			{
				previewButton.PreviewThemeColor(sourceAccentColor);
			}
		}

		public void SetThemeColor(ThemeSet themeSet, Color accentColor, string mode = null)
		{
			lastColor = accentColor;

			if (colorSelector != null)
			{
				foreach (var colorButton in colorSelector.ColorButtons)
				{
					colorButton.BorderColor = (colorButton.SourceColor == accentColor) ? Color.White : Color.Transparent;
				}
			}

			themeSet.Theme.PrimaryAccentColor = accentColor;
			themeSet.Theme.AccentMimimalOverlay = accentColor.WithAlpha(90);

			AppContext.SetTheme(themeSet);

			previewButtonPanel.BackgroundColor = this.SelectionColor;
		}

		public class AccentColorsWidget : FlowLayoutWidget
		{
			private int containerHeight;
			private int buttonSpacing;
			private List<ColorButton> colorButtons = new List<ColorButton>();
			private ThemeSet themeSet;

			public AccentColorsWidget(ThemeSet themeSet, int buttonHeight = 18, int buttonSpacing = 3)
			{
				this.themeSet = themeSet;
				this.Margin = new BorderDouble(left: buttonSpacing);
				this.buttonSpacing = buttonSpacing;

				containerHeight = (int)(buttonHeight * GuiWidget.DeviceScale);

				this.RebuildColorButtons();
			}

			public IEnumerable<ColorButton> ColorButtons => colorButtons;

			public ThemeColorPanel ThemeColorPanel { get; set; }

			public void RebuildColorButtons()
			{
				this.CloseAllChildren();

				colorButtons.Clear();

				bool firstItem = true;

				foreach (var color in themeSet.AccentColors)
				{
					var colorButton = this.CreateThemeButton(color);
					colorButton.Width = containerHeight;

					colorButtons.Add(colorButton);

					if (firstItem)
					{
						firstItem = false;
						colorButton.Margin = colorButton.Margin.Clone(left: 0);
					}

					this.AddChild(colorButton);
				}
			}

			public ColorButton CreateThemeButton(Color color)
			{
				var colorButton = new ColorButton(color)
				{
					Cursor = Cursors.Hand,
					Width = containerHeight,
					Height = containerHeight,
					Margin = buttonSpacing
				};
				colorButton.Click += (s, e) =>
				{
					AppContext.SetThemeAccentColor(colorButton.BackgroundColor);
				};

				colorButton.MouseEnterBounds += (s, e) =>
				{
					this.ThemeColorPanel?.PreviewTheme(colorButton.BackgroundColor);
				};

				return colorButton;
			}
		}
	}
}