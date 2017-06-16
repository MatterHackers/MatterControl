using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PluginSystem;
using System;
using System.IO;
using MatterHackers.Agg.Image;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class CloudSettingsWidget : SettingsViewBase
	{
		private DisableableWidget notificationSettingsContainer;

		private Button configureNotificationSettingsButton;

		public CloudSettingsWidget(TextImageButtonFactory buttonFactory)
			: base("Cloud".Localize(), buttonFactory)
		{
			mainContainer.AddChild(new HorizontalLine(50));

			notificationSettingsContainer = new DisableableWidget();
			notificationSettingsContainer.AddChild(GetNotificationControls());
			mainContainer.AddChild(notificationSettingsContainer);
			mainContainer.AddChild(new HorizontalLine(50));

			AddChild(mainContainer);

			AddHandlers();
		}

		public static Action enableCloudMonitorFunction = null;

		private void enableCloudMonitor_Click(object sender, EventArgs mouseEvent)
		{
			if (enableCloudMonitorFunction != null)
			{
				UiThread.RunOnIdle(enableCloudMonitorFunction);
			}
		}

		private TextWidget notificationSettingsLabel;

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

			configureNotificationSettingsButton = buttonFactory.Generate("Configure".Localize().ToUpper());
			configureNotificationSettingsButton.Name = "Configure Notification Settings Button";
			configureNotificationSettingsButton.Margin = new BorderDouble(left: 6);
			configureNotificationSettingsButton.VAnchor = VAnchor.ParentCenter;
			configureNotificationSettingsButton.Click += configureNotificationSettingsButton_Click;

			notificationSettingsLabel = new TextWidget("Notifications".Localize());
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

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public static Action openUserDashBoardFunction = null;

		private void cloudSyncGoButton_Click(object sender, EventArgs mouseEvent)
		{
			if (openUserDashBoardFunction != null)
			{
				UiThread.RunOnIdle(openUserDashBoardFunction);
			}
		}

		private EventHandler unregisterEvents;

		private void AddHandlers()
		{
			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnection.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			this.Invalidate();
		}

		public static Action openPrintNotificationFunction = null;

		private void configureNotificationSettingsButton_Click(object sender, EventArgs mouseEvent)
		{
			if (openPrintNotificationFunction != null)
			{
				UiThread.RunOnIdle(openPrintNotificationFunction);
			}
		}

	}
}