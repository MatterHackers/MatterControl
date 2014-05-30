using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg.Image;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class HelpTextWidget : TextWidget
    {
        string defaultHelpMessage = "";
        string hoverHelpMessage = "";

        public bool showHoverText = false;

        static HelpTextWidget globalInstance;

        public static HelpTextWidget Instance
        {
            get
            {
                if (globalInstance == null)
                {
                    globalInstance = new HelpTextWidget("");
                }
                return globalInstance;
            }
        }

        public void ShowHoverText(string message)
        {
            if (this.Text != message)
            {
                hoverHelpMessage = message;
                if (!showHoverText)
                {
                    showHoverText = true;
                    this.Invalidate();
                }
            }
        }

        public void HideHoverText()
        {
            showHoverText = false;
            this.Invalidate();
        }

        public HelpTextWidget(string initialText)
            : base(initialText, pointSize: 10, ellipsisIfClipped: true)
        {
            this.HAnchor = HAnchor.ParentLeftRight;
            this.VAnchor = VAnchor.ParentCenter;
            this.Margin = new BorderDouble(0);
            this.TextColor = RGBA_Bytes.White;
            this.MinimumSize = new Vector2(LocalBounds.Width, LocalBounds.Height);
            AddHandlers();
            setHelpMessageFromStatus();
        }

        private void SetDefaultMessage(string message)
        {
            if (message != this.defaultHelpMessage)
            {
                this.defaultHelpMessage = message;
                this.Invalidate();
            }
        }

        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }

            base.OnClosed(e);
        }

        private void onPrinterStatusChanged(object sender, EventArgs e)
        {
            setHelpMessageFromStatus();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            if (this.showHoverText == true && this.Text != hoverHelpMessage)
            {
                this.Text = hoverHelpMessage;
            }
            else
            {
                if (this.Text != defaultHelpMessage)
                {
                    this.Text = defaultHelpMessage;
                }
            }

            base.OnDraw(graphics2D);
        }

        private void setHelpMessageFromStatus()
        {
            string newMessage = getHelpMessageFromStatus();
            SetDefaultMessage(newMessage);
        }

        private string getHelpMessageFromStatus()
        {
            return "";
            //if (ActivePrinterProfile.Instance.ActivePrinter == null)
            //{
            //    return LocalizedString.Get("Press 'Connect' to choose a printer.");
            //}
            //else
            //{
            //    switch (PrinterCommunication.Instance.CommunicationState)
            //    {
            //        case PrinterCommunication.CommunicationStates.Disconnected:
            //            return LocalizedString.Get("Not connected. Press 'Connect' to enable printing.");
            //        case PrinterCommunication.CommunicationStates.AttemptingToConnect:
            //            string attemptToConnect = LocalizedString.Get ("Attempting to Connect");
            //            string attemptToConnectFull = string.Format ("{0}...", attemptToConnect);
            //            return  attemptToConnectFull;               
            //        case PrinterCommunication.CommunicationStates.ConnectionLost:
            //        case PrinterCommunication.CommunicationStates.FailedToConnect:
            //            return LocalizedString.Get("Unable to communicate with printer.");
            //        case PrinterCommunication.CommunicationStates.Connected:
            //            if (PrinterCommunication.Instance.ActivePrintItem != null)
            //            {
            //                return LocalizedString.Get("Press 'Start' to begin your print.");
            //            }
            //            else
            //            {
            //                return LocalizedString.Get("No items to select. Press 'Add' to select a file to print.");
            //            }
            //        default:
            //            return "";
            //    }
            //}
        }
    }
}
