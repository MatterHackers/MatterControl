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

		private RGBA_Bytes menuTextColor = RGBA_Bytes.Black;

		public SettingsItem(string text, ToggleSwitchConfig toggleSwitchConfig = null, GuiWidget optionalControls = null, ImageBuffer iconImage = null, bool enforceGutter = true)
			: base(FlowDirection.LeftToRight)
		{
			this.HAnchor = HAnchor.Stretch;
			var switchContainer = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 16),
				Width = 45
			};

			if (toggleSwitchConfig != null)
			{
				CheckBox toggleSwitch = ImageButtonFactory.CreateToggleSwitch(toggleSwitchConfig.Checked, menuTextColor);
				toggleSwitch.VAnchor = Agg.UI.VAnchor.Center;
				toggleSwitch.CheckedStateChanged += (sender, e) =>
				{
					toggleSwitchConfig.ToggleAction?.Invoke(toggleSwitch.Checked);
				};
				switchContainer.AddChild(toggleSwitch);
				switchContainer.SetBoundsToEncloseChildren();
			}

			CreateChildControls(text, switchContainer, optionalControls, enforceGutter, iconImage);
		}

		public SettingsItem (string text, GuiWidget settingsControls, GuiWidget optionalControls = null, ImageBuffer iconImage = null, bool enforceGutter = true)
			: base (FlowDirection.LeftToRight)
		{
			CreateChildControls(text, settingsControls, optionalControls, enforceGutter, iconImage);
		}

		private void CreateChildControls(string text, GuiWidget settingsControls, GuiWidget optionalControls, bool enforceGutter, ImageBuffer imageBuffer = null)
		{
			this.HAnchor = HAnchor.Stretch;
			this.MinimumSize = new Vector2(0, 40);

			settingsControls.VAnchor |= VAnchor.Center;

			if (optionalControls != null)
			{
				optionalControls.VAnchor |= VAnchor.Center;
			}

			if (imageBuffer != null)
			{
				if (!ActiveTheme.Instance.IsDarkTheme)
				{
					InvertLightness.DoInvertLightness(imageBuffer);
				}

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

			this.AddChild(settingsControls);
		}
	}
}