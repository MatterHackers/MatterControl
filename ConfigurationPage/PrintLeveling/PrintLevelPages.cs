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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class FirstPageInstructions : InstructionsPage
	{
		public FirstPageInstructions(string pageDescription, string instructionsText)
			: base(pageDescription, instructionsText)
		{
		}
	}

	public class LastPage3PointInstructions : InstructionsPage
	{
		private List<ProbePosition> probePositions = new List<ProbePosition>(3)
		{
			new ProbePosition(),new ProbePosition(),new ProbePosition()
		};

		public LastPage3PointInstructions(string pageDescription, string instructionsText, List<ProbePosition> probePositions)
			: base(pageDescription, instructionsText)
		{
			this.probePositions = probePositions;
		}

		public override void PageIsBecomingActive()
		{
			Vector3 paperWidth = new Vector3(0, 0, ActiveSliceSettings.Instance.GetValue<double>("manual_probe_paper_width"));

			PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
			levelingData.SampledPositions.Clear();
			levelingData.SampledPositions.Add(probePositions[0].position - paperWidth);
			levelingData.SampledPositions.Add(probePositions[1].position - paperWidth);
			levelingData.SampledPositions.Add(probePositions[2].position - paperWidth);

			// Invoke setter forcing persistence of leveling data
			ActiveSliceSettings.Instance.Helpers.SetPrintLevelingData(levelingData);
			ActiveSliceSettings.Instance.Helpers.DoPrintLeveling ( true);

			base.PageIsBecomingActive();
		}
	}

	public class LastPageRadialInstructions : InstructionsPage
	{
		private List<ProbePosition> probePositions;

		public LastPageRadialInstructions(string pageDescription, string instructionsText, List<ProbePosition> probePositions)
			: base(pageDescription, instructionsText)
		{
			this.probePositions = probePositions;
		}

		public override void PageIsBecomingActive()
		{
			PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
			levelingData.SampledPositions.Clear();

			Vector3 paperWidth = new Vector3(0, 0, ActiveSliceSettings.Instance.GetValue<double>("manual_probe_paper_width"));
			for (int i = 0; i < probePositions.Count; i++)
			{
				levelingData.SampledPositions.Add(probePositions[i].position - paperWidth);
			}

			// Invoke setter forcing persistence of leveling data
			ActiveSliceSettings.Instance.Helpers.SetPrintLevelingData(levelingData);
			ActiveSliceSettings.Instance.Helpers.DoPrintLeveling ( true);

			base.PageIsBecomingActive();
		}
	}

	public class GettingThirdPointFor2PointCalibration : InstructionsPage
	{
		protected Vector3 probeStartPosition;
		private ProbePosition probePosition;
		protected WizardControl container;

		public GettingThirdPointFor2PointCalibration(WizardControl container, string pageDescription, Vector3 probeStartPosition, string instructionsText, ProbePosition probePosition)
			: base(pageDescription, instructionsText)
		{
			this.probeStartPosition = probeStartPosition;
			this.probePosition = probePosition;
			this.container = container;
		}

		private EventHandler unregisterEvents;

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);

			base.OnClosed(e);
		}

		public override void PageIsBecomingActive()
		{
			// first make sure there is no leftover FinishedProbe event
			PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(FinishedProbe, ref unregisterEvents);

			var feedRates = ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds();

			PrinterConnectionAndCommunication.Instance.MoveAbsolute(PrinterConnectionAndCommunication.Axis.Z, probeStartPosition.z, feedRates.z);
			PrinterConnectionAndCommunication.Instance.MoveAbsolute(probeStartPosition, feedRates.x);
			PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G30");
			PrinterConnectionAndCommunication.Instance.ReadLine.RegisterEvent(FinishedProbe, ref unregisterEvents);

			base.PageIsBecomingActive();

			container.nextButton.Enabled = false;
			container.backButton.Enabled = false;
		}

		private void FinishedProbe(object sender, EventArgs e)
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
					PrinterConnectionAndCommunication.Instance.MoveAbsolute(probeStartPosition, ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds().z);
					PrinterConnectionAndCommunication.Instance.ReadPosition();

					container.nextButton.ClickButton(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
				}
			}
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
		private Vector3 lastReportedPosition;
		private List<ProbePosition> probePositions;
		int probePositionsBeingEditedIndex;
		private double moveAmount;
		private bool allowLessThan0;

		protected JogControls.MoveButton zPlusControl;
		protected JogControls.MoveButton zMinusControl;

		public FindBedHeight(string pageDescription, string setZHeightCoarseInstruction1, string setZHeightCoarseInstruction2, double moveDistance, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex, bool allowLessThan0)
			: base(pageDescription, setZHeightCoarseInstruction1)
		{
			this.allowLessThan0 = allowLessThan0;
			this.probePositions = probePositions;
			this.moveAmount = moveDistance;
			this.lastReportedPosition = PrinterConnectionAndCommunication.Instance.LastReportedPosition;
			this.probePositionsBeingEditedIndex = probePositionsBeingEditedIndex;

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

		private EventHandler unregisterEvents;

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
			ActiveSliceSettings.Instance.Helpers.DoPrintLeveling(false);

			base.PageIsBecomingActive();
		}

		public override void PageIsBecomingInactive()
		{
			probePositions[probePositionsBeingEditedIndex].position = PrinterConnectionAndCommunication.Instance.LastReportedPosition;
			base.PageIsBecomingInactive();
		}

		private FlowLayoutWidget CreateZButtons()
		{
			FlowLayoutWidget zButtons = JogControls.CreateZButtons(RGBA_Bytes.White, 4, out zPlusControl, out zMinusControl, true);
			// set these to 0 so the button does not do any movements by default (we will handle the movement on our click callback)
			zPlusControl.MoveAmount = 0;
			zMinusControl.MoveAmount = 0;
			zPlusControl.Click += zPlusControl_Click;
			zMinusControl.Click += zMinusControl_Click;
			return zButtons;
		}

		private static string zIsTooLowMessage = "You cannot move any lower. This position on your bed is too low for the extruder to reach. You need to raise your bed, or adjust your limits to allow the extruder to go lower.".Localize();
		private static string zTooLowTitle = "Warning - Moving Too Low".Localize();

		private void zMinusControl_Click(object sender, EventArgs mouseEvent)
		{
			if (!allowLessThan0
				&& PrinterConnectionAndCommunication.Instance.LastReportedPosition.z - moveAmount < 0)
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(null, zIsTooLowMessage, zTooLowTitle, StyledMessageBox.MessageType.OK);
				});
				// don't move the bed lower it will not work when we print.
				return;
			}

			PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Z, -moveAmount, ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds().z);
			PrinterConnectionAndCommunication.Instance.ReadPosition();
		}

		private void zPlusControl_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Z, moveAmount, ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds().z);
			PrinterConnectionAndCommunication.Instance.ReadPosition();
		}
	}

	public class GetCoarseBedHeight : FindBedHeight
	{
		private static string setZHeightCoarseInstruction1 = "Using the [Z] controls on this screen, we will now take a coarse measurement of the extruder height at this position.".Localize();

		private static string setZHeightCourseInstructTextOne = "Place the paper under the extruder".Localize();
		private static string setZHeightCourseInstructTextTwo = "Using the above controls".Localize();
		private static string setZHeightCourseInstructTextThree = "Press [Z-] until there is resistance to moving the paper".Localize();
		private static string setZHeightCourseInstructTextFour = "Press [Z+] once to release the paper".Localize();
		private static string setZHeightCourseInstructTextFive = "Finally click 'Next' to continue.".Localize();
		private static string setZHeightCoarseInstruction2 = string.Format("\t• {0}\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}", setZHeightCourseInstructTextOne, setZHeightCourseInstructTextTwo, setZHeightCourseInstructTextThree, setZHeightCourseInstructTextFour, setZHeightCourseInstructTextFive);

		protected Vector3 probeStartPosition;
		protected WizardControl container;

		public GetCoarseBedHeight(WizardControl container, Vector3 probeStartPosition, string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex, bool allowLessThan0)
			: base(pageDescription, setZHeightCoarseInstruction1, setZHeightCoarseInstruction2, 1, probePositions, probePositionsBeingEditedIndex, allowLessThan0)
		{
			this.container = container;
			this.probeStartPosition = probeStartPosition;
		}

		public override void PageIsBecomingActive()
		{
			base.PageIsBecomingActive();

			var feedRates = ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds();

			PrinterConnectionAndCommunication.Instance.MoveAbsolute(PrinterConnectionAndCommunication.Axis.Z, probeStartPosition.z, feedRates.z);
			PrinterConnectionAndCommunication.Instance.MoveAbsolute(probeStartPosition, feedRates.x);
			PrinterConnectionAndCommunication.Instance.ReadPosition();

			container.backButton.Enabled = false;
			container.nextButton.Enabled = false;

			zPlusControl.Click += zControl_Click;
			zMinusControl.Click += zControl_Click;
		}

		protected void zControl_Click(object sender, EventArgs mouseEvent)
		{
			container.nextButton.Enabled = true;
		}

		public override void PageIsBecomingInactive()
		{
			container.backButton.Enabled = true;
			container.nextButton.Enabled = true;
		}
	}

	public class GetFineBedHeight : FindBedHeight
	{
		private static string setZHeightFineInstruction1 = "We will now refine our measurement of the extruder height at this position.".Localize();
		private static string setZHeightFineInstructionTextOne = "Press [Z-] until there is resistance to moving the paper".Localize();
		private static string setZHeightFineInstructionTextTwo = "Press [Z+] once to release the paper".Localize();
		private static string setZHeightFineInstructionTextThree = "Finally click 'Next' to continue.".Localize();
		private static string setZHeightFineInstruction2 = string.Format("\t• {0}\n\t• {1}\n\n{2}", setZHeightFineInstructionTextOne, setZHeightFineInstructionTextTwo, setZHeightFineInstructionTextThree);

		public GetFineBedHeight(string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex, bool allowLessThan0)
			: base(pageDescription, setZHeightFineInstruction1, setZHeightFineInstruction2, .1, probePositions, probePositionsBeingEditedIndex, allowLessThan0)
		{
		}
	}

	public class GetUltraFineBedHeight : FindBedHeight
	{
		private static string setZHeightFineInstruction1 = "We will now finalize our measurement of the extruder height at this position.".Localize();
		private static string setHeightFineInstructionTextOne = "Press [Z-] one click PAST the first hint of resistance".Localize();
		private static string setHeightFineInstructionTextTwo = "Finally click 'Next' to continue.".Localize();
		private static string setZHeightFineInstruction2 = string.Format("\t• {0}\n\n\n{1}", setHeightFineInstructionTextOne, setHeightFineInstructionTextTwo);

		public GetUltraFineBedHeight(string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex, bool allowLessThan0)
			: base(pageDescription, setZHeightFineInstruction1, setZHeightFineInstruction2, .02, probePositions, probePositionsBeingEditedIndex, allowLessThan0)
		{
		}

		private bool haveDrawn = false;

		public override void OnDraw(Graphics2D graphics2D)
		{
			haveDrawn = true;
			base.OnDraw(graphics2D);
		}

		public override void PageIsBecomingInactive()
		{
			if (haveDrawn)
			{
				PrinterConnectionAndCommunication.Instance.MoveRelative(PrinterConnectionAndCommunication.Axis.Z, 2, ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds().z);
			}
			base.PageIsBecomingInactive();
		}
	}
}