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

using MatterControl.Printing;
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
		public FirstPageInstructions(PrinterConfig printer, string pageDescription, string instructionsText)
			: base(printer, pageDescription, instructionsText)
		{
		}
	}

	public class SelectMaterialPage : InstructionsPage
	{
		public SelectMaterialPage(PrinterConfig printer, string pageDescription, string instructionsText)
			: base(printer, pageDescription, instructionsText)
		{
			var materialSelector = new PresetSelectorWidget(printer, "Material".Localize(), Color.Transparent, NamedSettingsLayers.Material);
			materialSelector.BackgroundColor = Color.Transparent;
			materialSelector.Margin = new BorderDouble(0, 0, 0, 15);
			topToBottomControls.AddChild(materialSelector);
		}
	}

	public class WaitForTempPage : InstructionsPage
	{
		protected WizardControl container;
		private ProgressBar progressBar;
		private TextWidget progressBarText;
		private TextWidget doneText;
		private double startingTemp;

		public WaitForTempPage(PrinterConfig printer, WizardControl container, LevelingStrings levelingStrings)
			: base(printer, levelingStrings.WaitingForTempPageStepText, levelingStrings.WaitingForTempPageInstructions)
		{
			this.container = container;
			var holder = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(0, 5)
			};
			progressBar = new ProgressBar((int)(150 * GuiWidget.DeviceScale), (int)(15 * GuiWidget.DeviceScale))
			{
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
				BorderColor = ActiveTheme.Instance.PrimaryTextColor,
				BackgroundColor = Color.White,
				Margin = new BorderDouble(3, 0, 0, 0),
				VAnchor = VAnchor.Center
			};
			progressBarText = new TextWidget("", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(5, 0, 0, 0),
				VAnchor = VAnchor.Center
			};
			holder.AddChild(progressBar);
			holder.AddChild(progressBarText);
			topToBottomControls.AddChild(holder);

			doneText = new TextWidget("Done!", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				Visible = false,
			};
			topToBottomControls.AddChild(doneText);
		}

		public override void PageIsBecomingActive()
		{
			startingTemp = printer.Connection.ActualBedTemperature;
			UiThread.RunOnIdle(ShowTempChangeProgress);

			// start heating the bed and show our progress
			printer.Connection.TargetBedTemperature = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);

			// hook our parent so we can turn off the bed when we are done with leveling
			Parent.Closed += (s, e) =>
			{
				// Make sure when the wizard closes we turn off the bed heating
				printer.Connection.TurnOffBedAndExtruders(false);
			};

			if (printer.Settings.Helpers.UseZProbe())
			{
				container.backButton.Enabled = false;
				container.nextButton.Enabled = false;
			}

			// if we are trying to go to a temp of 0 than just move on to next window
			if(printer.Settings.GetValue<double>(SettingsKey.bed_temperature) == 0)
			{
				// advance to the next page
				UiThread.RunOnIdle(() => container.nextButton.OnClick(null));
			}

			base.PageIsBecomingActive();
		}

		public override void PageIsBecomingInactive()
		{
			container.nextButton.Enabled = true;
			container.backButton.Enabled = true;

			base.PageIsBecomingInactive();
		}

		private void ShowTempChangeProgress()
		{
			progressBar.Visible = true;
			double targetTemp = printer.Connection.TargetBedTemperature;
			double actualTemp = printer.Connection.ActualBedTemperature;
			double totalDelta = targetTemp - startingTemp;
			double currentDelta = actualTemp - startingTemp;
			double ratioDone = totalDelta != 0 ? (currentDelta / totalDelta) : 1;
			progressBar.RatioComplete = Math.Min(Math.Max(0, ratioDone), 1);
			progressBarText.Text = $"Temperature: {actualTemp:0} / {targetTemp:0}";
			if (!HasBeenClosed)
			{
				UiThread.RunOnIdle(ShowTempChangeProgress, 1);
			}

			// if we are within 1 degree of our target
			if (Math.Abs(targetTemp - actualTemp) < 1
				&& doneText.Visible == false)
			{
				doneText.Visible = true;
				container.backButton.Enabled = true;
				container.nextButton.Enabled = true;

				if (printer.Settings.Helpers.UseZProbe())
				{
					// advance to the next page
					UiThread.RunOnIdle(() => container.nextButton.OnClick(null));
				}
			}
		}
	}

	public class LastPagelInstructions : InstructionsPage
	{
		protected WizardControl container;
		private List<ProbePosition> probePositions;

		public LastPagelInstructions(PrinterConfig printer, WizardControl container, string pageDescription, string instructionsText, List<ProbePosition> probePositions)
			: base(printer, pageDescription, instructionsText)
		{
			this.probePositions = probePositions;
			this.container = container;
		}

		public override void PageIsBecomingActive()
		{
			PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
			levelingData.SampledPositions.Clear();

			Vector3 zProbeOffset = new Vector3(0, 0, printer.Settings.GetValue<double>(SettingsKey.z_probe_z_offset));
			for (int i = 0; i < probePositions.Count; i++)
			{
				levelingData.SampledPositions.Add(probePositions[i].position - zProbeOffset);
			}

			// Invoke setter forcing persistence of leveling data
			printer.Settings.Helpers.SetPrintLevelingData(levelingData, true);
			printer.Settings.Helpers.DoPrintLeveling ( true);

			if (printer.Settings.GetValue<bool>(SettingsKey.z_homes_to_max))
			{
				printer.Connection.HomeAxis(PrinterConnection.Axis.XYZ);
			}

			container.backButton.Enabled = false;

			// Make sure when the wizard is done we turn off the bed heating
			printer.Connection.TurnOffBedAndExtruders(false);

			base.PageIsBecomingActive();
		}
	}

	public class GettingThirdPointFor2PointCalibration : InstructionsPage
	{
		protected Vector3 probeStartPosition;
		private ProbePosition probePosition;
		protected WizardControl container;

		public GettingThirdPointFor2PointCalibration(PrinterConfig printer, WizardControl container, string pageDescription, Vector3 probeStartPosition, string instructionsText, ProbePosition probePosition)
			: base(printer, pageDescription, instructionsText)
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
			printer.Connection.LineReceived.UnregisterEvent(FinishedProbe, ref unregisterEvents);

			var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();

			printer.Connection.MoveAbsolute(PrinterConnection.Axis.Z, probeStartPosition.Z, feedRates.Z);
			printer.Connection.MoveAbsolute(probeStartPosition, feedRates.X);
			printer.Connection.QueueLine("G30");
			printer.Connection.LineReceived.RegisterEvent(FinishedProbe, ref unregisterEvents);

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
					printer.Connection.LineReceived.UnregisterEvent(FinishedProbe, ref unregisterEvents);
					int zStringPos = currentEvent.Data.LastIndexOf("Z:");
					string zProbeHeight = currentEvent.Data.Substring(zStringPos + 2);
					probePosition.position = new Vector3(probeStartPosition.X, probeStartPosition.Y, double.Parse(zProbeHeight));
					printer.Connection.MoveAbsolute(probeStartPosition, printer.Settings.Helpers.ManualMovementSpeeds().Z);
					printer.Connection.ReadPosition();

					UiThread.RunOnIdle(() => container.nextButton.OnClick(null));
				}
			}
		}
	}

	public class HomePrinterPage : InstructionsPage
	{
		protected WizardControl container;
		private EventHandler unregisterEvents;

		public HomePrinterPage(PrinterConfig printer, WizardControl container, string pageDescription, string instructionsText)
			: base(printer, pageDescription, instructionsText)
		{
			this.container = container;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public override void PageIsBecomingActive()
		{
			// make sure we don't have anything left over
			unregisterEvents?.Invoke(this, null);

			printer.Connection.PrintingStateChanged.RegisterEvent(CheckHomeFinished, ref unregisterEvents);

			printer.Connection.HomeAxis(PrinterConnection.Axis.XYZ);

			if (printer.Settings.Helpers.UseZProbe())
			{
				container.nextButton.Enabled = false;
			}

			base.PageIsBecomingActive();
		}

		private void CheckHomeFinished(object sender, EventArgs e)
		{
			if(printer.Connection.DetailedPrintingState != DetailedPrintingState.HomingAxis)
			{
				unregisterEvents?.Invoke(this, null);
				container.nextButton.Enabled = true;
				container.backButton.Enabled = true;

				if (printer.Settings.Helpers.UseZProbe())
				{
					UiThread.RunOnIdle(() => container.nextButton.OnClick(null));
				}
			}
		}

		public override void PageIsBecomingInactive()
		{
			unregisterEvents?.Invoke(this, null);
			container.nextButton.Enabled = true;
			container.backButton.Enabled = true;

			base.PageIsBecomingInactive();
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

		public FindBedHeight(PrinterConfig printer, WizardControl container, string pageDescription, string setZHeightCoarseInstruction1, string setZHeightCoarseInstruction2, double moveDistance, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex)
			: base(printer, pageDescription, setZHeightCoarseInstruction1)
		{
			this.container = container;
			this.probePositions = probePositions;
			this.moveAmount = moveDistance;
			this.lastReportedPosition = printer.Connection.LastReportedPosition;
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
				Vector3 destinationPosition = printer.Connection.CurrentDestination;
				zPosition.Text = "Z: {0:0.00}".FormatWith(destinationPosition.Z);
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
			printer.Settings.Helpers.DoPrintLeveling(false);

			base.PageIsBecomingActive();
			this.Parents<SystemWindow>().First().KeyDown += TopWindowKeyDown;
		}

		public override void PageIsBecomingInactive()
		{
			this.Parents<SystemWindow>().First().KeyDown -= TopWindowKeyDown;
			probePositions[probePositionsBeingEditedIndex].position = printer.Connection.LastReportedPosition;
			base.PageIsBecomingInactive();
		}

		private FlowLayoutWidget CreateZButtons()
		{
			FlowLayoutWidget zButtons = JogControls.CreateZButtons(printer, Color.White, 4, out zPlusControl, out zMinusControl, true);
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
						UiThread.RunOnIdle(() => container.nextButton.OnClick(null));
					}
					break;

				case Keys.Left:
					if (container.backButton.Enabled)
					{
						UiThread.RunOnIdle(() => container.backButton.OnClick(null));
					}
					break;
			}

			base.OnKeyDown(keyEvent);
		}

		private void zMinusControl_Click(object sender, EventArgs mouseEvent)
		{
			printer.Connection.MoveRelative(PrinterConnection.Axis.Z, -moveAmount, printer.Settings.Helpers.ManualMovementSpeeds().Z);
			printer.Connection.ReadPosition();
		}

		private void zPlusControl_Click(object sender, EventArgs mouseEvent)
		{
			printer.Connection.MoveRelative(PrinterConnection.Axis.Z, moveAmount, printer.Settings.Helpers.ManualMovementSpeeds().Z);
			printer.Connection.ReadPosition();
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

		public AutoProbeFeedback(PrinterConfig printer, WizardControl container, Vector3 probeStartPosition, string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex)
			: base(printer, pageDescription, pageDescription)
		{
			this.container = container;
			this.probeStartPosition = probeStartPosition;

			this.probePositions = probePositions;

			this.lastReportedPosition = printer.Connection.LastReportedPosition;
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
					probePositions[probePositionsBeingEditedIndex].position.X = probeStartPosition.X;
					probePositions[probePositionsBeingEditedIndex].position.Y = probeStartPosition.Y;
					GCodeFile.GetFirstNumberAfter("Z:", currentEvent.Data, ref sampleRead);
				}
				else if (currentEvent.Data.StartsWith("Z:")) // smoothie G30 return code (looks like: 'Z:10.01')
				{
					probePositions[probePositionsBeingEditedIndex].position.X = probeStartPosition.X;
					probePositions[probePositionsBeingEditedIndex].position.Y = probeStartPosition.Y;
					// smoothie returns the position relative to the start postion
					double reportedProbeZ = 0;
					GCodeFile.GetFirstNumberAfter("Z:", currentEvent.Data, ref reportedProbeZ);
					sampleRead = probeStartPosition.Z - reportedProbeZ;
				}

				if (sampleRead != double.MinValue)
				{
					samples.Add(sampleRead);

					int numberOfSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);
					if (samples.Count == numberOfSamples)
					{
						samples.Sort();
						if (samples.Count > 3)
						{
							// drop the high and low values
							samples.RemoveAt(0);
							samples.RemoveAt(samples.Count - 1);
						}

						probePositions[probePositionsBeingEditedIndex].position.Z = Math.Round(samples.Average(), 2);
						UiThread.RunOnIdle(() => container.nextButton.OnClick(null));
					}
				}
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		List<double> samples = new List<double>();

		public override void PageIsBecomingActive()
		{
			// always make sure we don't have print leveling turned on
			printer.Settings.Helpers.DoPrintLeveling(false);

			base.PageIsBecomingActive();

			if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
			{
				// make sure the servo is deployed
				var servoDeploy = printer.Settings.GetValue<double>(SettingsKey.z_servo_depolyed_angle);
				printer.Connection.QueueLine($"M280 P0 S{servoDeploy}");
			}

			var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();

			var adjustedProbePosition = probeStartPosition;
			// subtract out the probe offset
			var probeOffset = printer.Settings.GetValue<Vector2>(SettingsKey.z_probe_xy_offset);
			adjustedProbePosition -= new Vector3(probeOffset);

			printer.Connection.MoveAbsolute(PrinterConnection.Axis.Z, probeStartPosition.Z, feedRates.Z);
			printer.Connection.MoveAbsolute(adjustedProbePosition, feedRates.X);

			int numberOfSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);
			for (int i = 0; i < numberOfSamples; i++)
			{
				// probe the current position
				printer.Connection.QueueLine("G30");
				// raise the probe after each sample
				printer.Connection.MoveAbsolute(adjustedProbePosition, feedRates.X);
			}

			container.backButton.Enabled = false;
			container.nextButton.Enabled = false;

			if (printer.Connection.IsConnected
				&& !(printer.Connection.PrinterIsPrinting
				|| printer.Connection.PrinterIsPaused))
			{
				printer.Connection.LineReceived.RegisterEvent(GetZProbeHeight, ref unregisterEvents);
			}
		}

		public override void PageIsBecomingInactive()
		{
			printer.Connection.LineReceived.UnregisterEvent(GetZProbeHeight, ref unregisterEvents);
			base.PageIsBecomingInactive();
		}
	}

	public class GetCoarseBedHeight : FindBedHeight
	{
		protected Vector3 probeStartPosition;

		public GetCoarseBedHeight(PrinterConfig printer, WizardControl container, Vector3 probeStartPosition, string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex, LevelingStrings levelingStrings)
			: base(printer, container, pageDescription, levelingStrings.CoarseInstruction1, levelingStrings.CoarseInstruction2, 1, probePositions, probePositionsBeingEditedIndex)
		{
			this.probeStartPosition = probeStartPosition;
		}

		public override void PageIsBecomingActive()
		{
			base.PageIsBecomingActive();

			var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();

			printer.Connection.MoveAbsolute(PrinterConnection.Axis.Z, probeStartPosition.Z, feedRates.Z);
			printer.Connection.MoveAbsolute(probeStartPosition, feedRates.X);
			printer.Connection.ReadPosition();

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
		public GetFineBedHeight(PrinterConfig printer, WizardControl container, string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex, LevelingStrings levelingStrings)
			: base(printer, container, pageDescription, levelingStrings.FineInstruction1, levelingStrings.FineInstruction2, .1, probePositions, probePositionsBeingEditedIndex)
		{
		}
	}

	public class GetUltraFineBedHeight : FindBedHeight
	{
		public GetUltraFineBedHeight(PrinterConfig printer, WizardControl container, string pageDescription, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex, LevelingStrings levelingStrings)
			: base(printer, container, pageDescription, levelingStrings.UltraFineInstruction1, levelingStrings.FineInstruction2, .02, probePositions, probePositionsBeingEditedIndex)
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
				printer.Connection.MoveRelative(PrinterConnection.Axis.Z, 2, printer.Settings.Helpers.ManualMovementSpeeds().Z);
			}
			base.PageIsBecomingInactive();
		}
	}
}