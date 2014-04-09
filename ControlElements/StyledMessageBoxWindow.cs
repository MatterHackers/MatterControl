using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class StyledMessageBox : SystemWindow
    {
        public EventHandler ClickedOk;
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public enum MessageType { OK, YES_NO };

        public static bool ShowMessageBox(String message, string caption, MessageType messageType = MessageType.OK)
        {
            EnglishTextWrapping wrapper = new EnglishTextWrapping(12);
            string wrappedMessage = wrapper.InsertCRs(message, 350);
            StyledMessageBox messageBox = new StyledMessageBox(wrappedMessage, caption, messageType, null, 400, 300);
            bool okClicked = false;
            messageBox.ClickedOk += (sender, e) => { okClicked = true; };
            messageBox.ShowAsSystemWindow();
            return okClicked;
        }

        public static bool ShowMessageBox(string message, string caption, GuiWidget[] extraWidgetsToAdd, MessageType messageType)
        {
            EnglishTextWrapping wrapper = new EnglishTextWrapping(12);
            string wrappedMessage = wrapper.InsertCRs(message, 300);
            StyledMessageBox messageBox = new StyledMessageBox(wrappedMessage, caption, messageType, extraWidgetsToAdd, 400, 300);
            bool okClicked = false;
            messageBox.ClickedOk += (sender, e) => { okClicked = true; };
            messageBox.ShowAsSystemWindow();
            return okClicked;
        }

        public StyledMessageBox(String message, string windowTitle, MessageType messageType, GuiWidget[] extraWidgetsToAdd, double width, double height)
            : base(width, height)
        {
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            textImageButtonFactory.FixedWidth = 50;            

            FlowLayoutWidget topToBottomFlow = new FlowLayoutWidget(FlowDirection.TopToBottom);
            //topToBottomFlow.DebugShowBounds = true;
            TextWidget messageContainer = new TextWidget(message, textColor: ActiveTheme.Instance.PrimaryTextColor);
            messageContainer.HAnchor = Agg.UI.HAnchor.ParentCenter;
            topToBottomFlow.AddChild(messageContainer);

            if (extraWidgetsToAdd != null)
            {
                foreach (GuiWidget widget in extraWidgetsToAdd)
                {
                    topToBottomFlow.AddChild(widget);
                }
            }

            Title = windowTitle;

            // add a spacer
            GuiWidget spacer = new GuiWidget(10, 10);
            spacer.HAnchor |= Agg.UI.HAnchor.ParentCenter;
            //spacer.DebugShowBounds = true;
            topToBottomFlow.AddChild(spacer);
            topToBottomFlow.HAnchor = Agg.UI.HAnchor.ParentCenter | Agg.UI.HAnchor.FitToChildren;
            topToBottomFlow.VAnchor = Agg.UI.VAnchor.ParentCenter | Agg.UI.VAnchor.FitToChildren;

            switch (messageType)
            {
                case MessageType.YES_NO:
                    {
                        FlowLayoutWidget yesNoButtonsFlow = new FlowLayoutWidget();
                        yesNoButtonsFlow.HAnchor |= HAnchor.ParentCenter;

					Button yesButton = textImageButtonFactory.Generate(LocalizedString.Get("Yes"), centerText:true);
                        yesButton.Click += new ButtonBase.ButtonEventHandler(okButton_Click);
                        yesNoButtonsFlow.AddChild(yesButton);

                        GuiWidget buttonSpacer = new GuiWidget(10, 10);
                        yesNoButtonsFlow.AddChild(buttonSpacer);

					Button noButton = textImageButtonFactory.Generate(LocalizedString.Get("No"), centerText: true);
                        noButton.Click += new ButtonBase.ButtonEventHandler(noButton_Click);
                        yesNoButtonsFlow.AddChild(noButton);

                        topToBottomFlow.AddChild(yesNoButtonsFlow);
                    }
                    break;

                case MessageType.OK:
                    {
					Button okButton = textImageButtonFactory.Generate(LocalizedString.Get("Ok"), centerText: true);
                        //okButton.DebugShowBounds = true;
                        okButton.Click += new ButtonBase.ButtonEventHandler(okButton_Click);
                        okButton.HAnchor = HAnchor.ParentCenter;
                        topToBottomFlow.AddChild(okButton);
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            AddChild(topToBottomFlow);

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
