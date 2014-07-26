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
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
    public class FirstPageInstructions : InstructionsPage
    {
        public FirstPageInstructions(string pageDescription, string instructionsText)
            : base(pageDescription, instructionsText)
        {
        }

        public override void PageIsBecomingActive()
        {
            ActivePrinterProfile.Instance.DoPrintLeveling = false;
            base.PageIsBecomingActive();
        }
    }

    public class LastPage3PointInstructions : InstructionsPage
    {
        ProbePosition[] probePositions = new ProbePosition[3];

        public LastPage3PointInstructions(string pageDescription, string instructionsText, ProbePosition[] probePositions)
            : base(pageDescription, instructionsText)
        {
            this.probePositions = probePositions;
        }

        public override void PageIsBecomingActive()
        {
            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
            levelingData.sampledPosition0 = probePositions[0].position - paperWidth;
            levelingData.sampledPosition1 = probePositions[1].position - paperWidth;
            levelingData.sampledPosition2 = probePositions[2].position - paperWidth;

            ActivePrinterProfile.Instance.DoPrintLeveling = true;
            base.PageIsBecomingActive();
        }
    }

    public class GettingThirdPointFor2PointCalibration : InstructionsPage
    {
        protected Vector3 probeStartPosition;
        ProbePosition probePosition;
        protected WizardControl container;

        public GettingThirdPointFor2PointCalibration(WizardControl container, string pageDescription, Vector3 probeStartPosition, string instructionsText, ProbePosition probePosition)
            : base(pageDescription, instructionsText)
        {
            this.probeStartPosition = probeStartPosition;
            this.probePosition = probePosition;
            this.container = container;
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

        public override void PageIsBecomingActive()
        {
            // first make sure there is no leftover FinishedProbe event
            PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(FinishedProbe, ref unregisterEvents);

            PrinterConnectionAndCommunication.Instance.MoveAbsolute(PrinterConnectionAndCommunication.Axis.Z, probeStartPosition.z, InstructionsPage.ManualControlsFeedRate().z);
            PrinterConnectionAndCommunication.Instance.MoveAbsolute(probeStartPosition, InstructionsPage.ManualControlsFeedRate().x);
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G30");
            PrinterConnectionAndCommunication.Instance.ReadLine.RegisterEvent(FinishedProbe, ref unregisterEvents);

            base.PageIsBecomingActive();

            container.nextButton.Enabled = false;
            container.backButton.Enabled = false;
        }

        void FinishedProbe(object sender, EventArgs e)
        {
            StringEventArgs currentEvent = e as StringEventArgs;
            if (currentEvent != null)
            {
                if (currentEvent.Data.Contains("endstops hit"))
                {
                    PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(FinishedProbe, ref unregisterEvents);
                    int zStringPos = currentEvent.Data.LastIndexOf("Z:");
                    string zProbeHeight = currentEvent.Data.Substring(zStringPos + 2);
                    probePosition.position = new Vector3(probeStartPosition.x, probeStartPosition.y, double.Parse(zProbeHeight));
                    PrinterConnectionAndCommunication.Instance.MoveAbsolute(probeStartPosition, InstructionsPage.ManualControlsFeedRate().z);
                    PrinterConnectionAndCommunication.Instance.ReadPosition();

                    container.nextButton.ClickButton(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                }
            }
        }
    }

    public class LastPage2PointInstructions : InstructionsPage
    {
        ProbePosition[] probePositions = new ProbePosition[5];

        public LastPage2PointInstructions(string pageDescription, string instructionsText, ProbePosition[] probePositions)
            : base(pageDescription, instructionsText)
        {
            this.probePositions = probePositions;
        }

        public override void PageIsBecomingActive()
        {
            // This data is currently the offset from the probe to the extruder tip. We need to translate them
            // into bed offsets and store them.

            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
            // The first point is the user assisted offset to the bed
            Vector3 userBedSample0 = probePositions[0].position;
            // The first point sample offset at the limit switch
            Vector3 probeOffset0 = probePositions[1].position; // this z should be 0

            // right side of printer
            Vector3 userBedSample1 = probePositions[2].position;
            Vector3 probeOffset1 = probePositions[3].position;

            // auto back probe
            Vector3 probeOffset2 = probePositions[4].position;

            levelingData.sampledPosition0 = userBedSample0 - paperWidth;
            levelingData.sampledPosition1 = userBedSample1 - paperWidth;
            levelingData.sampledPosition2 = probeOffset2 - probeOffset0 + userBedSample0 - paperWidth;

            levelingData.probeOffset0 = probeOffset0 - paperWidth;
            levelingData.probeOffset1 = probeOffset1 - paperWidth;

            ActivePrinterProfile.Instance.DoPrintLeveling = true;
            base.PageIsBecomingActive();
        }
    }

    public class HomePrinterPage : InstructionsPage
    {
        public HomePrinterPage(string pageDescription, string instructionsText)
            : base(pageDescription, instructionsText)
        {
        }

        public override void PageIsBecomingActive()
        {
            PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.XYZ);
            base.PageIsBecomingActive();
        }
    }

    public class FindBedHeight : InstructionsPage
    {
        Vector3 lastReportedPosition;
        ProbePosition probePosition;
        double moveAmount;
        bool allowLessThan0;

        protected JogControls.MoveButton zPlusControl;
        protected JogControls.MoveButton zMinusControl;

        public FindBedHeight(string pageDescription, string setZHeightCoarseInstruction1, string setZHeightCoarseInstruction2, double moveDistance, ProbePosition whereToWriteProbePosition, bool allowLessThan0)
            : base(pageDescription, setZHeightCoarseInstruction1)
        {
            this.allowLessThan0 = allowLessThan0;
            this.moveAmount = moveDistance;
            this.lastReportedPosition = PrinterConnectionAndCommunication.Instance.LastReportedPosition;
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

        public override void PageIsBecomingActive()
        {
            // always make sure we don't have print leveling turned on
            ActivePrinterProfile.Instance.DoPrintLeveling = false;

            base.PageIsBecomingActive();
        }

        public override void PageIsBecomingInactive()
        {
            probePosition.position = PrinterConnectionAndCommunication.Instance.LastReportedPosition;
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

        static string zIsTooLowMessage = "You cannot move any lower. This position on your bed is too low for the extruder to reach. You need to raise your bed, or adjust your limits to allow the extruder to go lower.".Localize();
        static string zTooLowTitle = "Waring Moving Too Low".Localize();
        void zMinusControl_Click(object sender, MouseEventArgs mouseEvent)
        {
            if (!allowLessThan0
                && PrinterConnectionAndCommunication.Instance.LastReportedPosition.z - moveAmount < 0)
            {
                UiThread.RunOnIdle( (state) => 
                {
                    StyledMessageBox.ShowMessageBox(zIsTooLowMessage, zTooLowTitle, StyledMessageBox.MessageType.OK);
                });
                // don't move the bed lower it will not work when we print.
                return;
            }
            PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Z, -moveAmount, InstructionsPage.ManualControlsFeedRate().z);
            PrinterConnectionAndCommunication.Instance.ReadPosition();
        }

        void zPlusControl_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Z, moveAmount, InstructionsPage.ManualControlsFeedRate().z);
            PrinterConnectionAndCommunication.Instance.ReadPosition();
        }
    }

    public class GetCoarseBedHeight : FindBedHeight
    {
		static string setZHeightCoarseInstruction1 = LocalizedString.Get("Using the [Z] controls on this screen, we will now take a coarse measurement of the extruder height at this position.");

		static string setZHeightCourseInstructTextOne = "Place the paper under the extruder".Localize();
        static string setZHeightCourseInstructTextTwo = "Using the above contols".Localize();
		static string setZHeightCourseInstructTextThree = LocalizedString.Get("Press [Z-] until there is resistance to moving the paper");
		static string setZHeightCourseInstructTextFour = LocalizedString.Get("Press [Z+] once to release the paper");
		static string setZHeightCourseInstructTextFive = LocalizedString.Get("Finally click 'Next' to continue.");                                
		static string setZHeightCoarseInstruction2 = string.Format("\t• {0}\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}", setZHeightCourseInstructTextOne, setZHeightCourseInstructTextTwo, setZHeightCourseInstructTextThree,setZHeightCourseInstructTextFour, setZHeightCourseInstructTextFive);
		            
        protected Vector3 probeStartPosition;
        protected WizardControl container;

        public GetCoarseBedHeight(WizardControl container, Vector3 probeStartPosition, string pageDescription, ProbePosition whereToWriteProbePosition, bool allowLessThan0)
            : base(pageDescription, setZHeightCoarseInstruction1, setZHeightCoarseInstruction2, 1, whereToWriteProbePosition, allowLessThan0)
        {
            this.container = container;
            this.probeStartPosition = probeStartPosition;
        }

        public override void PageIsBecomingActive()
        {
            base.PageIsBecomingActive();

            PrinterConnectionAndCommunication.Instance.MoveAbsolute(PrinterConnectionAndCommunication.Axis.Z, probeStartPosition.z, InstructionsPage.ManualControlsFeedRate().z);
            PrinterConnectionAndCommunication.Instance.MoveAbsolute(probeStartPosition, InstructionsPage.ManualControlsFeedRate().x);
            PrinterConnectionAndCommunication.Instance.ReadPosition();

            container.nextButton.Enabled = false;

            zPlusControl.Click += new ButtonBase.ButtonEventHandler(zControl_Click);
            zMinusControl.Click += new ButtonBase.ButtonEventHandler(zControl_Click);
        }

        protected void zControl_Click(object sender, MouseEventArgs mouseEvent)
        {
            container.nextButton.Enabled = true;
        }

        public override void PageIsBecomingInactive()
        {
            container.nextButton.Enabled = true;
        }
    }

    public class GetCoarseBedHeightProbeFirst : GetCoarseBedHeight
    {
        event EventHandler unregisterEvents;
        ProbePosition whereToWriteSamplePosition;

        public GetCoarseBedHeightProbeFirst(WizardControl container, Vector3 probeStartPosition, string pageDescription, ProbePosition whereToWriteProbePosition, ProbePosition whereToWriteSamplePosition, bool allowLessThan0)
            : base(container, probeStartPosition, pageDescription, whereToWriteProbePosition, allowLessThan0)
        {
            this.whereToWriteSamplePosition = whereToWriteSamplePosition;
        }

        public override void PageIsBecomingActive()
        {
            // first make sure there is no leftover FinishedProbe event
            PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(FinishedProbe, ref unregisterEvents);

            PrinterConnectionAndCommunication.Instance.MoveAbsolute(PrinterConnectionAndCommunication.Axis.Z, probeStartPosition.z, InstructionsPage.ManualControlsFeedRate().z);
            PrinterConnectionAndCommunication.Instance.MoveAbsolute(probeStartPosition, InstructionsPage.ManualControlsFeedRate().x);
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G30");
            PrinterConnectionAndCommunication.Instance.ReadLine.RegisterEvent(FinishedProbe, ref unregisterEvents);

            base.PageIsBecomingActive();

            container.nextButton.Enabled = false;

            zPlusControl.Click += new ButtonBase.ButtonEventHandler(zControl_Click);
            zMinusControl.Click += new ButtonBase.ButtonEventHandler(zControl_Click);
        }

        void FinishedProbe(object sender, EventArgs e)
        {
            StringEventArgs currentEvent = e as StringEventArgs;
            if (currentEvent != null)
            {
                if (currentEvent.Data.Contains("endstops hit"))
                {
                    PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(FinishedProbe, ref unregisterEvents);
                    int zStringPos = currentEvent.Data.LastIndexOf("Z:");
                    string zProbeHeight = currentEvent.Data.Substring(zStringPos + 2);
                    // store the position that the limit swich fires
                    whereToWriteSamplePosition.position = new Vector3(probeStartPosition.x, probeStartPosition.y, double.Parse(zProbeHeight));

                    // now move to the probe start position
                    PrinterConnectionAndCommunication.Instance.MoveAbsolute(probeStartPosition, InstructionsPage.ManualControlsFeedRate().z);
                    PrinterConnectionAndCommunication.Instance.ReadPosition();
                }
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
    }

    public class GetFineBedHeight : FindBedHeight
    {
		static string setZHeightFineInstruction1 = LocalizedString.Get("We will now refine our measurement of the extruder height at this position.");
		static string setZHeightFineInstructionTextOne = LocalizedString.Get("Press [Z-] until there is resistance to moving the paper");
		static string setZHeightFineInstructionTextTwo = LocalizedString.Get("Press [Z+] once to release the paper");
		static string setZHeightFineInstructionTextThree = LocalizedString.Get("Finally click 'Next' to continue.");
		static string setZHeightFineInstruction2 = string.Format("\t• {0}\n\t• {1}\n\n{2}",setZHeightFineInstructionTextOne, setZHeightFineInstructionTextTwo, setZHeightFineInstructionTextThree);

        public GetFineBedHeight(string pageDescription, ProbePosition whereToWriteProbePosition, bool allowLessThan0)
            : base(pageDescription, setZHeightFineInstruction1, setZHeightFineInstruction2, .1, whereToWriteProbePosition, allowLessThan0)
        {
        }
    }

    public class GetUltraFineBedHeight : FindBedHeight
    {
		static string setZHeightFineInstruction1 = LocalizedString.Get("We will now finalize our measurement of the extruder height at this position.");
		static string setHeightFineInstructionTextOne = LocalizedString.Get("Press [Z-] one click PAST the first hint of resistance");
		static string setHeightFineInstructionTextTwo = LocalizedString.Get("Finally click 'Next' to continue.");
		static string setZHeightFineInstruction2 = string.Format("\t• {0}\n\n\n{1}", setHeightFineInstructionTextOne, setHeightFineInstructionTextTwo);

        public GetUltraFineBedHeight(string pageDescription, ProbePosition whereToWriteProbePosition, bool allowLessThan0)
            : base(pageDescription, setZHeightFineInstruction1, setZHeightFineInstruction2, .02, whereToWriteProbePosition, allowLessThan0)
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
                PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Z, 2, InstructionsPage.ManualControlsFeedRate().z);
            }
            base.PageIsBecomingInactive();
        }
    }
}
