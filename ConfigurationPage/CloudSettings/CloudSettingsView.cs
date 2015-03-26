using System;
using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage
{
    public class CloudSettingsWidget : SettingsViewBase
    {
        DisableableWidget cloudMonitorContainer;
        DisableableWidget notificationSettingsContainer;
        
        Button enableCloudMonitorButton;
        Button disableCloudMonitorButton;
        Button goCloudMonitoringWebPageButton;
        
        Button cloudMonitorInstructionsLink;
        TextWidget cloudMonitorStatusLabel;        
        Button configureNotificationSettingsButton;
        
        public CloudSettingsWidget()
			: base(LocalizedString.Get("Cloud Settings"))
        {
            cloudMonitorContainer = new DisableableWidget();
            cloudMonitorContainer.AddChild(GetCloudMonitorControls());
            mainContainer.AddChild(cloudMonitorContainer);

            mainContainer.AddChild(new HorizontalLine(separatorLineColor));

            notificationSettingsContainer = new DisableableWidget();
            notificationSettingsContainer.AddChild(GetNotificationControls());
            mainContainer.AddChild(notificationSettingsContainer);

            AddChild(mainContainer);

            SetCloudButtonVisiblity();
            
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

        public delegate void EnableCloudMonitor(object state);
        public static EnableCloudMonitor enableCloudMonitorFunction = null;
        void enableCloudMonitor_Click(object sender, EventArgs mouseEvent)
        {
            if (enableCloudMonitorFunction != null)
            {
                UiThread.RunOnIdle((state) =>
                {
                    enableCloudMonitorFunction(null);
                });
            }
        }

        public delegate void DisableCloudMonitor(object state);
        public static DisableCloudMonitor disableCloudMonitorFunction = null;
        void disableCloudMonitor_Click(object sender, EventArgs mouseEvent)
        {
            PrinterSettings.Instance.set("CloudMonitorEnabled", "false");
            ApplicationController.Instance.ChangeCloudSyncStatus();
            ApplicationController.Instance.ReloadAdvancedControlsPanel();
            if (disableCloudMonitorFunction != null)
            {
                UiThread.RunOnIdle((state) =>
                {
                    disableCloudMonitorFunction(null);
                });
            }
        }


        public delegate void OpenDashboardPage(object state);
        public static OpenDashboardPage openDashboardPageFunction = null;
        void goCloudMonitoringWebPageButton_Click(object sender, EventArgs mouseEvent)
        {
            if (openDashboardPageFunction != null)
            {
                UiThread.RunOnIdle((state) =>
                {
                    openDashboardPageFunction(null);
                });
            }
        }

        public delegate void OpenInstructionsPage(object state);
        public static OpenInstructionsPage openInstructionsPageFunction = null;
        void goCloudMonitoringInstructionsButton_Click(object sender, EventArgs mouseEvent)
        {
            if (openDashboardPageFunction != null)
            {
                UiThread.RunOnIdle((state) =>
                {
                    openInstructionsPageFunction(null);
                });
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

            ImageWidget cloudMonitoringIcon = new ImageWidget(cloudMonitorImage);
            cloudMonitoringIcon.Margin = new BorderDouble(right: 6);

            enableCloudMonitorButton = textImageButtonFactory.Generate("Enable".Localize().ToUpper());
            enableCloudMonitorButton.Margin = new BorderDouble(left: 6);
            enableCloudMonitorButton.VAnchor = VAnchor.ParentCenter;
            enableCloudMonitorButton.Click += new EventHandler(enableCloudMonitor_Click);

            disableCloudMonitorButton = textImageButtonFactory.Generate("Disable".Localize().ToUpper());
            disableCloudMonitorButton.Margin = new BorderDouble(left: 6);
            disableCloudMonitorButton.VAnchor = VAnchor.ParentCenter;
            disableCloudMonitorButton.Click += new EventHandler(disableCloudMonitor_Click);

            cloudMonitorInstructionsLink = linkButtonFactory.Generate("More Info".Localize().ToUpper());
            cloudMonitorInstructionsLink.VAnchor = VAnchor.ParentCenter;
            cloudMonitorInstructionsLink.Click += new EventHandler(goCloudMonitoringInstructionsButton_Click);
            cloudMonitorInstructionsLink.Margin = new BorderDouble(left: 6);

            goCloudMonitoringWebPageButton = linkButtonFactory.Generate("View Status".Localize().ToUpper());
            goCloudMonitoringWebPageButton.VAnchor = VAnchor.ParentCenter;
            goCloudMonitoringWebPageButton.Click += new EventHandler(goCloudMonitoringWebPageButton_Click);
            goCloudMonitoringWebPageButton.Margin = new BorderDouble(left: 6);

            cloudMonitorStatusLabel = new TextWidget("");
            cloudMonitorStatusLabel.AutoExpandBoundsToText = true;
            cloudMonitorStatusLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            cloudMonitorStatusLabel.VAnchor = VAnchor.ParentCenter;

            GuiWidget hSpacer = new GuiWidget();
            hSpacer.HAnchor = HAnchor.ParentLeftRight;

            buttonBar.AddChild(cloudMonitoringIcon);
            buttonBar.AddChild(cloudMonitorStatusLabel);
            buttonBar.AddChild(cloudMonitorInstructionsLink);
            buttonBar.AddChild(goCloudMonitoringWebPageButton);
            buttonBar.AddChild(hSpacer);
            buttonBar.AddChild(enableCloudMonitorButton);
            buttonBar.AddChild(disableCloudMonitorButton);

            return buttonBar;
        }

        TextWidget notificationSettingsLabel;
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

            configureNotificationSettingsButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
            configureNotificationSettingsButton.Margin = new BorderDouble(left: 6);
            configureNotificationSettingsButton.VAnchor = VAnchor.ParentCenter;
            configureNotificationSettingsButton.Click += new EventHandler(configureNotificationSettingsButton_Click);

			notificationSettingsLabel = new TextWidget(LocalizedString.Get("Notification Settings"));
            notificationSettingsLabel.AutoExpandBoundsToText = true;
            notificationSettingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            notificationSettingsLabel.VAnchor = VAnchor.ParentCenter;

			GuiWidget levelingSwitchContainer = new FlowLayoutWidget();
			levelingSwitchContainer.VAnchor = VAnchor.ParentCenter;
			levelingSwitchContainer.Margin = new BorderDouble(left: 16);

			ToggleSwitch enablePrintNotificationsSwitch = GenerateToggleSwitch(levelingSwitchContainer, UserSettings.Instance.get("PrintNotificationsEnabled") == "true");
			enablePrintNotificationsSwitch.SwitchStateChanged += (sender, e) => 
			{
				UserSettings.Instance.set("PrintNotificationsEnabled", enablePrintNotificationsSwitch.SwitchState ? "true" : "false");
			};
			levelingSwitchContainer.SetBoundsToEncloseChildren();

            buttonRow.AddChild(levelingIcon);
            buttonRow.AddChild(notificationSettingsLabel);
            buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(configureNotificationSettingsButton);
			buttonRow.AddChild(levelingSwitchContainer);

            return buttonRow;
        }

        void SetCloudButtonVisiblity()
        {
            bool cloudMontitorEnabled = (PrinterSettings.Instance.get("CloudMonitorEnabled") == "true");
            enableCloudMonitorButton.Visible = !cloudMontitorEnabled;
            disableCloudMonitorButton.Visible = cloudMontitorEnabled;
            goCloudMonitoringWebPageButton.Visible = cloudMontitorEnabled;


            if (cloudMontitorEnabled)
            {
                cloudMonitorStatusLabel.Text = LocalizedString.Get("Cloud Monitoring (enabled)");
            }
            else
            {
                cloudMonitorStatusLabel.Text = LocalizedString.Get("Cloud Monitoring (disabled)");
            }
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
        }

        private void onPrinterStatusChanged(object sender, EventArgs e)
        {
            SetVisibleControls();
            this.Invalidate();
        }

        public delegate void OpenNotificationFormWindow(object state);
        public static OpenNotificationFormWindow openPrintNotificationFunction = null;
        void configureNotificationSettingsButton_Click(object sender, EventArgs mouseEvent)
        {
            if (openPrintNotificationFunction != null)
            {
                UiThread.RunOnIdle((state) =>
                {
                    openPrintNotificationFunction(null);
                });
            }
        }

        private void SetVisibleControls()
        {
            if (ActivePrinterProfile.Instance.ActivePrinter == null)
            {
                // no printer selected                         
                cloudMonitorContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
            }
            else // we at least have a printer selected
            {
                cloudMonitorContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
            }
        }
    }
}