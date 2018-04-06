using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

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
			: base (text, "", textColor, ApplicationController.Instance.Theme)
		{
			var theme = ApplicationController.Instance.Theme;
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

		private static GuiWidget CreateToggleSwitch(ToggleSwitchConfig toggleSwitchConfig, Color textColor)
		{
			if (toggleSwitchConfig == null)
			{
				return null;
			}

			var toggleSwitch = new RoundedToggleSwitch(ApplicationController.Instance.Theme)
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