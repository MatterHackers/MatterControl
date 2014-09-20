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
    public class HardwareSettingsWidget : SettingsViewBase
    {
        Button configureAutoLevelButton;
        Button configureEePromButton;

        public HardwareSettingsWidget()
            : base("Hardware Settings")
        {   
            mainContainer.AddChild(GetAutoLevelControl());
            mainContainer.AddChild(new HorizontalLine(separatorLineColor));
            mainContainer.AddChild(GetEEPromControl());

            AddChild(mainContainer);

            AddHandlers();
        }



        private FlowLayoutWidget GetAutoLevelControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(top: 4);

            configureEePromButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
            configureEePromButton.Margin = new BorderDouble(left: 6);
            configureEePromButton.VAnchor = VAnchor.ParentCenter;

            TextWidget notificationSettingsLabel = new TextWidget("Automatic Print Leveling");
            notificationSettingsLabel.AutoExpandBoundsToText = true;
            notificationSettingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            notificationSettingsLabel.VAnchor = VAnchor.ParentCenter;

            buttonRow.AddChild(notificationSettingsLabel);
            buttonRow.AddChild(new HorizontalSpacer());
            buttonRow.AddChild(configureEePromButton);
            return buttonRow;
        }

        private FlowLayoutWidget GetEEPromControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(top: 4);

            configureAutoLevelButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
            configureAutoLevelButton.Margin = new BorderDouble(left: 6);
            configureAutoLevelButton.VAnchor = VAnchor.ParentCenter;

            TextWidget notificationSettingsLabel = new TextWidget("EEProm Settings");
            notificationSettingsLabel.AutoExpandBoundsToText = true;
            notificationSettingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            notificationSettingsLabel.VAnchor = VAnchor.ParentCenter;

            buttonRow.AddChild(notificationSettingsLabel);
            buttonRow.AddChild(new HorizontalSpacer());
            buttonRow.AddChild(configureAutoLevelButton);
            return buttonRow;
        }     

        private void AddHandlers()
        {
            configureAutoLevelButton.Click += new ButtonBase.ButtonEventHandler(configureAutoLevelButton_Click);
            configureEePromButton.Click += new ButtonBase.ButtonEventHandler(configureEePromButton_Click);
        }

        void configureEePromButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
            {
                //Do stuff
            });
        }

        void configureAutoLevelButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
            {
                //Do stuff
            });
        }

    }
}