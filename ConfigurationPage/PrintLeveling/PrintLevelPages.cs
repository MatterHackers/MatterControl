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
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class FirstPageInstructions : InstructionsPage
	{
		public FirstPageInstructions(string pageDescription, string instructionsText)
			: base(pageDescription, instructionsText)
		{
		}
	}

	public class SelectMaterialPage : InstructionsPage
	{
		public SelectMaterialPage(string pageDescription, string instructionsText)
			: base(pageDescription, instructionsText)
		{
			int extruderIndex = 0;
			var materialSelector = new PresetSelectorWidget(string.Format($"{"Material".Localize()} {extruderIndex + 1}"), RGBA_Bytes.Transparent, NamedSettingsLayers.Material, extruderIndex);
			materialSelector.BackgroundColor = RGBA_Bytes.Transparent;
			materialSelector.Margin = new BorderDouble(0, 0, 0, 15);
			topToBottomControls.AddChild(materialSelector);
		}
	}

	public class WaitForTempPage : InstructionsPage
	{
		private ProgressBar progressBar;
		private TextWidget progressBarText;
		double startingTemp;

		public WaitForTempPage(string pageDescription, string instructionsText)
			: base(pageDescription, instructionsText)
		{
			var holder = new FlowLayoutWidget();
			progressBar = new ProgressBar((int)(150 * GuiWidget.DeviceScale), (int)(15 * GuiWidget.DeviceScale))
			{
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
				BorderColor = ActiveTheme.Instance.PrimaryTextColor,
				BackgroundColor = RGBA_Bytes.White,
				Margin = new BorderDouble(3, 0, 0, 10),
			};
			progressBarText = new TextWidget("", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(5, 0, 0, 0),
			};
			holder.AddChild(progressBar);
			holder.AddChild(progressBarText);
			topToBottomControls.AddChild(holder);
		}

		public override void PageIsBecomingActive()
		{
			startingTemp = PrinterConnection.Instance.ActualBedTemperature;
			UiThread.RunOnIdle(ShowTempChangeProgress);

			// start heating the bed and show our progress
			PrinterConnection.Instance.TargetBedTemperature = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.bed_temperature);

			// hook our parent so we can turn off the bed when we are done with leveling
			Parent.Closed += (s, e) =>
			{
				// Make sure when the wizard closes we turn off the bed heating
				PrinterConnection.Instance.TargetBedTemperature = 0;
			};

			base.PageIsBecomingActive();
		}

		private void ShowTempChangeProgress()
		{
			progressBar.Visible = true;
			double targetTemp = PrinterConnection.Instance.TargetBedTemperature;
			double actualTemp = PrinterConnection.Instance.ActualBedTemperature;
			double totalDelta = targetTemp - startingTemp;
			double currentDelta = actualTemp - startingTemp;
			double ratioDone = totalDelta != 0 ? (currentDelta / totalDelta) : 1;
			progressBar.RatioComplete = Math.Min(Math.Max(0, ratioDone), 1);
			progressBarText.Text = $"Temperature: {actualTemp:0} / {targetTemp:0}";
			if (!HasBeenClosed)
			{
				UiThread.RunOnIdle(ShowTempChangeProgress, 1);
			}
		}
	}

	public class LastPagelInstructions : InstructionsPage
	{
		protected WizardControl container;
		private List<ProbePosition> probePositions;

		public LastPagelInstructions(WizardControl container, string pageDescription, string instructionsText, List<ProbePosition> probePositions)
			: base(pageDescription, instructionsText)
		{
			this.probePositions = probePositions;
			this.container = container;
		}

		public override void PageIsBecomingActive()
		{
			PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
			levelingData.SampledPositions.Clear();

			Vector3 zProbeOffset = new Vector3(0, 0, ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.z_probe_z_offset));
			for (int i = 0; i < probePositions.Count; i++)
			{
				levelingData.SampledPositions.Add(probePositions[i].position - zProbeOffset);
			}

			// Invoke setter forcing persistence of leveling data
			ActiveSliceSettings.Instance.Helpers.SetPrintLevelingData(levelingData, true);
			ActiveSliceSettings.Instance.Helpers.DoPrintLeveling ( true);

			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.z_homes_to_max))
			{
				PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.XYZ);
			}

			container.backButton.Enabled = false;

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

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);

			base.OnClosed(e);
		}

		public override void PageIsBecomingActive()
		{
			// first make sure there is no leftover FinishedProbe event
			PrinterConnection.Instance.ReadLine.UnregisterEvent(FinishedProbe, ref unregisterEvents);

			var feedRates = ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds();

			PrinterConnection.Instance.MoveAbsolute(PrinterConnection.Axis.Z, probeStartPosition.z, feedRates.z);
			PrinterConnection.Instance.MoveAbsolute(probeStartPosition, feedRates.x);
			PrinterConnection.Instance.SendLineToPrinterNow("G30");
			PrinterConnection.Instance.ReadLine.RegisterEvent(FinishedProbe, ref unregisterEvents);

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
					PrinterConnection.Instance.ReadLine.UnregisterEvent(FinishedProbe, ref unregisterEvents);
					int zStringPos = currentEvent.Data.LastIndexOf("Z:");
					string zProbeHeight = currentEvent.Data.Substring(zStringPos + 2);
					probePosition.position = new Vector3(probeStartPosition.x, probeStartPosition.y, double.Parse(zProbeHeight));
					PrinterConnection.Instance.MoveAbsolute(probeStartPosition, ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds().z);
					PrinterConnection.Instance.ReadPosition();

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
			PrinterConnection.Instance.HomeAxis(PrinterConnection.Axis.XYZ);
			base.PageIsBecomingActive();
		}
	}

	public class FindBedHeight : InstructionsPage
	{
		private Vector3 lastReportedPosition;
		private List<ProbePosition> probePositions;
		int probePositionsBeingEditedIndex;
		private double moveAmount;

		protected JogControls.MoveButton zPlusControl;
		protected JogControls.MoveButton zMinusControl;
		protected WizardControl container;

		public FindBedHeight(WizardControl container, string pageDescription, string setZHeightCoarseInstruction1, string setZHeightCoarseInstruction2, double moveDistance, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex)
			: base(pageDescription, setZHeightCoarseInstruction1)
		{
			this.container = container;
			this.probePositions = probePositions;
			this.moveAmount = moveDistance;
			this.lastReportedPosition = PrinterConnection.Instance.LastReportedPosition;
			this.probePositionsBeingEditedIndex = probePositionsBeingEditedIndex;

			GuiWidget spacer = new GuiWidget(15, 15);
			topToBottomControls.AddChild(spacer);

			FlowLayoutWidget zButtonsAndInfo = new FlowLayoutWidget();
			zButtonsAndInfo.HAnchor |= Agg.UI.HAnchor.Center;
			FlowLayoutWidget zButtons = CreateZButtons();
			zButtonsAndInfo.AddChild(zButtons);

			zButtonsAndInfo.AddChild(new GuiWidget(15, 10));

			//textFields
			TextWidget zPosition = new TextWidget("Z: 0.0      ", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(10, 0),
			};
			Action<TextWidget> updateUntilClose = null;
			updateUntilClose = (tw) =>
			{
				Vector3 destinationPosition = PrinterConnection.Instance.CurrentDestination;
				zPosition.Text = "Z: {0:0.00}".FormatWith(destinationPosition.z);
				UiThread.RunOnIdle(() => updateUntilClose(zPosition), .3);
			};
			updateUntilClose(zPosition);

			zButtonsAndInfo.AddChild(zPosition);

			topToBottomControls.AddChild(zButtonsAndInfo);

			AddTextField(setZHeightCoarseInstruction2, 10);
		}

		public override void PageIsBecomingActive()
		{
			// always make sure we don't have print leveling turned on
			ActiveSliceSettings.Instance.Helpers.DoPrintLeveling(false);

			base.PageIsBecomingActive();
			this.Parents<SystemWindow>().First().KeyDown += TopWindowKeyDown;
		}

		public override void PageIsBecomingInactive()
		{
			this.Parents<SystemWindow>().First().KeyDown -= TopWindowKeyDown;
			probePositions[probePositionsBeingEditedIndex].position = PrinterConnection.Instance.LastReportedPosition;
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

		public void TopWindowKeyDown(object s, KeyEventArgs keyEvent)
		{
			switch(keyEvent.KeyCode)
			{
				case Keys.Up:
					zPlusControl_Click(null, null);
					container.nextButton.Enabled = true;
					break;

				case Keys.Down:
					zMinusControl_Click(null, null);
					container.nextButton.Enabled = true;
					break;

				case Keys.Right:
					if (container.nextButton.Enabled)
					{
						UiThread.RunOnIdle(() => container.nextButton.ClickButton(null));
					}
					break;

				case Keys.Left:
					if (container.backButton.Enabled)
					{
						UiThread.RunOnIdle(() => container.backButton.ClickButton(null));
					}
					break;
			}

			base.OnKeyDown(keyEvent);
		}

		private static string zIsTooLowMessage = "You cannot move any lower. This position on your bed is too low for the extruder to reach. You need to raise your bed, or adjust your limits to allow the extruder to go lower.".Localize();
		private static string zTooLowTitle = "Warning - Moving Too Low".Localize();

		private void zMinusControl_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.Z, -moveAmount, ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds().z);
			PrinterConnection.Instance.ReadPosition();
		}

		private void zPlusControl_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.Z, moveAmount, ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds().z);
			PrinterConnection.Instance.ReadPosition();
		}
	}

	public class AutoProbeFeedback : InstructionsPage
	{
		private Vector3 lastReportedPosition;
		private List<ProbePosition> probePositions;
		int probePositionsBeingEditedIndex;

		private EventHandler unregisterEvents;
		protected Vector3 probeStartPosition;
		protected WizardControl container;

		public AutoProbeFeedback(WizardControl container, Vector3 probeStartPosition, string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex)
			: base(pageDescription, pageDescription)
		{
			this.container = container;
			this.probeStartPosition = probeStartPosition;

			this.probePositions = probePositions;

			this.lastReportedPosition = PrinterConnection.Instance.LastReportedPosition;
			this.probePositionsBeingEditedIndex = probePositionsBeingEditedIndex;

			GuiWidget spacer = new GuiWidget(15, 15);
			topToBottomControls.AddChild(spacer);

			FlowLayoutWidget textFields = new FlowLayoutWidget(FlowDirection.TopToBottom);
		}

		private void GetZProbeHeight(object sender, EventArgs e)
		{
			StringEventArgs currentEvent = e as StringEventArgs;
			if (currentEvent != null)
			{
				double sampleRead = double.MinValue;
				if (currentEvent.Data.StartsWith("Bed")) // marlin G30 return code (looks like: 'Bed Position X:20 Y:32 Z:.01')
				{
					probePositions[probePositionsBeingEditedIndex].position.x = probeStartPosition.x;
					probePositions[probePositionsBeingEditedIndex].position.y = probeStartPosition.y;
					GCodeFile.GetFirstNumberAfter("Z:", currentEvent.Data, ref sampleRead);
				}
				else if (currentEvent.Data.StartsWith("Z:")) // smoothie G30 return code (looks like: 'Z:10.01')
				{
					probePositions[probePositionsBeingEditedIndex].position.x = probeStartPosition.x;
					probePositions[probePositionsBeingEditedIndex].position.y = probeStartPosition.y;
					// smoothie returns the position relative to the start postion
					double reportedProbeZ = 0;
					GCodeFile.GetFirstNumberAfter("Z:", currentEvent.Data, ref reportedProbeZ);
					sampleRead = probeStartPosition.z - reportedProbeZ;
				}

				if (sampleRead != double.MinValue)
				{
					samples.Add(sampleRead);

					int numberOfSamples = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.z_probe_samples);
					if (samples.Count == numberOfSamples)
					{
						samples.Sort();
						if (samples.Count > 3)
						{
							// drop the high and low values
							samples.RemoveAt(0);
							samples.RemoveAt(samples.Count - 1);
						}

						probePositions[probePositionsBeingEditedIndex].position.z = Math.Round(samples.Average(), 2);
						UiThread.RunOnIdle(() => container.nextButton.ClickButton(null));
					}
				}
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		List<double> samples = new List<double>();

		public override void PageIsBecomingActive()
		{
			// always make sure we don't have print leveling turned on
			ActiveSliceSettings.Instance.Helpers.DoPrintLeveling(false);

			base.PageIsBecomingActive();

			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_z_probe)
				&& ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.use_z_probe)
				&& ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_z_servo))
			{
				// make sure the servo is deployed
				var servoDeploy = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.z_servo_depolyed_angle);
				PrinterConnection.Instance.SendLineToPrinterNow($"M280 P0 S{servoDeploy}");
			}

			var feedRates = ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds();

			var adjustedProbePosition = probeStartPosition;
			// subtract out the probe offset
			var probeOffset = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.z_probe_xy_offset);
			adjustedProbePosition -= new Vector3(probeOffset);

			PrinterConnection.Instance.MoveAbsolute(PrinterConnection.Axis.Z, probeStartPosition.z, feedRates.z);
			PrinterConnection.Instance.MoveAbsolute(adjustedProbePosition, feedRates.x);

			int numberOfSamples = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.z_probe_samples);
			for (int i = 0; i < numberOfSamples; i++)
			{
				// probe the current position
				PrinterConnection.Instance.SendLineToPrinterNow("G30");
				// raise the probe after each sample
				PrinterConnection.Instance.MoveAbsolute(adjustedProbePosition, feedRates.x);
			}

			container.backButton.Enabled = false;
			container.nextButton.Enabled = false;

			if (PrinterConnection.Instance.PrinterIsConnected
				&& !(PrinterConnection.Instance.PrinterIsPrinting
				|| PrinterConnection.Instance.PrinterIsPaused))
			{
				PrinterConnection.Instance.ReadLine.RegisterEvent(GetZProbeHeight, ref unregisterEvents);
			}
		}

		public override void PageIsBecomingInactive()
		{
			PrinterConnection.Instance.ReadLine.UnregisterEvent(GetZProbeHeight, ref unregisterEvents);
			base.PageIsBecomingInactive();
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

		public GetCoarseBedHeight(WizardControl container, Vector3 probeStartPosition, string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex)
			: base(container, pageDescription, setZHeightCoarseInstruction1, setZHeightCoarseInstruction2, 1, probePositions, probePositionsBeingEditedIndex)
		{
			this.probeStartPosition = probeStartPosition;
		}

		public override void PageIsBecomingActive()
		{
			base.PageIsBecomingActive();

			var feedRates = ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds();

			PrinterConnection.Instance.MoveAbsolute(PrinterConnection.Axis.Z, probeStartPosition.z, feedRates.z);
			PrinterConnection.Instance.MoveAbsolute(probeStartPosition, feedRates.x);
			PrinterConnection.Instance.ReadPosition();

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

			base.PageIsBecomingInactive();
		}
	}

	public class GetFineBedHeight : FindBedHeight
	{
		private static string setZHeightFineInstruction1 = "We will now refine our measurement of the extruder height at this position.".Localize();
		private static string setZHeightFineInstructionTextOne = "Press [Z-] until there is resistance to moving the paper".Localize();
		private static string setZHeightFineInstructionTextTwo = "Press [Z+] once to release the paper".Localize();
		private static string setZHeightFineInstructionTextThree = "Finally click 'Next' to continue.".Localize();
		private static string setZHeightFineInstruction2 = string.Format("\t• {0}\n\t• {1}\n\n{2}", setZHeightFineInstructionTextOne, setZHeightFineInstructionTextTwo, setZHeightFineInstructionTextThree);

		public GetFineBedHeight(WizardControl container, string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex)
			: base(container, pageDescription, setZHeightFineInstruction1, setZHeightFineInstruction2, .1, probePositions, probePositionsBeingEditedIndex)
		{
		}
	}

	public class GetUltraFineBedHeight : FindBedHeight
	{
		private static string setZHeightFineInstruction1 = "We will now finalize our measurement of the extruder height at this position.".Localize();
		private static string setHeightFineInstructionTextOne = "Press [Z-] one click PAST the first hint of resistance".Localize();
		private static string setHeightFineInstructionTextTwo = "Finally click 'Next' to continue.".Localize();
		private static string setZHeightFineInstruction2 = string.Format("\t• {0}\n\n\n{1}", setHeightFineInstructionTextOne, setHeightFineInstructionTextTwo);

		public GetUltraFineBedHeight(WizardControl container, string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex)
			: base(container, pageDescription, setZHeightFineInstruction1, setZHeightFineInstruction2, .02, probePositions, probePositionsBeingEditedIndex)
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
				PrinterConnection.Instance.MoveRelative(PrinterConnection.Axis.Z, 2, ActiveSliceSettings.Instance.Helpers.ManualMovementSpeeds().z);
			}
			base.PageIsBecomingInactive();
		}
	}
}