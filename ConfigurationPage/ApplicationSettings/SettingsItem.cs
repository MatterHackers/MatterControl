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

		private static RGBA_Bytes menuTextColor = RGBA_Bytes.Black;

		public SettingsItem(string text, ToggleSwitchConfig toggleSwitchConfig = null, GuiWidget optionalControls = null, ImageBuffer iconImage = null, bool enforceGutter = true)
			: this(text, CreateToggleSwitch(toggleSwitchConfig), optionalControls, iconImage, enforceGutter)
		{
		}

		public SettingsItem (string text, GuiWidget settingsControls, GuiWidget optionalControls = null, ImageBuffer imageBuffer = null, bool enforceGutter = true)
			: base (FlowDirection.LeftToRight)
		{
			this.HAnchor = HAnchor.Stretch;
			this.MinimumSize = new Vector2(0, 40);

			if (optionalControls != null)
			{
				optionalControls.VAnchor |= VAnchor.Center;
			}

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

			this.AddChild(new TextWidget(text)
			{
				AutoExpandBoundsToText = true,
				TextColor = menuTextColor,
				VAnchor = VAnchor.Center,
			});

			this.AddChild(new HorizontalSpacer());

			if (optionalControls != null)
			{
				this.AddChild(optionalControls);
			}

			if (settingsControls != null)
			{
				settingsControls.VAnchor |= VAnchor.Center;
				this.AddChild(settingsControls);
			}
		}

		private static CheckBox CreateToggleSwitch(ToggleSwitchConfig toggleSwitchConfig)
		{
			if (toggleSwitchConfig == null)
			{
				return null;
			}

			var toggleSwitch = ImageButtonFactory.CreateToggleSwitch(toggleSwitchConfig.Checked, menuTextColor);
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