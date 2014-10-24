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
        String unwrappedMessage;
        TextWidget messageContainer;
        FlowLayoutWidget middleRowContainer;
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public delegate void MessageBoxDelegate(bool response);
        MessageBoxDelegate responseCallback;

        public enum MessageType { OK, YES_NO };

        public static void ShowMessageBox(MessageBoxDelegate callback, String message, string caption, MessageType messageType = MessageType.OK, string yesOk = "", string no = "")
        {
            ShowMessageBox(callback, message, caption, null, messageType, yesOk, no);
        }

        public static void ShowMessageBox(MessageBoxDelegate callback, string message, string caption, GuiWidget[] extraWidgetsToAdd, MessageType messageType, string yesOk = "", string no = "")
        {
            StyledMessageBox messageBox = new StyledMessageBox(callback, message, caption, messageType, extraWidgetsToAdd, 400, 300, yesOk, no);            
            messageBox.ShowAsSystemWindow();
            
        }

        public StyledMessageBox(MessageBoxDelegate callback, String message, string windowTitle, MessageType messageType, GuiWidget[] extraWidgetsToAdd, double width, double height, string yesOk, string no)
            : base(width, height)
        {
            responseCallback = callback;
            unwrappedMessage = message;
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
            }

            // Creates container in the middle of window
            middleRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {
                middleRowContainer.HAnchor = HAnchor.ParentLeftRight;
                middleRowContainer.VAnchor = VAnchor.ParentBottomTop;
                // normally the padding for the middle container should be just (5) all around. The has extra top space
                middleRowContainer.Padding = new BorderDouble(5, 5, 5, 15);
                middleRowContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            }

            messageContainer = new TextWidget(message, textColor: ActiveTheme.Instance.PrimaryTextColor);
            messageContainer.AutoExpandBoundsToText = true;
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

            int minButtonWidth = 50;
            
            switch (messageType)
            {
                case MessageType.YES_NO:
                    {
                        Title = "MatterControl - " + "Input Required".Localize();
                        Button yesButton = textImageButtonFactory.Generate(yesOk, centerText: true);
                        if (yesOk == "")
                        {
                            yesOk = "Yes".Localize();
                            textImageButtonFactory.FixedWidth = minButtonWidth;
                            yesButton = textImageButtonFactory.Generate(yesOk, centerText: true);
                            textImageButtonFactory.FixedWidth = 0;
                        }
                        yesButton.Width = Math.Max(minButtonWidth, yesButton.Width);
                        yesButton.Click += new EventHandler(okButton_Click);
                        yesButton.Cursor = Cursors.Hand;
                        buttonRow.AddChild(yesButton);

                        buttonRow.AddChild(new HorizontalSpacer());

                        Button noButton = textImageButtonFactory.Generate(no, centerText: true);
                        if (no == "")
                        {
                            no = "No".Localize();
                            textImageButtonFactory.FixedWidth = minButtonWidth;
                            noButton = textImageButtonFactory.Generate(no, centerText: true);
                            textImageButtonFactory.FixedWidth = 0;
                        }
                        noButton.Width = Math.Max(minButtonWidth, noButton.Width);
                        noButton.Click += new EventHandler(noButton_Click);
                        noButton.Cursor = Cursors.Hand;
                        buttonRow.AddChild(noButton);
                    }
                    break;

                case MessageType.OK:
                    {
                        Title = "MatterControl - " + "Alert".Localize();
                        Button okButton = textImageButtonFactory.Generate(LocalizedString.Get("Ok"), centerText: true);
                        if (yesOk == "")
                        {
                            yesOk = "Ok".Localize();
                            textImageButtonFactory.FixedWidth = minButtonWidth;
                            okButton = textImageButtonFactory.Generate(yesOk, centerText: true);
                            textImageButtonFactory.FixedWidth = 0;
                        }
                        okButton.Width = Math.Max(minButtonWidth, okButton.Width);
                        okButton.Cursor = Cursors.Hand;
                        okButton.Click += new EventHandler(okButton_Click);
                        buttonRow.AddChild(okButton);
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            topToBottom.AddChild(buttonRow);
            this.AddChild(topToBottom);

            IsModal = true;
            AdjustTextWrap();
        }

        public override void OnBoundsChanged(EventArgs e)
        {
            AdjustTextWrap();
            base.OnBoundsChanged(e);
        }

        private void AdjustTextWrap()
        {
            if (messageContainer != null)
            {
                double wrappingSize = middleRowContainer.Width - (middleRowContainer.Padding.Width + messageContainer.Margin.Width);
                if (wrappingSize > 0)
                {
                    EnglishTextWrapping wrapper = new EnglishTextWrapping(12);
                    string wrappedMessage = wrapper.InsertCRs(unwrappedMessage, wrappingSize);
                    messageContainer.Text = wrappedMessage;
                }
            }
        }

        void noButton_Click(object sender, EventArgs mouseEvent)
        {            
            UiThread.RunOnIdle(CloseOnIdle);
            if (responseCallback != null)
            {
                responseCallback(false);
            }
        }

        void okButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle(CloseOnIdle);
            if (responseCallback != null)
            {
                responseCallback(true);
            }
            
        }

        void CloseOnIdle(object state)
        {
            Close();
        }
    }
}
