using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class SettingsItem : FlowLayoutWidget
	{
		public class ToggleSwitchConfig
		{
			public bool Checked { get; set; }
			public Action<bool> ToggleAction { get; set; }
		}

		private static Color menuTextColor = Color.Black;

		public SettingsItem(string text, Color textColor, ToggleSwitchConfig toggleSwitchConfig = null, GuiWidget optionalControls = null, ImageBuffer iconImage = null, bool enforceGutter = true)
			: this(text, textColor, CreateToggleSwitch(toggleSwitchConfig, textColor), optionalControls, iconImage, enforceGutter)
		{
		}

		public SettingsItem(string text, ToggleSwitchConfig toggleSwitchConfig = null, GuiWidget optionalControls = null, ImageBuffer iconImage = null, bool enforceGutter = true)
			: this(text, CreateToggleSwitch(toggleSwitchConfig, menuTextColor), optionalControls, iconImage, enforceGutter)
		{
		}

		public SettingsItem(string text, GuiWidget settingsControls, GuiWidget optionalControls = null, ImageBuffer imageBuffer = null, bool enforceGutter = true)
			: this(text, menuTextColor, settingsControls, optionalControls, imageBuffer, enforceGutter)
		{
		}

		public SettingsItem (string text, Color textColor, GuiWidget settingsControls, GuiWidget optionalControls = null, ImageBuffer imageBuffer = null, bool enforceGutter = true)
			: base (FlowDirection.LeftToRight)
		{
			var theme = ApplicationController.Instance.Theme;
			this.SettingsControl = settingsControls;
			this.HAnchor = HAnchor.Stretch;
			this.MinimumSize = new Vector2(0, theme.ButtonHeight);

			if (imageBuffer != null)
			{
				this.AddChild(new ImageWidget(imageBuffer)
				{
					Margin = new BorderDouble(right: 6, left: 6),
					VAnchor = VAnchor.Center
				});
			}
			else if (enforceGutter)
			{
				// Add an icon place holder to get consistent label indenting on items lacking icons 
				this.AddChild(new GuiWidget()
				{
					Width = 24 + 12,
					Height = 24,
					Margin = new BorderDouble(0)
				});
			}

			this.AddChild(new TextWidget(text, textColor: textColor, pointSize: theme.DefaultFontSize)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
			});

			this.AddChild(new HorizontalSpacer());

			if (optionalControls != null)
			{
				this.AddChild(optionalControls);
			}

			if (settingsControls != null)
			{
				this.AddChild(settingsControls);
			}
		}

		public GuiWidget SettingsControl { get; }

		private static CheckBox CreateToggleSwitch(ToggleSwitchConfig toggleSwitchConfig, Color textColor)
		{
			if (toggleSwitchConfig == null)
			{
				return null;
			}

			var toggleSwitch = ImageButtonFactory.CreateToggleSwitch(toggleSwitchConfig.Checked, textColor);
			toggleSwitch.VAnchor = VAnchor.Center;
			toggleSwitch.Margin = new BorderDouble(left: 16);
			toggleSwitch.CheckedStateChanged += (sender, e) =>
			{
				toggleSwitchConfig.ToggleAction?.Invoke(toggleSwitch.Checked);
			};

			return toggleSwitch;
		}
	}
}