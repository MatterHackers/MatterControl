/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

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
            ActivePrinterProfile.Instance.DoPrintLeveling = false;
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
            ActivePrinterProfile.Instance.SetPrintLevelingProbePositions(printLevelPositions3x3);

            ActivePrinterProfile.Instance.DoPrintLeveling = true;
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
		static string setZHeightCoarseInstruction1 = new LocalizedString("Using the [Z] controls on this screen, we will now take a coarse measurement of the extruder height at this position.").Translated;

		static string setZHeightCourseInstructTxtOne = new LocalizedString("Place the paper under the extruder").Translated;
		static string setZHeightCourseInstructTxtTwo = new LocalizedString("Using the above contols").Translated;
		static string setZHeightCourseInstructTxtThree = new LocalizedString("Press [Z-] until there is resistance to moving the paper").Translated;
		static string setZHeightCourseInstructTxtFour = new LocalizedString("Press [Z+] once to release the paper").Translated;
		static string setZHeightCourseInstructTxtFive = new LocalizedString("Finally click 'Next' to continue.").Translated;                                
		static string setZHeightCoarseInstruction2 = string.Format("\t• {0}\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}", setZHeightCourseInstructTxtOne, setZHeightCourseInstructTxtTwo, setZHeightCourseInstructTxtThree,setZHeightCourseInstructTxtFour, setZHeightCourseInstructTxtFive);
		            
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
		static string setZHeightFineInstruction1 = new LocalizedString("We will now refine our measurement of the extruder height at this position.").Translated;
		static string setZHeightFineInstructionTxtOne = new LocalizedString("Press [Z-] until there is resistance to moving the paper").Translated;
		static string setZHeightFineInstructionTxtTwo = new LocalizedString("Press [Z+] once to release the paper").Translated;
		static string setZHeightFineInstructionTxtThree = new LocalizedString("Finally click 'Next' to continue.").Translated;
		static string setZHeightFineInstruction2 = string.Format("\t• {0}\n\t• {1}\n\n{2}",setZHeightFineInstructionTxtOne, setZHeightFineInstructionTxtTwo, setZHeightFineInstructionTxtThree);

        public GetFineBedHeight(string instructionsText, ProbePosition whereToWriteProbePosition)
            : base(instructionsText, setZHeightFineInstruction1, setZHeightFineInstruction2, .1, whereToWriteProbePosition)
        {
        }
    }

    public class GetUltraFineBedHeight : FindBedHeight
    {
		static string setZHeightFineInstruction1 = new LocalizedString("We will now finalize our measurement of the extruder height at this position.").Translated;
		static string setHeightFineInstructionTxtOne = new LocalizedString("Press [Z-] one click PAST the first hint of resistance").Translated;
		static string setHeightFineInstructionTxtTwo = new LocalizedString("Finally click 'Next' to continue.").Translated;
		static string setZHeightFineInstruction2 = string.Format("\t• {0}\n\n\n{1}", setHeightFineInstructionTxtOne, setHeightFineInstructionTxtTwo);

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
		string pageOneInstructionsTxtOne = new LocalizedString("Welcome to the print leveling wizard. Here is a quick overview on what we are going to do.").Translated;
		string pageOneInstructionsTxtTwo = new LocalizedString("'Home' the printer").Translated;
		string pageOneInstructionsTxtThree = new LocalizedString("Sample the bed at three points").Translated;
		string pageOneInstructionsTxtFour = new LocalizedString("Turn auto leveling on").Translated;
		string pageOneInstructionsTxtFive = new LocalizedString("You should be done in about 3 minutes.").Translated;
		string pageOneInstructionsTxtSix = new LocalizedString("Click 'Next' to continue.").Translated;
		string pageOneInstructions;

		string homingPageInstructionsTxtOne = new LocalizedString("The printer should now be 'homing'. Once it is finished homing we will move it to the first point to sample.\n\nTo complete the next few steps you will need").Translated;
		string homingPageInstructionsTxtTwo = new LocalizedString("A standard sheet of paper").Translated;
		string homingPageInstructionsTxtThree = new LocalizedString("We will use this paper to measure the distance between the extruder and the bed.\n\nClick 'Next' to continue.").Translated;
		string homingPageInstructions;
		 
		string doneInstructionsTxt = new LocalizedString("Congratulations!\n\nAuto Print Leveling is now configured and enabled.").Translated;
		string doneInstructionsTxtTwo = new LocalizedString("Remove the paper").Translated;
		string doneInstructionsTxtThree = new LocalizedString("If in the future you wish to turn Auto Print Leveling off, you can uncheck the 'Enabled' button found in 'Advanced Settings'->'Printer Controls'.\n\nClick 'Done' to close this window.").Translated;
		string doneInstructions;

        WizardControl printLevelWizard;
        public PrintLevelWizardWindow()
            : base(500, 370)
		{	
			pageOneInstructions = string.Format("{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}\n\n{5}",pageOneInstructionsTxtOne, pageOneInstructionsTxtTwo, pageOneInstructionsTxtThree, pageOneInstructionsTxtFour, pageOneInstructionsTxtFive, pageOneInstructionsTxtSix);
			homingPageInstructions = string.Format("{0}:\n\n\t• {1}\n\n{2}", homingPageInstructionsTxtOne, homingPageInstructionsTxtTwo, homingPageInstructionsTxtThree);
			doneInstructions = string.Format("{0}\n\n\t• {1}\n\n{2}",doneInstructionsTxt, doneInstructionsTxtTwo, doneInstructionsTxtThree);


			string printLevelWizardTitle = new LocalizedString("MatterControl").Translated;
			string printLevelWizardTitleFull = new LocalizedString ("Print Leveling Wizard").Translated;
			Title = string.Format("{0} - {1}",printLevelWizardTitle, printLevelWizardTitleFull);
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

			string lowPrecisionPositionLbl = new LocalizedString ("Position").Translated;
			string lowPrecisionLbl = new LocalizedString ("Low Precision").Translated;
			GetCoarseBedHeight getCourseBedHeight = new GetCoarseBedHeight (printLevelWizard, 
				new Vector3 (probeBackCenter, 10), 
				string.Format ("{0} {1} 1 - {2}", Step (),lowPrecisionPositionLbl, lowPrecisionLbl),
				probePositions [0]);

			printLevelWizard.AddPage(getCourseBedHeight);
			string precisionPositionLbl = new LocalizedString("Position").Translated;
			string medPrecisionLbl = new LocalizedString("Medium Precision").Translated;
			printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} {1} 1 - {2}", Step(), precisionPositionLbl, medPrecisionLbl), probePositions[0]));
			string highPrecisionLbl = new LocalizedString("High Precision").Translated;
			printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} {1} 1 - {2}", Step(), precisionPositionLbl, highPrecisionLbl), probePositions[0]));

            Vector2 probeFrontLeft = ActiveSliceSettings.Instance.GetPrintLevelSamplePosition(1);
			string positionLblTwo = new LocalizedString("Position").Translated;
			string lowPrecisionTwoLbl = new LocalizedString("Low Precision").Translated;
			string medPrecisionTwoLbl = new LocalizedString("Medium Precision").Translated;
			string highPrecisionTwoLbl = new LocalizedString("High Precision").Translated;
			printLevelWizard.AddPage(new GetCoarseBedHeight(printLevelWizard, new Vector3(probeFrontLeft, 10), string.Format("{0} {1} 2 - {2}", Step(), positionLblTwo, lowPrecisionTwoLbl  ), probePositions[1]));
			printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} {1} 2 - {2}", Step(), positionLblTwo,medPrecisionTwoLbl), probePositions[1]));
			printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} {1} 2 - {2}", Step(), positionLblTwo,highPrecisionTwoLbl), probePositions[1]));

            Vector2 probeFrontRight = ActiveSliceSettings.Instance.GetPrintLevelSamplePosition(2);
			string positionLabelThree = new LocalizedString("Position").Translated;
			string lowPrecisionLblThree = new LocalizedString("Low Precision").Translated;
			string medPrecisionLblThree = new LocalizedString("Medium Precision").Translated;
			string highPrecisionLblThree = new LocalizedString("High Precision").Translated;
			printLevelWizard.AddPage(new GetCoarseBedHeight(printLevelWizard, new Vector3(probeFrontRight, 10), string.Format("{0} {1} 3 - {2}", Step(), positionLabelThree, lowPrecisionLblThree), probePositions[2]));
			printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} {1} 3 - {2}", Step(),positionLabelThree, medPrecisionLblThree ), probePositions[2]));
			printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} {1} 3 - {2}", Step(), positionLabelThree, highPrecisionLblThree ), probePositions[2]));

            printLevelWizard.AddPage(new LastPageInstructions(doneInstructions, probePositions));
        }

        int step = 1;
		string stepTextBeg = new LocalizedString("Step").Translated;
		string stepTextEnd = new LocalizedString("of").Translated;
        string Step()
        {
			return string.Format("{0} {1} {2} 9:",stepTextBeg, step++, stepTextEnd);
        }

        void DoneButton_Click(object sender, MouseEventArgs mouseEvent)
		{
			UiThread.RunOnIdle (DoDoneButton_Click);
		}

        void DoDoneButton_Click(object state)
        {
            Close();
        }
    }
}
