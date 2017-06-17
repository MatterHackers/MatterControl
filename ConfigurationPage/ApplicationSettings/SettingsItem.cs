using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class SettingsItem : FlowLayoutWidget
	{
		public class ToggleSwitchConfig
		{
			public bool Checked { get; set; }
			public Action<bool> ToggleAction { get; set; }
		}

		public SettingsItem(string text, TextImageButtonFactory buttonFactory, ToggleSwitchConfig toggleSwitchConfig = null, GuiWidget optionalControls = null, ImageBuffer iconImage = null)
			: base(FlowDirection.LeftToRight)
		{
			var switchContainer = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(left: 16),
				Width = 45
			};

			CheckBox toggleSwitch = null;
			if (toggleSwitchConfig != null)
			{
				toggleSwitch = GenerateToggleSwitch(toggleSwitchConfig.Checked);
				toggleSwitch.CheckedStateChanged += (sender, e) =>
				{
					toggleSwitchConfig.ToggleAction?.Invoke(toggleSwitch.Checked);
				};
				switchContainer.AddChild(toggleSwitch);
				switchContainer.SetBoundsToEncloseChildren();
			}

			CreateChildControls(text, switchContainer, optionalControls, iconImage);
		}

		public SettingsItem (string text, TextImageButtonFactory buttonFactory, GuiWidget settingsControls, GuiWidget optionalControls = null, ImageBuffer iconImage = null)
			: base (FlowDirection.LeftToRight)
		{
			CreateChildControls(text, settingsControls, optionalControls, iconImage);
		}

		private void CreateChildControls(string text, GuiWidget settingsControls, GuiWidget optionalControls, ImageBuffer imageBuffer = null)
		{
			this.HAnchor = HAnchor.ParentLeftRight;
			this.Height = 40;

			var sectionLabel = new TextWidget(text)
			{
				AutoExpandBoundsToText = true,
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.ParentCenter
			};

			if (imageBuffer != null)
			{
				if (!ActiveTheme.Instance.IsDarkTheme)
				{
					InvertLightness.DoInvertLightness(imageBuffer);
				}

				this.AddChild(new ImageWidget(imageBuffer)
				{
					Margin = new BorderDouble(right: 6, left: 6),
					VAnchor = VAnchor.ParentCenter
				});
			}
			else
			{
				// Add an icon place holder to get consistent label indenting on items lacking icons 
				this.AddChild(new GuiWidget()
				{
					Width = 24 + 12,
					Height = 24,
					Margin = new BorderDouble(0)
				});
			}

			// Add flag to align all labels - fill empty space if sectionIconPath is empty
			this.AddChild(sectionLabel, -1);
			this.AddChild(new HorizontalSpacer());
			if (optionalControls != null)
			{
				this.AddChild(optionalControls);
			}
			this.AddChild(settingsControls);
		}

		private CheckBox GenerateToggleSwitch(bool initiallyChecked)
		{
			CheckBox toggleSwitch = ImageButtonFactory.CreateToggleSwitch(initiallyChecked);
			toggleSwitch.VAnchor = Agg.UI.VAnchor.ParentCenter;

			return toggleSwitch;
		}
	}
}