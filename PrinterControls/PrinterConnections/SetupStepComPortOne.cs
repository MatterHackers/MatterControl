using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    public class SetupStepComPortOne : SetupConnectionWidgetBase
    {

        Button nextButton;

        public SetupStepComPortOne(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinter)
            : base(windowController, containerWindowToClose, setupPrinter)
        {
            contentRow.AddChild(createPrinterConnectionMessageContainer());
            {
                //Construct buttons
				nextButton = textImageButtonFactory.Generate(new LocalizedString("Continue").Translated);
                nextButton.Click += new ButtonBase.ButtonEventHandler(NextButton_Click);                

                GuiWidget hSpacer = new GuiWidget();
                hSpacer.HAnchor = HAnchor.ParentLeftRight;

                //Add buttons to buttonContainer
                footerRow.AddChild(nextButton);
                footerRow.AddChild(hSpacer);

                footerRow.AddChild(cancelButton);
            }
        }

        public FlowLayoutWidget createPrinterConnectionMessageContainer()
        {

            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.VAnchor = VAnchor.ParentBottomTop;
            container.Margin = new BorderDouble(5);            
            BorderDouble elementMargin = new BorderDouble(top: 5);

			TextWidget printerMessageOne = new TextWidget(new LocalizedString("MatterControl will now attempt to auto-detect printer.").Translated, 0, 0, 10);
            printerMessageOne.Margin = new BorderDouble(0, 10, 0,5);
            printerMessageOne.TextColor = RGBA_Bytes.White;
            printerMessageOne.HAnchor = HAnchor.ParentLeftRight;
            printerMessageOne.Margin = elementMargin;

			string printerMessageTwoTxt = new LocalizedString("Disconnect printer").Translated;
			string printerMessageTwoTxtEnd = new LocalizedString("if currently connected").Translated;
			string printerMessageTwoTxtFull = string.Format ("1.) {0} ({1}).", printerMessageTwoTxt, printerMessageTwoTxtEnd);
			TextWidget printerMessageTwo = new TextWidget(printerMessageTwoTxtFull, 0, 0, 12);
            printerMessageTwo.TextColor = RGBA_Bytes.White;
            printerMessageTwo.HAnchor = HAnchor.ParentLeftRight;
            printerMessageTwo.Margin = elementMargin;

			string printerMessageThreeTxt = new LocalizedString("Press").Translated;
			string printerMessageThreeTxtEnd = new LocalizedString ("Continue").Translated;
			string printerMessageThreeFull = string.Format ("2.) {0} '{1}'.", printerMessageThreeTxt, printerMessageThreeTxtEnd);
			TextWidget printerMessageThree = new TextWidget(printerMessageThreeFull, 0, 0, 12);
            printerMessageThree.TextColor = RGBA_Bytes.White;
            printerMessageThree.HAnchor = HAnchor.ParentLeftRight;
            printerMessageThree.Margin = elementMargin;

            GuiWidget vSpacer = new GuiWidget();
            vSpacer.VAnchor = VAnchor.ParentBottomTop;

			Button manualLink = linkButtonFactory.Generate(new LocalizedString("Manual Configuration").Translated);
            manualLink.Margin = new BorderDouble(0, 5);
            manualLink.Click += new ButtonBase.ButtonEventHandler(ManualLink_Click);

            container.AddChild(printerMessageOne);
            container.AddChild(printerMessageTwo);
            container.AddChild(printerMessageThree);
            container.AddChild(vSpacer);
            container.AddChild(manualLink);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;            
        }

        void ManualLink_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(MoveToManualConfiguration);
        }

        void MoveToManualConfiguration(object state)
        {
            Parent.AddChild(new SetupStepComPortManual((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
            Parent.RemoveChild(this);
        }

        void NextButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(MoveToNextWidget);
        }

        void MoveToNextWidget(object state)
        {
            Parent.AddChild(new SetupStepComPortTwo((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
            Parent.RemoveChild(this);
        }
    }
}
