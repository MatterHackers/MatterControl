/*
Copyright (c) 2013, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Font;

namespace MatterHackers.MatterControl
{
    public class ProbePosition
    {
        public Vector3 position;
    }

    public class InstructionsPage : WizardPage
    {
        protected FlowLayoutWidget topToBottomControls;

        public InstructionsPage(string instructionsText)
        {
            topToBottomControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomControls.HAnchor |= Agg.UI.HAnchor.ParentCenter;
            topToBottomControls.VAnchor |= Agg.UI.VAnchor.ParentTop;

            AddTextField(instructionsText, 10);

            AddChild(topToBottomControls);

            AnchorAll();
        }

        public void AddTextField(string instructionsText, int pixelsFromLast)
        {
            GuiWidget spacer = new GuiWidget(10, pixelsFromLast);
            topToBottomControls.AddChild(spacer);

            string wrappedInstructions = TypeFacePrinter.InsertCRs(instructionsText, 400, 12);
            string wrappedInstructionsTabsToSpaces = wrappedInstructions.Replace("\t", "    ");
            TextWidget instructionsWidget = new TextWidget(wrappedInstructionsTabsToSpaces, textColor: RGBA_Bytes.White);
            instructionsWidget.HAnchor = Agg.UI.HAnchor.ParentCenter;
            topToBottomControls.AddChild(instructionsWidget);
        }
    }

    public class FirstPageInstructions : InstructionsPage
    {
        public FirstPageInstructions(string instructionsText)
            : base(instructionsText)
        {
        }

        public override void PageIsBecomingActive()
        {
            PrinterCommunication.Instance.DoPrintLeveling = false;
            base.PageIsBecomingActive();
        }
    }

    public class LastPageInstructions : InstructionsPage
    {
        ProbePosition[] probePositions = new ProbePosition[3];

        public LastPageInstructions(string instructionsText, ProbePosition[] probePositions)
            : base(instructionsText)
        {
            this.probePositions = probePositions;
        }

        public override void PageIsBecomingActive()
        {
            double[] printLevelPositions3x3 =  
            {
                probePositions[0].position.x, probePositions[0].position.y, probePositions[0].position.z, 
                probePositions[1].position.x, probePositions[1].position.y, probePositions[1].position.z, 
                probePositions[2].position.x, probePositions[2].position.y, probePositions[2].position.z, 
            };
            PrinterCommunication.Instance.SetPrintLevelingProbePositions(printLevelPositions3x3);

            PrinterCommunication.Instance.DoPrintLeveling = true;
            base.PageIsBecomingActive();
        }
    }

    public class HomePrinterPage : InstructionsPage
    {
        public HomePrinterPage(string instructionsText)
            : base(instructionsText)
        {
        }

        public override void PageIsBecomingActive()
        {
            PrinterCommunication.Instance.HomeAxis(PrinterCommunication.Axis.XYZ);
            base.PageIsBecomingActive();
        }
    }

    public class FindBedHeight : InstructionsPage
    {
        Vector3 lastReportedPosition;
        ProbePosition probePosition;
        double moveAmount;

        protected JogControls.MoveButton zPlusControl;
        protected JogControls.MoveButton zMinusControl;

        public FindBedHeight(string instructionsText, string setZHeightCoarseInstruction1, string setZHeightCoarseInstruction2, double moveDistance, ProbePosition whereToWriteProbePosition)
            : base(instructionsText + "\n\n" + setZHeightCoarseInstruction1)
        {
            this.moveAmount = moveDistance;
            this.lastReportedPosition = PrinterCommunication.Instance.LastReportedPosition;
            this.probePosition = whereToWriteProbePosition;

            GuiWidget spacer = new GuiWidget(15, 15);
            topToBottomControls.AddChild(spacer);

            FlowLayoutWidget zButtonsAndInfo = new FlowLayoutWidget();
            zButtonsAndInfo.HAnchor |= Agg.UI.HAnchor.ParentCenter;
            FlowLayoutWidget zButtons = CreateZButtons();
            zButtonsAndInfo.AddChild(zButtons);

            zButtonsAndInfo.AddChild(new GuiWidget(15, 10));

            FlowLayoutWidget textFields = new FlowLayoutWidget(FlowDirection.TopToBottom);
           
            zButtonsAndInfo.AddChild(textFields);

            topToBottomControls.AddChild(zButtonsAndInfo);

            AddTextField(setZHeightCoarseInstruction2, 10);
        }

        event EventHandler unregisterEvents;
        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public override void PageIsBecomingInactive()
        {
            probePosition.position = PrinterCommunication.Instance.LastReportedPosition;
            base.PageIsBecomingInactive();
        }

        private FlowLayoutWidget CreateZButtons()
        {
            FlowLayoutWidget zButtons = JogControls.CreateZButtons(RGBA_Bytes.White, 4, out zPlusControl, out zMinusControl);
            // set these to 0 so the button does not do any movements by default (we will handle the movement on our click callback)
            zPlusControl.MoveAmount = 0;
            zMinusControl.MoveAmount = 0;
            zPlusControl.Click += new ButtonBase.ButtonEventHandler(zPlusControl_Click);
            zMinusControl.Click += new ButtonBase.ButtonEventHandler(zMinusControl_Click);
            return zButtons;
        }

        void zMinusControl_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.MoveRelative(PrinterCommunication.Axis.Z, -moveAmount, 1000);
            PrinterCommunication.Instance.ReadPosition();
        }

        void zPlusControl_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.MoveRelative(PrinterCommunication.Axis.Z, moveAmount, 1000);
            PrinterCommunication.Instance.ReadPosition();
        }
    }

    public class GetCoarseBedHeight : FindBedHeight
    {
        static string setZHeightCoarseInstruction1 = "Using the [Z] controls on this screen, we will now take a coarse measurement of the extruder height at this position.";
        static string setZHeightCoarseInstruction2 = "\t• Place the paper under the extruder\n\t• Using the above contols\n\t• Press [Z-] until there is resistance to moving the paper\n\t• Press [Z+] once to release the paper\n\nFinally click 'Next' to continue.";
        Vector3 probeStartPosition;
        WizardControl container;

        public GetCoarseBedHeight(WizardControl container, Vector3 probeStartPosition, string instructionsText, ProbePosition whereToWriteProbePosition)
            : base(instructionsText, setZHeightCoarseInstruction1, setZHeightCoarseInstruction2, 1, whereToWriteProbePosition)
        {
            this.container = container;
            this.probeStartPosition = probeStartPosition;
        }

        public override void PageIsBecomingActive()
        {
            double feedRate = 4000;
            PrinterCommunication.Instance.MoveAbsolute(PrinterCommunication.Axis.Z, probeStartPosition.z, feedRate);
            PrinterCommunication.Instance.MoveAbsolute(probeStartPosition, feedRate);
            base.PageIsBecomingActive();
            PrinterCommunication.Instance.ReadPosition();

            container.nextButton.Enabled = false;

            zPlusControl.Click += new ButtonBase.ButtonEventHandler(zControl_Click);
            zMinusControl.Click += new ButtonBase.ButtonEventHandler(zControl_Click);
        }

        void zControl_Click(object sender, MouseEventArgs mouseEvent)
        {
            container.nextButton.Enabled = true;
        }

        public override void PageIsBecomingInactive()
        {
            container.nextButton.Enabled = true;
        }
    }

    public class GetFineBedHeight : FindBedHeight
    {
        static string setZHeightFineInstruction1 = "We will now refine our measurement of the extruder height at this position.";
        static string setZHeightFineInstruction2 = "\t• Press [Z-] until there is resistance to moving the paper\n\t• Press [Z+] once to release the paper\n\nFinally click 'Next' to continue.";

        public GetFineBedHeight(string instructionsText, ProbePosition whereToWriteProbePosition)
            : base(instructionsText, setZHeightFineInstruction1, setZHeightFineInstruction2, .1, whereToWriteProbePosition)
        {
        }
    }

    public class GetUltraFineBedHeight : FindBedHeight
    {
        static string setZHeightFineInstruction1 = "We will now finalize our measurement of the extruder height at this position.";
        static string setZHeightFineInstruction2 = "\t• Press [Z-] one click PAST the first hint of resistance\n\n\nFinally click 'Next' to continue.";

        public GetUltraFineBedHeight(string instructionsText, ProbePosition whereToWriteProbePosition)
            : base(instructionsText, setZHeightFineInstruction1, setZHeightFineInstruction2, .02, whereToWriteProbePosition)
        {
        }

        bool haveDrawn = false;
        public override void OnDraw(Graphics2D graphics2D)
        {
            haveDrawn = true;
            base.OnDraw(graphics2D);
        }

        public override void PageIsBecomingInactive()
        {
            if (haveDrawn)
            {
                PrinterCommunication.Instance.MoveRelative(PrinterCommunication.Axis.Z, 1, 1000);
            }
            base.PageIsBecomingInactive();
        }
    }

    // disabled style
    // check box control

    public class PrintLevelWizardWindow : SystemWindow
    {
        string pageOneInstructions = "Welcome to the print leveling wizard. Here is a quick overview on what we are going to do.\n\n\t• 'Home' the printer\n\t• Sample the bed at three points\n\t• Turn auto leveling on\n\nYou should be done in about 3 minutes.\n\nClick 'Next' to continue.";

        string homingPageInstructions = "The printer should now be 'homing'. Once it is finished homing we will move it to the first point to sample.\n\nTo complete the next few steps you will need:\n\n\t• A standard sheet of paper\n\nWe will use this paper to measure the distance between the extruder and the bed.\n\nClick 'Next' to continue.";

        string doneInstructions = "Congratulations!\n\nAuto Print Leveling is now configured and enabled.\n\n\t• Remove the paper\n\nIf in the future you wish to turn Auto Print Leveling off, you can uncheck the 'Enabled' button found in 'Advanced Settings'->'Printer Controls'.\n\nClick 'Done' to close this window.";

        WizardControl printLevelWizard;
        public PrintLevelWizardWindow()
            : base(500, 370)
        {
            Title = "MatterControl - Print Leveling Wizard";
            ProbePosition[] probePositions = new ProbePosition[3];
            probePositions[0] = new ProbePosition();
            probePositions[1] = new ProbePosition();
            probePositions[2] = new ProbePosition();

            printLevelWizard = new WizardControl();
            printLevelWizard.DoneButton.Click += new ButtonBase.ButtonEventHandler(DoneButton_Click);
            AddChild(printLevelWizard);

            printLevelWizard.AddPage(new FirstPageInstructions(pageOneInstructions));
            printLevelWizard.AddPage(new HomePrinterPage(homingPageInstructions));

            Vector2 probeBackCenter = ActiveSliceSettings.Instance.GetPrintLevelSamplePosition(0);
            printLevelWizard.AddPage(new GetCoarseBedHeight(printLevelWizard, new Vector3(probeBackCenter, 10), string.Format("{0} Position 1 - Low Precision", Step()), probePositions[0]));
            printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} Position 1 - Medium Precision", Step()), probePositions[0]));
            printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} Position 1 - High Precision", Step()), probePositions[0]));

            Vector2 probeFrontLeft = ActiveSliceSettings.Instance.GetPrintLevelSamplePosition(1);
            printLevelWizard.AddPage(new GetCoarseBedHeight(printLevelWizard, new Vector3(probeFrontLeft, 10), string.Format("{0} Position 2 - Low Precision", Step()), probePositions[1]));
            printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} Position 2 - Medium Precision", Step()), probePositions[1]));
            printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} Position 2 - High Precision", Step()), probePositions[1]));

            Vector2 probeFrontRight = ActiveSliceSettings.Instance.GetPrintLevelSamplePosition(2);
            printLevelWizard.AddPage(new GetCoarseBedHeight(printLevelWizard, new Vector3(probeFrontRight, 10), string.Format("{0} Position 3 - Low Precision", Step()), probePositions[2]));
            printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} Position 3 - Medium Precision", Step()), probePositions[2]));
            printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} Position 3 - High Precision", Step()), probePositions[2]));

            printLevelWizard.AddPage(new LastPageInstructions(doneInstructions, probePositions));
        }

        int step = 1;
        string Step()
        {
            return string.Format("Step {0} of 9:", step++);
        }

        void DoneButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            Close();
        }
    }
}
