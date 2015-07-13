using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using System;
using System.IO;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class CloudSettingsWidget : SettingsViewBase
	{
		private DisableableWidget notificationSettingsContainer;

		public CloudSettingsWidget()
			: base(LocalizedString.Get("Cloud Settings"))
		{
			mainContainer.AddChild(new HorizontalLine(separatorLineColor));

			notificationSettingsContainer = new DisableableWidget();
			notificationSettingsContainer.AddChild(GetNotificationControls());
			mainContainer.AddChild(notificationSettingsContainer);

			AddChild(mainContainer);


			AddHandlers();
		}

		private void SetDisplayAttributes()
		{
			//this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
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

		private FlowLayoutWidget GetCloudMonitorControls()
		{
			FlowLayoutWidget buttonBar = new FlowLayoutWidget();
			buttonBar.HAnchor |= HAnchor.ParentLeftRight;
			buttonBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;
			buttonBar.Margin = new BorderDouble(0, 0, 0, 0);
			buttonBar.Padding = new BorderDouble(0);

			this.textImageButtonFactory.FixedHeight = TallButtonHeight;

			Agg.Image.ImageBuffer cloudMonitorImage = StaticData.Instance.LoadIcon(Path.Combine("PrintStatusControls", "cloud-24x24.png"));
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				InvertLightness.DoInvertLightness(cloudMonitorImage);
			}

			return buttonBar;
		}

		private TextWidget notificationSettingsLabel;

		private FlowLayoutWidget GetNotificationControls()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor |= HAnchor.ParentLeftRight;
			buttonRow.VAnchor |= Agg.UI.VAnchor.ParentCenter;
			buttonRow.Margin = new BorderDouble(0, 0, 0, 0);
			buttonRow.Padding = new BorderDouble(0);

			this.textImageButtonFactory.FixedHeight = TallButtonHeight;

			Agg.Image.ImageBuffer notificationSettingsImage = StaticData.Instance.LoadIcon(Path.Combine("PrintStatusControls", "notify-24x24.png"));
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				InvertLightness.DoInvertLightness(notificationSettingsImage);
			}

			ImageWidget levelingIcon = new ImageWidget(notificationSettingsImage);
			levelingIcon.Margin = new BorderDouble(right: 6, bottom: 6);

			notificationSettingsLabel = new TextWidget(LocalizedString.Get("Notification Settings"));
			notificationSettingsLabel.AutoExpandBoundsToText = true;
			notificationSettingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			notificationSettingsLabel.VAnchor = VAnchor.ParentCenter;

			GuiWidget levelingSwitchContainer = new FlowLayoutWidget();
			levelingSwitchContainer.VAnchor = VAnchor.ParentCenter;
			levelingSwitchContainer.Margin = new BorderDouble(left: 16);

			CheckBox enablePrintNotificationsSwitch = ImageButtonFactory.CreateToggleSwitch(UserSettings.Instance.get("PrintNotificationsEnabled") == "true");
			enablePrintNotificationsSwitch.VAnchor = VAnchor.ParentCenter;
			enablePrintNotificationsSwitch.CheckedStateChanged += (sender, e) =>
			{
				UserSettings.Instance.set("PrintNotificationsEnabled", enablePrintNotificationsSwitch.Checked ? "true" : "false");
			};
			levelingSwitchContainer.AddChild(enablePrintNotificationsSwitch);
			levelingSwitchContainer.SetBoundsToEncloseChildren();

			buttonRow.AddChild(levelingIcon);
			buttonRow.AddChild(notificationSettingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(levelingSwitchContainer);

			return buttonRow;
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private event EventHandler unregisterEvents;

		private void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
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