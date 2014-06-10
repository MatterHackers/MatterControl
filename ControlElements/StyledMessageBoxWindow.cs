using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
    public class StyledMessageBox : SystemWindow
    {
        public EventHandler ClickedOk;
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public enum MessageType { OK, YES_NO };

        public static bool ShowMessageBox(String message, string caption, MessageType messageType = MessageType.OK)
        {
            return ShowMessageBox(message, caption, null, messageType);
        }

        public static bool ShowMessageBox(string message, string caption, GuiWidget[] extraWidgetsToAdd, MessageType messageType)
        {
            EnglishTextWrapping wrapper = new EnglishTextWrapping(12);
            string wrappedMessage = wrapper.InsertCRs(message, 300 - 6);
            StyledMessageBox messageBox = new StyledMessageBox(wrappedMessage, caption, messageType, extraWidgetsToAdd, 400, 300);
            bool okClicked = false;
            messageBox.ClickedOk += (sender, e) => { okClicked = true; };
            messageBox.ShowAsSystemWindow();
            return okClicked;
        }

        public StyledMessageBox(String message, string windowTitle, MessageType messageType, GuiWidget[] extraWidgetsToAdd, double width, double height)
            : base(width, height)
        {
            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.AnchorAll();
            topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

            // Creates Header
            FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            headerRow.HAnchor = HAnchor.ParentLeftRight;
            headerRow.Margin = new BorderDouble(0, 3, 0, 0);
            headerRow.Padding = new BorderDouble(0, 3, 0, 3);
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            //Creates Text and adds into header 
            {
                TextWidget elementHeader = new TextWidget(windowTitle, pointSize: 14);
                elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                elementHeader.HAnchor = HAnchor.ParentLeftRight;
                elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

                headerRow.AddChild(elementHeader);
                topToBottom.AddChild(headerRow);
                this.AddChild(topToBottom);
            }

            //Creates container in the middle of window
            FlowLayoutWidget middleRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {
                middleRowContainer.HAnchor = HAnchor.ParentLeftRight;
                middleRowContainer.VAnchor = VAnchor.ParentBottomTop;
                middleRowContainer.Padding = new BorderDouble(5, 5, 5, 15);
                middleRowContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            }

            TextWidget messageContainer = new TextWidget(message, textColor: ActiveTheme.Instance.PrimaryTextColor);
            messageContainer.HAnchor = Agg.UI.HAnchor.ParentLeft;
            middleRowContainer.AddChild(messageContainer);

            if (extraWidgetsToAdd != null)
            {
                foreach (GuiWidget widget in extraWidgetsToAdd)
                {
                    middleRowContainer.AddChild(widget);
                }
            }

            topToBottom.AddChild(middleRowContainer);

            //Creates button container on the bottom of window 
            FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            {
                BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
                buttonRow.HAnchor = HAnchor.ParentLeftRight;
                buttonRow.Padding = new BorderDouble(0, 3);
            }

            textImageButtonFactory.FixedWidth = 50;
            
            switch (messageType)
            {
                case MessageType.YES_NO:
                    {
                        Title = "MatterControl - " + "Input Required".Localize();
                        Button yesButton = textImageButtonFactory.Generate(LocalizedString.Get("Yes"), centerText: true);
                        yesButton.Click += new ButtonBase.ButtonEventHandler(okButton_Click);
                        yesButton.Cursor = Cursors.Hand;
                        buttonRow.AddChild(yesButton);

                        buttonRow.AddChild(new HorizontalSpacer());

                        Button noButton = textImageButtonFactory.Generate(LocalizedString.Get("No"), centerText: true);
                        noButton.Click += new ButtonBase.ButtonEventHandler(noButton_Click);
                        noButton.Cursor = Cursors.Hand;
                        buttonRow.AddChild(noButton);
                    }
                    break;

                case MessageType.OK:
                    {
                        Title = "MatterControl - " + "Alert".Localize();
                        Button okButton = textImageButtonFactory.Generate(LocalizedString.Get("Ok"), centerText: true);
                        okButton.Cursor = Cursors.Hand;
                        okButton.Click += new ButtonBase.ButtonEventHandler(okButton_Click);
                        buttonRow.AddChild(okButton);
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            topToBottom.AddChild(buttonRow);

            IsModal = true;
        }

        void noButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(CloseOnIdle);
        }

        void okButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            if (ClickedOk != null)
            {
                ClickedOk(this, null);
            }
            UiThread.RunOnIdle(CloseOnIdle);
        }

        void CloseOnIdle(object state)
        {
            Close();
        }
    }
}
