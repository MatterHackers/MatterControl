using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    public class SetupStepConfigureConnection : SetupConnectionWidgetBase
    {        
        Button nextButton;
        Button skipButton;
        TextWidget printerErrorMessage;

        public SetupStepConfigureConnection(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinter)
            : base(windowController, containerWindowToClose, setupPrinter)
        {            
            contentRow.AddChild(createPrinterConnectionMessageContainer());
            {
                //Construct buttons
                nextButton = textImageButtonFactory.Generate("Connect");
                nextButton.Click += new ButtonBase.ButtonEventHandler(NextButton_Click);

                skipButton = textImageButtonFactory.Generate("Skip");
                skipButton.Click += new ButtonBase.ButtonEventHandler(SkipButton_Click);

                GuiWidget hSpacer = new GuiWidget();
                hSpacer.HAnchor = HAnchor.ParentLeftRight;

                //Add buttons to buttonContainer
                footerRow.AddChild(nextButton);
                footerRow.AddChild(skipButton);
                footerRow.AddChild(hSpacer);
                footerRow.AddChild(cancelButton);
            }
        }

        public FlowLayoutWidget createPrinterConnectionMessageContainer()
        {

            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(5);
            BorderDouble elementMargin = new BorderDouble(top: 5);

            TextWidget continueMessage = new TextWidget("Would you like to connect to this printer now?", 0, 0, 12);
            continueMessage.AutoExpandBoundsToText = true;
            continueMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;            
            continueMessage.HAnchor = HAnchor.ParentLeftRight;
            continueMessage.Margin = elementMargin;

            TextWidget continueMessageTwo = new TextWidget("You can always configure this later.", 0, 0, 10);
            continueMessageTwo.AutoExpandBoundsToText = true;
            continueMessageTwo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            continueMessageTwo.HAnchor = HAnchor.ParentLeftRight;
            continueMessageTwo.Margin = elementMargin;

            printerErrorMessage = new TextWidget("", 0, 0, 10);
            printerErrorMessage.AutoExpandBoundsToText = true;
            printerErrorMessage.TextColor = RGBA_Bytes.Red;
            printerErrorMessage.HAnchor = HAnchor.ParentLeftRight;
            printerErrorMessage.Margin = elementMargin;
            
            container.AddChild(continueMessage);
            container.AddChild(continueMessageTwo);
            container.AddChild(printerErrorMessage);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        private void SkipButton_Click(object sender, MouseEventArgs e)
        {
            //Save the printer info to the datastore and exit the setup process
            this.ActivePrinter.Commit();
            SaveAndExit();
        }

        void MoveToNextWidget(object state)
        {
            // you can call this like this
            //             AfterUiEvents.AddAction(new AfterUIAction(MoveToNextWidget));            
            if (this.ActivePrinter.BaudRate == null)
            {
                Parent.AddChild(new SetupStepBaudRate((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);               
                
            }
            else if (this.PrinterSetupStatus.DriverNeedsToBeInstalled)
            {
                Parent.AddChild(new SetupStepInstallDriver((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
            else
            {
                Parent.AddChild(new SetupStepComPortOne((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
        }

        void NextButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(MoveToNextWidget);
        }
    }
}
