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
        private DisableableWidget cloudSyncContainer;

		private Button configureNotificationSettingsButton;

		public CloudSettingsWidget()
			: base("Cloud".Localize())
		{
			mainContainer.AddChild(new HorizontalLine(50));

			notificationSettingsContainer = new DisableableWidget();
			notificationSettingsContainer.AddChild(GetNotificationControls());
			mainContainer.AddChild(notificationSettingsContainer);
			mainContainer.AddChild(new HorizontalLine(50));
			cloudSyncContainer = new DisableableWidget();
			cloudSyncContainer.AddChild(GetCloudSyncDashboardControls());
			mainContainer.AddChild(cloudSyncContainer);

			AddChild(mainContainer);

			AddHandlers();
		}

		private void SetDisplayAttributes()
		{
			this.Margin = new BorderDouble(2, 4, 2, 0);
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
			this.textImageButtonFactory.disabledFillColor = RGBA_Bytes.White;

			this.textImageButtonFactory.FixedHeight = TallButtonHeight;
			this.textImageButtonFactory.fontSize = 11;

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.DarkGray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			this.linkButtonFactory.fontSize = 11;
		}

		public static Action enableCloudMonitorFunction = null;

		private void enableCloudMonitor_Click(object sender, EventArgs mouseEvent)
		{
			if (enableCloudMonitorFunction != null)
			{
				UiThread.RunOnIdle(enableCloudMonitorFunction);
			}
		}

		private FlowLayoutWidget GetCloudSyncDashboardControls()
		{
			FlowLayoutWidget cloudSyncContainer = new FlowLayoutWidget();
			cloudSyncContainer.HAnchor |= HAnchor.ParentLeftRight;
			cloudSyncContainer.VAnchor |= Agg.UI.VAnchor.ParentCenter;
			cloudSyncContainer.Margin = new BorderDouble(0, 0, 0, 0);
			cloudSyncContainer.Padding = new BorderDouble(0);

			ImageBuffer cloudMonitorImage = StaticData.Instance.LoadIcon("cloud-24x24.png").InvertLightness();
			cloudMonitorImage.SetRecieveBlender(new BlenderPreMultBGRA());
			int iconSize = (int)(24 * GuiWidget.DeviceScale);
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				cloudMonitorImage.InvertLightness();
			}

            ImageWidget cloudSyncIcon = new ImageWidget(cloudMonitorImage);
            cloudSyncIcon.Margin = new BorderDouble(right: 6, bottom: 6);
			cloudSyncIcon.VAnchor = VAnchor.ParentCenter;

			TextWidget cloudSyncLabel = new TextWidget("Cloud Sync".Localize());
            cloudSyncLabel.AutoExpandBoundsToText = true;
            cloudSyncLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            cloudSyncLabel.VAnchor = VAnchor.ParentCenter;

            linkButtonFactory.fontSize = 10;
            Button cloudSyncGoLink = linkButtonFactory.Generate("Go to Dashboard".Localize().ToUpper());
            cloudSyncGoLink.ToolTipText = "Open cloud sync dashboard in web browser".Localize();
            cloudSyncGoLink.Click += cloudSyncGoButton_Click;
			cloudSyncGoLink.VAnchor = VAnchor.ParentCenter;


			cloudSyncContainer.AddChild(cloudSyncIcon);
            cloudSyncContainer.AddChild(cloudSyncLabel);
            cloudSyncContainer.AddChild(new HorizontalSpacer()
			{
				VAnchor = VAnchor.ParentCenter,
			});
            cloudSyncContainer.AddChild(cloudSyncGoLink);

			return cloudSyncContainer;
		}

		private TextWidget notificationSettingsLabel;

		private FlowLayoutWidget GetNotificationControls()
		{
			FlowLayoutWidget notificationSettingsContainer = new FlowLayoutWidget();
			notificationSettingsContainer.HAnchor |= HAnchor.ParentLeftRight;
			notificationSettingsContainer.VAnchor |= Agg.UI.VAnchor.ParentCenter;
			notificationSettingsContainer.Margin = new BorderDouble(0, 0, 0, 0);
			notificationSettingsContainer.Padding = new BorderDouble(0);

			this.textImageButtonFactory.FixedHeight = TallButtonHeight;

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

			configureNotificationSettingsButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
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
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

        public static Action openUserDashBoardFunction = null;

        private void cloudSyncGoButton_Click(object sender, EventArgs mouseEvent)
        {
            if(openUserDashBoardFunction != null)
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