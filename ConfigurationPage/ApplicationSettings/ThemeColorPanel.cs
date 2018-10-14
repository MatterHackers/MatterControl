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
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class ThemeColorPanel : FlowLayoutWidget
	{
		private Color lastColor;
		private AccentColorsWidget colorSelector;

		private IColorTheme _themeProvider;
		private GuiWidget previewButtonPanel;

		public ThemeColorPanel(ThemeConfig activeTheme)
			: base (FlowDirection.TopToBottom)
		{
			string currentProviderName = UserSettings.Instance.get(UserSettingsKey.ThemeName) ?? "";

			if (AppContext.ThemeProviders.TryGetValue(currentProviderName, out IColorTheme currentProvider))
			{
				_themeProvider = currentProvider;
			}
			else
			{
				_themeProvider = AppContext.ThemeProviders.Values.First();
			}

			this.SelectionColor = activeTheme.GetBorderColor(80);

			// Add color selector
			this.AddChild(colorSelector = new AccentColorsWidget(this)
			{
				Margin = new BorderDouble(activeTheme.DefaultContainerPadding, 0)
			});

			this.AddChild(previewButtonPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				BackgroundColor = this.SelectionColor,
				Padding = new BorderDouble(left: colorSelector.ColorButtons.First().Border.Left)
			});

			this.CreateThemeModeButtons();
		}

		public ImageBuffer CheckMark { get; } = AggContext.StaticData.LoadIcon("426.png", 16, 16, invertImage: true);

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

			var accentColor = theme.Colors.PrimaryAccentColor;

			if (!_themeProvider.Colors.Contains(accentColor))
			{
				accentColor = _themeProvider.DefaultColor;
			}

			var activeMode = UserSettings.Instance.get(UserSettingsKey.ThemeMode);

			foreach (var mode in _themeProvider.Modes)
			{
				var themeset = _themeProvider.GetTheme(mode, accentColor);

				previewButtonPanel.AddChild(new ThemePreviewButton(themeset.Theme, this)
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Absolute,
					Width = 80,
					Height = 65,
					Mode = mode,
					Margin = new BorderDouble(theme.DefaultContainerPadding, theme.DefaultContainerPadding, 0, theme.DefaultContainerPadding),
					Border = 1,
					IsActive = mode == activeMode,
					BorderColor = theme.GetBorderColor(20),
				});
			}
		}

		public void PreviewTheme(Color sourceAccentColor)
		{
			foreach (var previewButton in previewButtonPanel.Children<ThemePreviewButton>())
			{
				previewButton.PreviewThemeColor(sourceAccentColor);
			}
		}

		public void SetThemeColor(Color accentColor, string mode = null)
		{
			lastColor = accentColor;

			foreach (var colorButton in colorSelector.ColorButtons)
			{
				colorButton.BorderColor = (colorButton.SourceColor == accentColor) ? Color.White : Color.Transparent;
			}

			if (mode == null)
			{
				mode = this.ThemeProvider.DefaultMode;

				var lastMode = UserSettings.Instance.get(UserSettingsKey.ThemeMode);
				if (this.ThemeProvider.Modes.Contains(lastMode))
				{
					mode = lastMode;
				}
			}

			Console.WriteLine("Getting/setting theme for " + accentColor.Html);

			AppContext.SetTheme(this.ThemeProvider.GetTheme(mode, accentColor));
			previewButtonPanel.BackgroundColor = this.SelectionColor;
		}

		public class AccentColorsWidget : FlowLayoutWidget
		{
			private int containerHeight = (int)(20 * GuiWidget.DeviceScale);
			private ThemeColorPanel themeColorPanel;

			public AccentColorsWidget(ThemeColorPanel themeColorPanel)
			{
				this.Padding = new BorderDouble(2, 0);
				this.themeColorPanel = themeColorPanel;

				this.RebuildColorButtons();
			}

			private List<ColorButton> colorButtons = new List<ColorButton>();

			public IEnumerable<ColorButton> ColorButtons => colorButtons;

			public void RebuildColorButtons()
			{
				this.CloseAllChildren();

				colorButtons.Clear();

				bool firstItem = true;

				foreach (var color in themeColorPanel.ThemeProvider.Colors)
				{
					var colorButton = CreateThemeButton(color);
					colorButton.Width = containerHeight;
					colorButton.BorderColor = (color == AppContext.Theme.Colors.SourceColor) ? themeColorPanel.SelectionColor : Color.Transparent;

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
					Border = 5,
				};
				colorButton.Click += (s, e) =>
				{
					themeColorPanel.SetThemeColor(colorButton.BackgroundColor);
				};

				colorButton.MouseEnterBounds += (s, e) =>
				{
					foreach(var button in this.ColorButtons)
					{
						button.BorderColor = (button == colorButton) ? themeColorPanel.SelectionColor : Color.Transparent;
					}

					themeColorPanel.PreviewTheme(colorButton.BackgroundColor);
				};

				return colorButton;
			}
		}
	}
}