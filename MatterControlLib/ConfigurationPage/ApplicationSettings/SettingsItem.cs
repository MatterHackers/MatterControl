using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class SettingsItem : SettingsRow
	{
		public class ToggleSwitchConfig
		{
			public bool Checked { get; set; }

			public string Name { get; set; }

			public Action<bool> ToggleAction { get; set; }
		}

		public SettingsItem(string text, ThemeConfig theme, ToggleSwitchConfig toggleSwitchConfig = null, GuiWidget optionalControls = null, ImageBuffer iconImage = null, bool enforceGutter = true)
			: this(text, CreateToggleSwitch(toggleSwitchConfig, theme), theme, optionalControls, iconImage, enforceGutter)
		{
		}

		public SettingsItem (string text, GuiWidget settingsControls, ThemeConfig theme, GuiWidget optionalControls = null, ImageBuffer imageBuffer = null, bool enforceGutter = true)
			: base (text, "", theme, imageBuffer)
		{
			this.SettingsControl = settingsControls;

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

		private static GuiWidget CreateToggleSwitch(ToggleSwitchConfig toggleSwitchConfig, ThemeConfig theme)
		{
			if (toggleSwitchConfig == null)
			{
				return null;
			}

			var toggleSwitch = new RoundedToggleSwitch(theme)
			{
				VAnchor = VAnchor.Center,
				Checked = toggleSwitchConfig.Checked,
				Name = toggleSwitchConfig.Name,
				Margin = new BorderDouble(left: 16),
			};

			toggleSwitch.CheckedStateChanged += (sender, e) =>
			{
				toggleSwitchConfig.ToggleAction?.Invoke(toggleSwitch.Checked);
			};

			return toggleSwitch;
		}
	}
}