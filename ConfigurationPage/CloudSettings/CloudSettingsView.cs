using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class CloudSettingsWidget : SettingsViewBase
	{
		public static Action openPrintNotificationFunction = null;

		public CloudSettingsWidget(TextImageButtonFactory buttonFactory)
			: base("Cloud".Localize(), buttonFactory)
		{
			mainContainer.AddChild(new HorizontalLine(50));

			var notificationSettingsContainer = new DisableableWidget();
			notificationSettingsContainer.AddChild(GetNotificationControls());
			mainContainer.AddChild(notificationSettingsContainer);
			mainContainer.AddChild(new HorizontalLine(50));

			AddChild(mainContainer);
		}

		private FlowLayoutWidget GetNotificationControls()
		{
			FlowLayoutWidget notificationSettingsContainer = new FlowLayoutWidget();
			notificationSettingsContainer.HAnchor |= HAnchor.ParentLeftRight;
			notificationSettingsContainer.VAnchor |= Agg.UI.VAnchor.ParentCenter;
			notificationSettingsContainer.Margin = new BorderDouble(0, 0, 0, 0);
			notificationSettingsContainer.Padding = new BorderDouble(0);

			ImageBuffer notifiImage = StaticData.Instance.LoadIcon("notify-24x24.png").InvertLightness();
			notifiImage.SetRecieveBlender(new BlenderPreMultBGRA());
			int iconSize = (int)(24 * GuiWidget.DeviceScale);
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				notifiImage.InvertLightness();
			}

			ImageWidget notificationSettingsIcon = new ImageWidget(notifiImage);
			notificationSettingsIcon.VAnchor = VAnchor.ParentCenter;
			notificationSettingsIcon.Margin = new BorderDouble(right: 6, bottom: 6);

			var configureNotificationSettingsButton = buttonFactory.Generate("Configure".Localize().ToUpper());
			configureNotificationSettingsButton.Name = "Configure Notification Settings Button";
			configureNotificationSettingsButton.Margin = new BorderDouble(left: 6);
			configureNotificationSettingsButton.VAnchor = VAnchor.ParentCenter;
			configureNotificationSettingsButton.Click += (s, e) =>
			{
				if (openPrintNotificationFunction != null)
				{
					UiThread.RunOnIdle(openPrintNotificationFunction);
				}
			};

			var notificationSettingsLabel = new TextWidget("Notifications".Localize());
			notificationSettingsLabel.AutoExpandBoundsToText = true;
			notificationSettingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			notificationSettingsLabel.VAnchor = VAnchor.ParentCenter;

			GuiWidget printNotificationsSwitchContainer = new FlowLayoutWidget();
			printNotificationsSwitchContainer.VAnchor = VAnchor.ParentCenter;
			printNotificationsSwitchContainer.Margin = new BorderDouble(left: 16);

			CheckBox enablePrintNotificationsSwitch = ImageButtonFactory.CreateToggleSwitch(UserSettings.Instance.get("PrintNotificationsEnabled") == "true");
			enablePrintNotificationsSwitch.VAnchor = VAnchor.ParentCenter;
			enablePrintNotificationsSwitch.CheckedStateChanged += (sender, e) =>
			{
				UserSettings.Instance.set("PrintNotificationsEnabled", enablePrintNotificationsSwitch.Checked ? "true" : "false");
			};
			printNotificationsSwitchContainer.AddChild(enablePrintNotificationsSwitch);
			printNotificationsSwitchContainer.SetBoundsToEncloseChildren();

			notificationSettingsContainer.AddChild(notificationSettingsIcon);
			notificationSettingsContainer.AddChild(notificationSettingsLabel);
			notificationSettingsContainer.AddChild(new HorizontalSpacer());
			notificationSettingsContainer.AddChild(configureNotificationSettingsButton);
			notificationSettingsContainer.AddChild(printNotificationsSwitchContainer);

			return notificationSettingsContainer;
		}
	}
}